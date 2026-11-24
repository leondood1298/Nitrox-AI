using System.Collections.Generic;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;
using NitroxClient.MonoBehaviours;
using UnityEngine;

namespace NitroxClient.GameLogic;

/// <summary>
///     Keeps a creature's leash (Creature.leashPosition, the place it stays around) and its size (Creature.Size) the same on
///     every game and on the server, in its creature metadata (<see cref="CreatureMetadata.LeashPosition" />,
///     <see cref="CreatureMetadata.Size" />).<br />
///     - the server seeds the leash where it spawns a creature, as Creature.InitializeOnce does in the game<br />
///     - every game applies what it receives; a leash received before the creature started is applied once Creature.Start
///     has run InitializeOnce, which would otherwise put it where the creature is<br />
///     - only the creature's simulating player sends: its leash when it moves (fleeing, a Stalker's nest, a Cuddlefish
///     following) and the size Creature.Start rolled when nobody had decided it
/// </summary>
/// <remarks>
///     Sea Treaders keep their leash with their path (SeaTreaderMetadata); contained fish grow (WaterParkCreatureMetadata).
///     Unity-bound but for <see cref="LeashMovedEnough" />.
/// </remarks>
public sealed class CreatureLeashes(SimulationOwnership simulationOwnership, Entities entities)
{
    /// <summary>
    ///     A leash that moved less than this is not sent again. Some creatures move it all the time (a Skyray, a Cuddlefish
    ///     following a player).
    /// </summary>
    public const float LEASH_RESEND_DISTANCE = 2f;

    private const float LEASH_THROTTLE_SECONDS = 2f;

    private readonly SimulationOwnership simulationOwnership = simulationOwnership;
    private readonly Entities entities = entities;

    private readonly Dictionary<NitroxId, Vector3> knownLeashById = [];
    private readonly Dictionary<NitroxId, Vector3> pendingLeashById = [];
    private readonly HashSet<NitroxId> knownSizeIds = [];

    public static bool LeashMovedEnough(float distance) => distance >= LEASH_RESEND_DISTANCE;

    /// <summary>
    ///     Applies the leash and size carried by a creature's metadata.
    /// </summary>
    public void ApplyReceived(GameObject gameObject, CreatureMetadata metadata)
    {
        if (!gameObject.TryGetComponent(out Creature creature) || !gameObject.TryGetNitroxId(out NitroxId creatureId) || creature is SeaTreader)
        {
            return;
        }
        if (metadata.HasSize && !creature.GetComponent<GrowMixin>() && !creature.GetComponent<WaterParkCreature>())
        {
            knownSizeIds.Add(creatureId);
            // Before Creature.Start this also keeps it from rolling a size of its own
            creature.SetSize(metadata.Size);
        }
        if (metadata.HasLeash)
        {
            Vector3 leash = metadata.LeashPosition.ToUnity();
            knownLeashById[creatureId] = leash;
            if (creature.isInitialized)
            {
                creature.leashPosition = leash;
            }
            else
            {
                pendingLeashById[creatureId] = leash;
            }
        }
    }

    /// <summary>
    ///     Called after Creature.Start, and when this player starts simulating a creature.
    /// </summary>
    public void OnStarted(Creature creature)
    {
        if (!creature || !creature.TryGetNitroxId(out NitroxId creatureId) || creature is SeaTreader)
        {
            return;
        }
        if (pendingLeashById.TryGetValue(creatureId, out Vector3 pendingLeash))
        {
            pendingLeashById.Remove(creatureId);
            creature.leashPosition = pendingLeash;
        }
        if (!simulationOwnership.HasAnyLockType(creatureId) || IsDeadOrContained(creature))
        {
            return;
        }

        bool sendLeash = !knownLeashById.ContainsKey(creatureId);
        bool sendSize = !knownSizeIds.Contains(creatureId) && creature.Size >= 0f && !creature.GetComponent<GrowMixin>();
        if (sendLeash || sendSize)
        {
            Send(creature, creatureId, sendLeash, sendSize, false);
        }
    }

    /// <summary>
    ///     Called after Creature.UpdateBehaviour: sends a leash its simulating player's game moved.
    /// </summary>
    public void OnBehaviourUpdated(Creature creature)
    {
        if (!creature.TryGetNitroxId(out NitroxId creatureId) || creature is SeaTreader || !simulationOwnership.HasAnyLockType(creatureId) ||
            !knownLeashById.TryGetValue(creatureId, out Vector3 knownLeash) || !LeashMovedEnough(Vector3.Distance(knownLeash, creature.leashPosition)) ||
            IsDeadOrContained(creature))
        {
            return;
        }
        Send(creature, creatureId, true, false, true);
    }

    private void Send(Creature creature, NitroxId creatureId, bool leash, bool size, bool throttled)
    {
        // The infection is merged into whatever creature metadata the server has; undecided leaves it as it is
        CreatureMetadata metadata = new CreatureInfectionMetadata(CreatureMetadata.UNDECIDED);
        if (leash)
        {
            knownLeashById[creatureId] = creature.leashPosition;
            metadata.WithLeash(creature.leashPosition.ToDto());
        }
        if (size)
        {
            knownSizeIds.Add(creatureId);
            metadata.WithSize(creature.Size);
        }
        if (throttled)
        {
            entities.BroadcastMetadataUpdateThrottled(creatureId, metadata, LEASH_THROTTLE_SECONDS);
        }
        else
        {
            entities.BroadcastMetadataUpdate(creatureId, metadata);
        }
    }

    private static bool IsDeadOrContained(Creature creature)
    {
        LiveMixin liveMixin = creature.GetComponent<LiveMixin>();
        if (liveMixin && !liveMixin.IsAlive())
        {
            return true;
        }
        return creature.TryGetComponent(out WaterParkCreature waterParkCreature) && waterParkCreature.IsInsideWaterPark();
    }
}
