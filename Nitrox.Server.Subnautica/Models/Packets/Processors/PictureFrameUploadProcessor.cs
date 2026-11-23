using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;
using Nitrox.Server.Subnautica.Models.GameLogic;
using Nitrox.Server.Subnautica.Models.GameLogic.Entities;
using Nitrox.Server.Subnautica.Models.GameLogic.PictureFrames;
using Nitrox.Server.Subnautica.Models.Packets.Core;

namespace Nitrox.Server.Subnautica.Models.Packets.Processors;

/// <summary>
///     Keeps the picture a player put into a frame and tells the players who can see the frame which picture it shows;
///     they ask for the picture itself once they come close to it. A picture that does not fit is refused with the reason.
/// </summary>
internal sealed class PictureFrameUploadProcessor(PictureFrameStore store, EntityRegistry entityRegistry, PlayerManager playerManager, ILogger<PictureFrameUploadProcessor> logger)
    : IAuthPacketProcessor<PictureFrameUpload>
{
    private readonly PictureFrameStore store = store;
    private readonly EntityRegistry entityRegistry = entityRegistry;
    private readonly PlayerManager playerManager = playerManager;
    private readonly ILogger<PictureFrameUploadProcessor> logger = logger;

    public async Task Process(AuthProcessorContext context, PictureFrameUpload packet)
    {
        if (entityRegistry.TryGetEntityById(packet.FrameId, out Entity frame) && !context.Sender.CanSee(frame))
        {
            await context.ReplyAsync(new PictureFrameRefused(packet.FrameId, PictureFrameRefusal.NO_FRAME, 0));
            return;
        }
        if (store.TryStore(packet.FrameId, packet.Picture ?? [], out string hash, out long limit) is { } refusal)
        {
            logger.ZLogInformation($"Refused a picture of {packet.Picture?.Length ?? 0} bytes from [{context.Sender.Name}] for frame {packet.FrameId}: {refusal}");
            await context.ReplyAsync(new PictureFrameRefused(packet.FrameId, refusal, limit));
            return;
        }

        EntityMetadataUpdate update = new(packet.FrameId, new PictureFrameMetadata(hash));
        foreach (Player player in playerManager.GetConnectedPlayers())
        {
            if (player != context.Sender && player.CanSee(frame))
            {
                await context.SendAsync(update, player.SessionId);
            }
        }
    }
}
