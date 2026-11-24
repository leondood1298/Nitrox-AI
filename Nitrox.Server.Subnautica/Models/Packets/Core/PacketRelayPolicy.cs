using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;

namespace Nitrox.Server.Subnautica.Models.Packets.Core;

/// <summary>
///     Which packets without a server processor of their own the server passes on from one player to the others. Only
///     those in <see cref="RELAYED" /> are: every other one is dropped, so that a modified game cannot send everyone a
///     packet that only the server should send (a rank, a setting, a story event).
/// </summary>
/// <remarks>
///     Every packet type is in exactly one place: it has a server processor, or it is in <see cref="RELAYED" />, or it
///     is in <see cref="SENT_ONLY_BY_SERVER" />. A test checks this, so a new packet type cannot be forgotten.
/// </remarks>
internal static class PacketRelayPolicy
{
    /// <summary>
    ///     Packets games send to each other through the server, which only passes them on.
    /// </summary>
    public static readonly FrozenSet<Type> RELAYED = new HashSet<Type>
    {
        typeof(CoffeeMachineUse),
        typeof(CyclopsDecoyLaunch),
        typeof(CyclopsDecoyUpdate),
        typeof(CyclopsFireSuppression),
        typeof(ExosuitArmActionPacket),
        typeof(FireExtinguisherUsed),
        typeof(FMODCustomEmitterPacket),
        typeof(FMODCustomLoopingEmitterPacket),
        typeof(FMODStudioEmitterPacket),
        typeof(GrapplingHookMovement),
        typeof(OpenableStateChanged),
        typeof(PlayerCinematicControllerCall),
        typeof(SeamothModulesAction),
        typeof(SeaTreaderChunkPickedUp),
        typeof(StasisSphereHit),
        typeof(StasisSphereShot),
        typeof(Nitrox.Model.Subnautica.Packets.ToggleLights),
        typeof(TorpedoHit),
        typeof(TorpedoShot),
        typeof(TorpedoTargetAcquired)
    }.ToFrozenSet();

    /// <summary>
    ///     Packets only the server sends (or that no game sends any more). One received from a game is dropped.
    /// </summary>
    public static readonly FrozenSet<Type> SENT_ONLY_BY_SERVER = new HashSet<Type>
    {
        typeof(AuroraAndTimeUpdate),
        typeof(BuildingDesyncWarning),
        typeof(BuildingResync),
        typeof(DeathMarkersChanged),
        typeof(DebugStartMapPacket),
        typeof(DeconstructionBegin),
        typeof(Disconnect),
        typeof(DropSimulationOwnership),
        typeof(FastCheatChanged),
        typeof(GameModeChanged),
        typeof(InitialPlayerSync),
        typeof(ItemClaimLost),
        typeof(ItemPosition),
        typeof(JoinQueueInfo),
        typeof(KeepInventoryChanged),
        typeof(MapRoomScanResultChanged),
        typeof(MapRoomScanSnapshot),
        typeof(MapRoomScanTypesChanged),
        typeof(MultiplayerSessionPolicy),
        typeof(MultiplayerSessionReservation),
        typeof(MutePlayer),
        typeof(PermsChanged),
        typeof(PictureFrameImage),
        typeof(PictureFrameRefused),
        typeof(PlayerJoinedMultiplayerSession),
        typeof(PlayerKicked),
        typeof(PlayerTeleported),
        typeof(PlayerTimeCapsuleContent),
        typeof(PlaySunbeamEvent),
        typeof(RocketLaunch),
        typeof(RocketLaunchRefused),
        typeof(RocketResync),
        typeof(ServerStopped),
        typeof(SimulationOwnershipChange),
        typeof(SimulationOwnershipResponse),
        typeof(SleepComplete),
        typeof(SleepStatusUpdate),
        typeof(SpawnEntities),
        typeof(TimeChange)
    }.ToFrozenSet();

    public static bool IsRelayed(Type packetType) => RELAYED.Contains(packetType);

    /// <summary>
    ///     The packet types the server has a processor of its own for.
    /// </summary>
    internal static HashSet<Type> GetTypesWithServerProcessor()
    {
        HashSet<Type> types = [];
        foreach (Type type in typeof(PacketRelayPolicy).Assembly.GetTypes().Where(type => type is { IsClass: true, IsAbstract: false }))
        {
            foreach (Type implemented in type.GetInterfaces().Where(i => i.IsGenericType))
            {
                Type definition = implemented.GetGenericTypeDefinition();
                if (definition != typeof(IAuthPacketProcessor<>) && definition != typeof(IAnonPacketProcessor<>))
                {
                    continue;
                }
                Type packetType = implemented.GetGenericArguments()[0];
                if (packetType != typeof(Packet))
                {
                    types.Add(packetType);
                }
            }
        }
        return types;
    }
}
