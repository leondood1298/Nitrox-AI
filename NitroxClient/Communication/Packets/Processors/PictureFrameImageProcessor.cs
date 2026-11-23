using Nitrox.Model.Subnautica.Packets;
using NitroxClient.Communication.Packets.Processors.Core;
using NitroxClient.GameLogic;

namespace NitroxClient.Communication.Packets.Processors;

internal sealed class PictureFrameImageProcessor(PictureFrames pictureFrames) : IClientPacketProcessor<PictureFrameImage>
{
    private readonly PictureFrames pictureFrames = pictureFrames;

    public Task Process(ClientProcessorContext context, PictureFrameImage packet)
    {
        pictureFrames.OnPictureReceived(packet.PictureHash, packet.Picture);
        return Task.CompletedTask;
    }
}
