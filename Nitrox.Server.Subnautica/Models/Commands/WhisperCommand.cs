using System.ComponentModel;
using Nitrox.Model.DataStructures.GameLogic;
using Nitrox.Server.Subnautica.Models.Administration;
using Nitrox.Server.Subnautica.Models.Commands.Core;
using Nitrox.Server.Subnautica.Models.Packets.Processors;

namespace Nitrox.Server.Subnautica.Models.Commands;

[Alias("w", "msg", "m")]
[RequiresPermission(Perms.PLAYER)]
internal sealed class WhisperCommand(ChatRateLimiter rateLimiter) : ICommandHandler<Player, string>
{
    private readonly ChatRateLimiter rateLimiter = rateLimiter;

    [Description("Sends a private message to a player (the message is the rest of the line)")]
    public async Task Execute(ICommandContext context, [Description("The players name to message")] Player targetPlayer, [Description("The message to send")] string message)
    {
        // Whispers are chat too: a mute and the chat rate apply to them
        if (context is PlayerToServerCommandContext { Player: { } sender })
        {
            if (sender.IsMuted)
            {
                await context.ReplyAsync("You're currently muted");
                return;
            }
            if (!rateLimiter.TryTake(sender.SessionId))
            {
                await context.ReplyAsync("You're sending messages too fast. Slow down.");
                return;
            }
        }
        if (message.Length > ChatMessageProcessor.MAX_MESSAGE_LENGTH)
        {
            message = message[..ChatMessageProcessor.MAX_MESSAGE_LENGTH];
        }
        await context.SendAsync(targetPlayer.SessionId, new ChatMessage(context.OriginId, $"[{context.OriginName} -> YOU]: {message}"));
    }
}
