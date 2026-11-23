using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Server.Subnautica.Models.GameLogic.Entities;
using Nitrox.Server.Subnautica.Models.GameLogic.PictureFrames;
using Nitrox.Server.Subnautica.Models.Packets.Core;

namespace Nitrox.Server.Subnautica.Models.Packets.Processors;

/// <summary>
///     Sends a picture to a player who came close to a frame that shows it. Only a picture a frame shows, and only to a
///     player who can see that frame.
/// </summary>
internal sealed class PictureFrameImageRequestProcessor(PictureFrameStore store, EntityRegistry entityRegistry) : IAuthPacketProcessor<PictureFrameImageRequest>
{
    private readonly PictureFrameStore store = store;
    private readonly EntityRegistry entityRegistry = entityRegistry;

    public async Task Process(AuthProcessorContext context, PictureFrameImageRequest packet)
    {
        if (!entityRegistry.TryGetEntityById(packet.FrameId, out Entity frame) || !context.Sender.CanSee(frame))
        {
            return;
        }
        if (store.GetPictureOf(packet.FrameId, packet.PictureHash) is { } picture)
        {
            await context.ReplyAsync(new PictureFrameImage(packet.PictureHash, picture));
        }
    }
}
