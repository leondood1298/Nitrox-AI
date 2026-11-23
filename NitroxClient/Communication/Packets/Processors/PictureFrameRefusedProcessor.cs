using Nitrox.Model.Subnautica.Packets;
using NitroxClient.Communication.Packets.Processors.Core;
using NitroxClient.GameLogic;

namespace NitroxClient.Communication.Packets.Processors;

internal sealed class PictureFrameRefusedProcessor(PictureFrames pictureFrames) : IClientPacketProcessor<PictureFrameRefused>
{
    private readonly PictureFrames pictureFrames = pictureFrames;

    public Task Process(ClientProcessorContext context, PictureFrameRefused packet)
    {
        pictureFrames.OnPictureRefused(packet.FrameId, packet.Reason, packet.Limit);
        return Task.CompletedTask;
    }
}
