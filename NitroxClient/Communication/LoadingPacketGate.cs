using System;
using System.Collections.Generic;
using Nitrox.Model.Packets;
using Nitrox.Model.Subnautica.Packets;

namespace NitroxClient.Communication;

/// <summary>
///     Decides what happens to a packet that arrives before the game is in the multiplayer world.
/// </summary>
/// <remarks>
///     <para>
///         Before the game asks to join (main menu, name panel), the world it would apply packets to is not the
///         multiplayer world, and the snapshot it gets later is newer, so everything but the session's own packets is
///         dropped.
///     </para>
///     <para>
///         From the join request until the snapshot is applied, packets are held and replayed in order once it is.
///         The server only broadcasts to a session once it has become a player, right before it gathers the snapshot,
///         so a held packet is either already in the snapshot (replaying it must change nothing) or newer than it. A
///         packet that only shows something passing (a movement, a sound) is dropped instead, as the next one carries
///         the state.
///     </para>
///     Unity-free so that it can be tested.
/// </remarks>
public sealed class LoadingPacketGate
{
    public enum Policy
    {
        /// <summary>Held until the snapshot is applied. The default.</summary>
        HOLD,

        /// <summary>Processed right away: the session itself, not the world.</summary>
        IMMEDIATE,

        /// <summary>Thrown away: something passing that the next packet of its kind replaces.</summary>
        DROP
    }

    public enum Phase
    {
        /// <summary>Connected, not yet asked to join.</summary>
        NOT_JOINING,

        /// <summary>Asked to join: queued, loading or applying the snapshot.</summary>
        LOADING,

        /// <summary>The snapshot is applied; packets pass.</summary>
        IN_GAME
    }

    private static readonly HashSet<Type> immediateTypes =
    [
        typeof(MultiplayerSessionPolicy),
        typeof(MultiplayerSessionReservation),
        typeof(JoinQueueInfo),
        typeof(InitialPlayerSync),
        typeof(InitialSyncTransfer),
        typeof(PlayerKicked),
        typeof(ServerStopped),
        typeof(DiscordRequestIP)
    ];

    private static readonly HashSet<Type> droppedTypes =
    [
        // Movements: the next one has the position
        typeof(PlayerMovement),
        typeof(PlayerInCyclopsMovement),
        typeof(VehicleMovements),
        typeof(GrapplingHookMovement),
        typeof(EntityTransformUpdates),
        // Sounds and other passing effects
        typeof(FootstepPacket),
        typeof(FMODAssetPacket),
        typeof(FMODCustomEmitterPacket),
        typeof(FMODCustomLoopingEmitterPacket),
        typeof(FMODStudioEmitterPacket),
        typeof(FMODEventInstancePacket),
        typeof(CreatureBitePlayed),
        typeof(TorpedoShot),
        typeof(TorpedoHit),
        typeof(TorpedoTargetAcquired),
        typeof(StasisSphereShot),
        typeof(StasisSphereHit),
        // A Cyclops decoy lives for a moment; the snapshot holds the decoys left in the tube
        typeof(CyclopsDecoyLaunch),
        typeof(CyclopsDecoyUpdate),
        // Sent every few seconds
        typeof(PlayerStats),
        // A loading player is not in bed: it must not get the sleep screen or wake up from a sleep it was not part of
        typeof(SleepStatusUpdate),
        typeof(SleepComplete)
    ];

    private readonly Queue<Packet> held = new();

    public Phase CurrentPhase { get; private set; } = Phase.NOT_JOINING;

    public int HeldCount => held.Count;

    public static Policy PolicyOf(Type packetType)
    {
        if (immediateTypes.Contains(packetType))
        {
            return Policy.IMMEDIATE;
        }
        if (droppedTypes.Contains(packetType))
        {
            return Policy.DROP;
        }
        return Policy.HOLD;
    }

    /// <summary>
    ///     The game asked the server to join (it now waits in the queue or loads).
    /// </summary>
    public void OnJoinRequested()
    {
        if (CurrentPhase == Phase.NOT_JOINING)
        {
            CurrentPhase = Phase.LOADING;
        }
    }

    /// <returns>True if the packet is to be processed now; otherwise it was held or dropped.</returns>
    public bool Admit(Packet packet)
    {
        if (CurrentPhase == Phase.IN_GAME)
        {
            return true;
        }
        switch (PolicyOf(packet.GetType()))
        {
            case Policy.IMMEDIATE:
                return true;
            case Policy.HOLD when CurrentPhase == Phase.LOADING:
                held.Enqueue(packet);
                return false;
            default:
                return false;
        }
    }

    /// <summary>
    ///     Takes the oldest held packet to replay. Packets that arrive while the replay runs over several frames are
    ///     held behind the others, so that they keep their order; <see cref="FinishLoading" /> once none is left.
    /// </summary>
    public bool TryTakeHeld(out Packet packet)
    {
        if (held.Count == 0)
        {
            packet = null;
            return false;
        }
        packet = held.Dequeue();
        return true;
    }

    /// <summary>
    ///     The snapshot is applied and every held packet replayed; from now on every packet passes.
    /// </summary>
    public void FinishLoading()
    {
        CurrentPhase = Phase.IN_GAME;
    }

    /// <summary>
    ///     The snapshot is applied. Returns the held packets in the order they arrived; from now on every packet passes.
    /// </summary>
    public List<Packet> Release()
    {
        List<Packet> released = [.. held];
        held.Clear();
        CurrentPhase = Phase.IN_GAME;
        return released;
    }
}
