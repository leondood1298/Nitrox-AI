using Nitrox.Server.Subnautica.Models.GameLogic;
using Nitrox.Server.Subnautica.Models.Packets.Core;

namespace Nitrox.Server.Subnautica.Models.Packets.Processors;

/// <summary>
///     A 3D emitter on an entity: every other game plays it on its copy of the entity, where the event's own distance
///     falloff applies. Loops are remembered for players who join while they play.
/// </summary>
internal sealed class FMODCustomEmitterProcessor(SoundLoops soundLoops) : IAuthPacketProcessor<FMODCustomEmitterPacket>
{
    private readonly SoundLoops soundLoops = soundLoops;

    public async Task Process(AuthProcessorContext context, FMODCustomEmitterPacket packet)
    {
        if (!packet.Play)
        {
            soundLoops.Stopped(packet.Id, packet.AssetPath);
        }
        else if (packet.Looping)
        {
            soundLoops.Started(context.Sender.SessionId, packet.Id, packet.AssetPath, 1f, new FMODCustomEmitterPacket(packet.Id, packet.AssetPath, false, true));
        }
        await context.SendToOthersAsync(packet);
    }
}
