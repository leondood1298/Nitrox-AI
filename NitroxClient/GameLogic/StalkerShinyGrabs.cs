using System.Collections.Generic;
using Nitrox.Model.DataStructures;
using NitroxClient.GameLogic.PlayerLogic;
using NitroxClient.GameLogic.Simulation;
using NitroxClient.MonoBehaviours;
using UnityEngine;

namespace NitroxClient.GameLogic;

/// <summary>
///     Stalkers pick up shiny things (metal salvage, ores, dropped tools) and carry them to their nest. The client that
///     simulates a Stalker takes the item's exclusive simulation lock first, as a player's propulsion cannon does, and
///     sends where the item is while it is carried and after it is dropped. Without the lock, another player who
///     simulates the item (the one who dropped it) kept sending where it lay, which pulled it out of the Stalker's mouth,
///     and nobody else saw it move: the item stayed where it was for them and the server.
/// </summary>
/// <remarks>
///     Scanner Room cameras are left to <see cref="StalkerCameraGrabs" />, whose lock is also how a player controls them.
/// </remarks>
public sealed class StalkerShinyGrabs(SimulationOwnership simulationOwnership, StalkerCameraGrabs stalkerCameraGrabs)
{
    /// <summary>
    ///     How long the game makes a Stalker wait before it looks for something else after it went for an item a player
    ///     holds (CollectShiny.TryPickupShinyTarget).
    /// </summary>
    private const float HELD_BY_PLAYER_RETRY_SECONDS = 6f;

    /// <summary>
    ///     The Stalker of this client that carries each item.
    /// </summary>
    private readonly Dictionary<NitroxId, IStalker> carriersByItemId = [];

    /// <summary>
    ///     Items whose lock was asked for and not answered yet. The game tries to pick the item up every frame while the
    ///     Stalker is close to it.
    /// </summary>
    private readonly HashSet<NitroxId> requestedItemIds = [];

    private bool applyingGrantedPickup;

    /// <summary>
    ///     Called before the game lets a Stalker pick up what it went for.
    /// </summary>
    /// <returns>False if the item's lock has to be asked for first; the Stalker picks it up when granted.</returns>
    public bool MayPickUp(CollectShiny collectShiny)
    {
        GameObject target = collectShiny.shinyTarget;
        if (applyingGrantedPickup || !target)
        {
            return true;
        }
        if (target.GetComponent<MapRoomCamera>())
        {
            return stalkerCameraGrabs.MayPickUp(collectShiny);
        }
        if (target.GetComponentInParent<RemotePlayerIdentifier>())
        {
            // The game lets go of an item in its own player's hands; another player's held item is in that player's body here
            collectShiny.shinyTarget = null;
            collectShiny.targetPickedUp = false;
            collectShiny.timeNextFindShiny = Time.time + HELD_BY_PLAYER_RETRY_SECONDS;
            return false;
        }
        if (!target.TryGetNitroxId(out NitroxId itemId))
        {
            // Not an entity: exists only on this game
            return true;
        }
        return MayPickUp(new GameStalker(collectShiny, target), itemId);
    }

    /// <summary>
    ///     Called before the game makes a Stalker drop what it carries.
    /// </summary>
    public void Dropped(GameObject target)
    {
        if (!target)
        {
            return;
        }
        if (target.GetComponent<MapRoomCamera>())
        {
            stalkerCameraGrabs.Dropped(target);
            return;
        }
        if (target.TryGetNitroxId(out NitroxId itemId))
        {
            Dropped(itemId);
        }
    }

    /// <summary>
    ///     Called when the simulation lock on an entity went to another player. If a Stalker of this client carries it,
    ///     the Stalker lets go of it.
    /// </summary>
    public void LockLost(NitroxId itemId)
    {
        if (carriersByItemId.TryGetValue(itemId, out IStalker stalker))
        {
            carriersByItemId.Remove(itemId);
            stalker.Drop();
        }
    }

    internal bool MayPickUp(IStalker stalker, NitroxId itemId)
    {
        if (simulationOwnership.HasExclusiveLock(itemId))
        {
            Carry(stalker, itemId);
            return true;
        }
        if (requestedItemIds.Add(itemId))
        {
            simulationOwnership.RequestSimulationLock(new LockRequest<StalkerShinyGrab>(itemId, SimulationLockType.EXCLUSIVE, LockAnswered, new StalkerShinyGrab(stalker)));
        }
        return false;
    }

    internal void Dropped(NitroxId itemId)
    {
        if (carriersByItemId.Remove(itemId))
        {
            GiveLockBack(itemId);
        }
    }

    private void LockAnswered(NitroxId itemId, bool acquired, StalkerShinyGrab grab)
    {
        requestedItemIds.Remove(itemId);
        IStalker stalker = grab.Stalker;
        if (!acquired)
        {
            // Another player holds it: in their propulsion cannon, or in the mouth of a Stalker they simulate
            stalker.ForgetTarget();
            return;
        }
        if (!stalker.StillWantsTarget)
        {
            GiveLockBack(itemId);
            stalker.ForgetTarget();
            return;
        }
        Carry(stalker, itemId);
        bool pickedUp = false;
        try
        {
            applyingGrantedPickup = true;
            stalker.PickUp();
            pickedUp = true;
        }
        finally
        {
            applyingGrantedPickup = false;
            if (!pickedUp)
            {
                carriersByItemId.Remove(itemId);
                GiveLockBack(itemId);
            }
        }
    }

    private void Carry(IStalker stalker, NitroxId itemId)
    {
        carriersByItemId[itemId] = stalker;
        if (EntityPositionBroadcaster.Instance)
        {
            EntityPositionBroadcaster.Instance.WatchEntity(itemId);
        }
    }

    /// <summary>
    ///     Like a propulsion cannon's release: the lock goes back to transient and this client keeps sending where the item
    ///     is, so that it lands in the same place for everybody.
    /// </summary>
    private void GiveLockBack(NitroxId itemId)
    {
        if (simulationOwnership.HasExclusiveLock(itemId))
        {
            // Known here at once, so that the next Stalker asks for the exclusive lock again
            simulationOwnership.SimulateEntity(itemId, SimulationLockType.TRANSIENT);
            simulationOwnership.RequestSimulationLock(itemId, SimulationLockType.TRANSIENT);
        }
    }

    /// <summary>
    ///     A Stalker of the game and the item it is after. Tests put a Stalker without the game here.
    /// </summary>
    internal interface IStalker
    {
        /// <summary>
        ///     False if the Stalker went for something else, or is gone, while the lock was asked for.
        /// </summary>
        bool StillWantsTarget { get; }

        void PickUp();
        void Drop();
        void ForgetTarget();
    }

    private sealed class GameStalker(CollectShiny collectShiny, GameObject item) : IStalker
    {
        public bool StillWantsTarget => collectShiny && item && collectShiny.shinyTarget == item;

        public void PickUp() => collectShiny.TryPickupShinyTarget();

        public void Drop()
        {
            if (StillWantsTarget)
            {
                collectShiny.DropShinyTarget();
            }
        }

        public void ForgetTarget()
        {
            if (StillWantsTarget)
            {
                collectShiny.shinyTarget = null;
                collectShiny.targetPickedUp = false;
            }
        }
    }
}
