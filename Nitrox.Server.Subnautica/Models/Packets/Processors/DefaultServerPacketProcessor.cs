using System.Collections.Generic;
using Nitrox.Server.Subnautica.Models.Packets.Core;

namespace Nitrox.Server.Subnautica.Models.Packets.Processors;

/// <summary>
///     Handles a packet the server has no processor of its own for: passes it on to the other players if it is one that
///     games send each other (<see cref="PacketRelayPolicy.RELAYED" />), and drops it otherwise.
/// </summary>
internal sealed class DefaultServerPacketProcessor(ILogger<DefaultServerPacketProcessor> logger) : IAuthPacketProcessor<Packet>
{
    private readonly ILogger<DefaultServerPacketProcessor> logger = logger;

    private readonly HashSet<Type> loggingPacketBlackList =
    [
        typeof(AnimationChangeEvent),
        typeof(PlayerMovement),
        typeof(ItemPosition),
        typeof(PlayerStats),
        typeof(StoryGoalExecuted),
        typeof(FMODAssetPacket),
        typeof(FMODCustomEmitterPacket),
        typeof(FMODCustomLoopingEmitterPacket),
        typeof(FMODStudioEmitterPacket),
        typeof(PlayerCinematicControllerCall),
        typeof(TorpedoShot),
        typeof(TorpedoHit),
        typeof(TorpedoTargetAcquired),
        typeof(StasisSphereShot),
        typeof(StasisSphereHit),
        typeof(SeaTreaderChunkPickedUp),
        typeof(Nitrox.Model.Subnautica.Packets.ToggleLights)
    ];

    public async Task Process(AuthProcessorContext context, Packet packet)
    {
        Type packetType = packet.GetType();
        if (!loggingPacketBlackList.Contains(packetType))
        {
            logger.ZLogDebug($"Using default packet processor for: {packet} and player #{context.Sender.SessionId}");
        }
        if (!PacketRelayPolicy.IsRelayed(packetType))
        {
            logger.ZLogErrorOnce($"Player {context.Sender.Name} #{context.Sender.SessionId} sent a {packetType.Name}, which only the server sends. It's likely that the said player is using a modified version of Nitrox and action could be taken accordingly.");
            return;
        }

        await context.SendToOthersAsync(packet);
    }
}
