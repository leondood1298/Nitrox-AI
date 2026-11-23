using System;
using NitroxClient.GameLogic.PlayerLogic;
using UnityEngine;

namespace NitroxClient.GameLogic;

/// <summary>
///     Lets the precursor pedestals, key terminal covers and the gun terminal area react to other players as they react to
///     the local one. The game only looks for the local Player: such a trigger stayed shut in front of another player and
///     closed when one of two players left.
/// </summary>
/// <remarks>
///     Who is still inside is worked out from the players' positions when somebody leaves, not counted from enter and exit
///     events: Unity sends no exit for a player who disconnects or whose collider is turned off while inside.
/// </remarks>
public class PrecursorProximityTriggers
{
    // FMOD distance used by PrecursorActivatedPillar.OnTriggerEnter
    private const float PILLAR_SOUND_DISTANCE = 20f;
    private const float INSIDE_TOLERANCE_SQUARED = 0.0001f;

    private readonly PlayerManager playerManager;

    public PrecursorProximityTriggers(PlayerManager playerManager)
    {
        this.playerManager = playerManager;
    }

    /// <returns>Whether the game's own handling should run.</returns>
    public bool PillarEntered(PrecursorActivatedPillar pillar, Collider other)
    {
        return Entered(pillar, other, () =>
        {
            // PrecursorActivatedPillar.OnTriggerEnter, which only accepts the local Player
            if (!pillar.active)
            {
                return;
            }
            if (pillar.openSound)
            {
                Utils.PlayFMODAsset(pillar.openSound, pillar.transform, PILLAR_SOUND_DISTANCE);
            }
            if (pillar.openedLoopingSound)
            {
                pillar.openedLoopingSound.Play();
            }
            pillar.extended = true;
            pillar.isFullyExtended = false;
        });
    }

    /// <inheritdoc cref="PillarEntered" />
    public bool PillarExited(PrecursorActivatedPillar pillar, Collider other)
    {
        return Exited(pillar, other, pillar.Retract);
    }

    /// <inheritdoc cref="PillarEntered" />
    public bool KeyTerminalTriggerEntered(PrecursorKeyTerminalTrigger trigger, Collider other)
    {
        return Entered(trigger, other, () => trigger.SendMessageUpwards(nameof(PrecursorKeyTerminal.OpenDeck)));
    }

    /// <inheritdoc cref="PillarEntered" />
    public bool KeyTerminalTriggerExited(PrecursorKeyTerminalTrigger trigger, Collider other)
    {
        return Exited(trigger, other, () => trigger.SendMessageUpwards(nameof(PrecursorKeyTerminal.CloseDeck)));
    }

    /// <inheritdoc cref="PillarEntered" />
    public bool GunTerminalAreaEntered(PrecursorDisableGunTerminalArea area, Collider other)
    {
        return Entered(area, other, area.terminal.OnTerminalAreaEnter);
    }

    /// <inheritdoc cref="PillarEntered" />
    public bool GunTerminalAreaExited(PrecursorDisableGunTerminalArea area, Collider other)
    {
        return Exited(area, other, area.terminal.OnTerminalAreaExit);
    }

    private bool Entered(Component trigger, Collider other, Action open)
    {
        if (TryGetRemotePlayer(other, out RemotePlayer remotePlayer))
        {
            if (!IsAnotherPlayerInside(trigger, remotePlayer.Body))
            {
                open();
            }
            return false;
        }

        // The game judges the local player. When somebody else is inside, it is open already and opening it again would
        // only repeat its sound.
        return !IsAnotherPlayerInside(trigger, Player.main.gameObject);
    }

    private bool Exited(Component trigger, Collider other, Action close)
    {
        if (TryGetRemotePlayer(other, out RemotePlayer remotePlayer))
        {
            if (!IsAnotherPlayerInside(trigger, remotePlayer.Body))
            {
                close();
            }
            return false;
        }

        return !IsAnotherPlayerInside(trigger, Player.main.gameObject);
    }

    private static bool TryGetRemotePlayer(Collider other, out RemotePlayer remotePlayer)
    {
        // Only the body's own collider: the remote player's held tools and ping have colliders of their own under it
        if (other.TryGetComponent(out RemotePlayerIdentifier identifier) && identifier.RemotePlayer.Collider == other)
        {
            remotePlayer = identifier.RemotePlayer;
            return true;
        }

        remotePlayer = null;
        return false;
    }

    private bool IsAnotherPlayerInside(Component trigger, GameObject leavingPlayer)
    {
        Collider[] triggerColliders = trigger.GetComponentsInChildren<Collider>();

        if (Player.main && Player.main.gameObject != leavingPlayer && IsInside(triggerColliders, Player.main.transform.position))
        {
            return true;
        }

        foreach (RemotePlayer remotePlayer in playerManager.GetAll())
        {
            if (remotePlayer.Body && remotePlayer.Body != leavingPlayer && IsInside(triggerColliders, remotePlayer.Body.transform.position))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsInside(Collider[] triggerColliders, Vector3 position)
    {
        foreach (Collider triggerCollider in triggerColliders)
        {
            if (!triggerCollider.isTrigger || !triggerCollider.enabled)
            {
                continue;
            }

            // ClosestPoint is only defined for convex shapes, and returns the position itself when it is inside
            bool inside = triggerCollider is MeshCollider { convex: false }
                              ? triggerCollider.bounds.Contains(position)
                              : (triggerCollider.ClosestPoint(position) - position).sqrMagnitude < INSIDE_TOLERANCE_SQUARED;
            if (inside)
            {
                return true;
            }
        }

        return false;
    }
}
