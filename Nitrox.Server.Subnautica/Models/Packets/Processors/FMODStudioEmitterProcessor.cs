using Nitrox.Server.Subnautica.Models.GameLogic;
using Nitrox.Server.Subnautica.Models.Packets.Core;

namespace Nitrox.Server.Subnautica.Models.Packets.Processors;

/// <inheritdoc cref="FMODCustomEmitterProcessor" />
internal sealed class FMODStudioEmitterProcessor(SoundLoops soundLoops) : IAuthPacketProcessor<FMODStudioEmitterPacket>
{
    private readonly SoundLoops soundLoops = soundLoops;

    public async Task Process(AuthProcessorContext context, FMODStudioEmitterPacket packet)
    {
        if (!packet.Play)
        {
            soundLoops.Stopped(packet.Id, packet.AssetPath);
        }
        else if (packet.Looping)
        {
            soundLoops.Started(context.Sender.SessionId, packet.Id, packet.AssetPath, 1f, new FMODStudioEmitterPacket(packet.Id, packet.AssetPath, false, true, true));
        }
        await context.SendToOthersAsync(packet);
    }
}
