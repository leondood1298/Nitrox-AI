using Nitrox.Model.Core;
using Nitrox.Server.Subnautica.Models.Administration;
using Nitrox.Server.Subnautica.Models.Packets.Core;

namespace Nitrox.Server.Subnautica.Models.Packets.Processors;

internal sealed class ChatMessageProcessor(ChatRateLimiter rateLimiter, ILogger<ChatMessageProcessor> logger) : IAuthPacketProcessor<ChatMessage>
{
    /// <summary>
    ///     Longest chat message passed on, in characters; longer ones are cut.
    /// </summary>
    public const int MAX_MESSAGE_LENGTH = 500;

    private readonly ChatRateLimiter rateLimiter = rateLimiter;
    private readonly ILogger<ChatMessageProcessor> logger = logger;

    public async Task Process(AuthProcessorContext context, ChatMessage packet)
    {
        if (context.Sender.PlayerContext.IsMuted)
        {
            await context.ReplyAsync(new ChatMessage(SessionId.SERVER_ID, "You're currently muted"));
            return;
        }
        if (string.IsNullOrWhiteSpace(packet.Text))
        {
            return;
        }
        if (!rateLimiter.TryTake(context.Sender.SessionId))
        {
            await context.ReplyAsync(new ChatMessage(SessionId.SERVER_ID, "You're sending messages too fast. Slow down."));
            return;
        }
        string text = packet.Text.Length > MAX_MESSAGE_LENGTH ? packet.Text[..MAX_MESSAGE_LENGTH] : packet.Text;
        logger.ZLogInformation($"<{context.Sender.Name}>: {text}");
        // Rebuilt with the real sender: the game's packet names its sender itself, so a modified game could pass as the
        // server or as another player
        await context.SendToAllAsync(new ChatMessage(context.Sender.SessionId, text));
    }
}
