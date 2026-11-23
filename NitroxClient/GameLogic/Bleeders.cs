using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.Packets;
using NitroxClient.Communication.Abstract;
using NitroxClient.GameLogic.PlayerLogic;
using NitroxClient.GameLogic.Simulation;
using NitroxClient.MonoBehaviours;
using UnityEngine;
using static Nitrox.Model.Subnautica.Packets.CreatureEffectPlayed;

namespace NitroxClient.GameLogic;

/// <summary>
///     A Bleeder latches onto a player who does not simulate it. The game only lets it latch onto the player of the game
///     that runs it, when it collides with that player (AttachAndSuck.OnCollisionEnter): another player's body is a trigger
///     there, which nothing collides with, and its BleederAttachTarget sits in first-person arms nobody looks through.<br />
///     - the Bleeder's simulating game decides the latch: it sees the Bleeder touch another player's body and tells that
///     player's game (<see cref="CreatureEffectPlayed" />)<br />
///     - the latched player's game takes the Bleeder over (an exclusive lock, as for anything a player holds) and latches
///     it onto its own arm the way the game does: it shows it in first person, takes its bites once and can punch it off,
///     and sends where it is, so everybody sees it on that player<br />
///     - once it lets go, the lock goes back to an ordinary one<br />
///     A copy of another player's Bleeder never latches on by itself.
/// </summary>
/// <remarks>Unity-bound but for <see cref="TouchesBody" />.</remarks>
public sealed class Bleeders(IPacketSender packetSender, SimulationOwnership simulationOwnership, PlayerManager playerManager)
{
    /// <summary>
    ///     How near a Bleeder must come to another player's body to latch on: about where it would collide with them.
    /// </summary>
    public const float TOUCH_DISTANCE = 1.2f;

    /// <summary>
    ///     AttachAndSuck.OnCollisionEnter waits this long after letting go before it latches on again.
    /// </summary>
    private const float REATTACH_DELAY = 4f;

    private readonly IPacketSender packetSender = packetSender;
    private readonly SimulationOwnership simulationOwnership = simulationOwnership;
    private readonly PlayerManager playerManager = playerManager;

    public static bool TouchesBody(float distance) => distance <= TOUCH_DISTANCE;

    /// <summary>
    ///     Called before AttachAndSuck.OnCollisionEnter.
    /// </summary>
    public bool MayLatchOnHere(AttachAndSuck attachAndSuck)
    {
        return !attachAndSuck.TryGetNitroxId(out NitroxId bleederId) || simulationOwnership.HasAnyLockType(bleederId);
    }

    /// <summary>
    ///     Called after AttachAndSuck.Update on every game: on the Bleeder's simulating game, a touch of another player's body.
    /// </summary>
    public void CheckTouchOfOtherPlayers(AttachAndSuck attachAndSuck)
    {
        if (attachAndSuck.active || attachAndSuck.currentTarget || Time.time < attachAndSuck.timeDetached + REATTACH_DELAY ||
            !attachAndSuck.TryGetNitroxId(out NitroxId bleederId) || !simulationOwnership.HasAnyLockType(bleederId))
        {
            return;
        }
        LiveMixin liveMixin = attachAndSuck.GetComponent<LiveMixin>();
        if (!liveMixin || !liveMixin.IsAlive())
        {
            return;
        }

        Vector3 position = attachAndSuck.transform.position;
        foreach (RemotePlayer remotePlayer in playerManager.GetAll())
        {
            if (!remotePlayer.Body || remotePlayer.Vehicle || !remotePlayer.CanBeAttacked() || !remotePlayer.LiveMixin || !remotePlayer.LiveMixin.IsAlive() ||
                !TouchesBody(Vector3.Distance(remotePlayer.Body.transform.position, position)))
            {
                continue;
            }
            // Not again while that player's game takes it over
            attachAndSuck.timeDetached = Time.time;
            packetSender.Send(new CreatureEffectPlayed(bleederId, CreatureEffect.BLEEDER_ATTACHED, 0, Optional.Of(remotePlayer.PlayerContext.PlayerNitroxId)));
            return;
        }
    }

    /// <summary>
    ///     On the game of the player a Bleeder latched onto: takes it over, then latches it on.
    /// </summary>
    public void LatchedOntoLocalPlayer(GameObject bleederObject, NitroxId bleederId)
    {
        Player player = Player.main;
        if (!player || !player.armsController || !player.armsController.bleederAttackTarget || player.armsController.bleederAttackTarget.occupied ||
            !player.CanBeAttacked() || player.IsInSub() || player.GetMode() != Player.Mode.Normal || !bleederObject.TryGetComponent(out AttachAndSuck _))
        {
            return;
        }
        simulationOwnership.RequestSimulationLock(new LockRequest<LatchContext>(bleederId, SimulationLockType.EXCLUSIVE, OnLatchLock, new LatchContext()));
    }

    /// <summary>
    ///     Called after AttachAndSuck.SetDetached: the player it held keeps an ordinary lock on it.
    /// </summary>
    public void Detached(AttachAndSuck attachAndSuck)
    {
        if (attachAndSuck.TryGetNitroxId(out NitroxId bleederId) && simulationOwnership.HasExclusiveLock(bleederId))
        {
            simulationOwnership.RequestSimulationLock(bleederId, SimulationLockType.TRANSIENT);
        }
    }

    private static void OnLatchLock(NitroxId bleederId, bool acquired, LatchContext context)
    {
        Player player = Player.main;
        if (!acquired || !player || !player.armsController || !NitroxEntity.TryGetComponentFrom(bleederId, out AttachAndSuck attachAndSuck))
        {
            return;
        }
        BleederAttachTarget target = player.armsController.bleederAttackTarget;
        if (!target || target.occupied || attachAndSuck.currentTarget)
        {
            return;
        }

        // What AttachAndSuck.OnCollisionEnter does on touching the player; Creature.UpdateBehaviour then starts sucking
        Transform bleederTransform = attachAndSuck.transform;
        attachAndSuck.currentTarget = target;
        attachAndSuck.targetLiveMixin = player.liveMixin;
        attachAndSuck.attached = false;
        attachAndSuck.startPos = bleederTransform.position;
        attachAndSuck.startRot = bleederTransform.rotation;
        attachAndSuck.timeTargetSet = Time.time;
        target.occupied = true;
        target.attached = false;
        target.bleeder = attachAndSuck.bleeder;
    }

    private sealed class LatchContext : LockRequestContext
    {
    }
}
