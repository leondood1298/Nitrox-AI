using System.ComponentModel;
using Nitrox.Model.DataStructures.GameLogic;
using Nitrox.Server.Subnautica.Models.Administration;
using Nitrox.Server.Subnautica.Models.Commands.ArgConverters;
using Nitrox.Server.Subnautica.Models.Commands.Core;

namespace Nitrox.Server.Subnautica.Models.Commands;

[RequiresPermission(Perms.MODERATOR)]
internal sealed class KickCommand(IKickPlayer playerKicker, IOptions<SubnauticaServerOptions> options) : ICommandHandler<Player, string>, ICommandHandler<ReservedSession, string>
{
    private readonly IKickPlayer playerKicker = playerKicker;
    private readonly IOptions<SubnauticaServerOptions> options = options;

    [Description("Kicks a player who waits in the join queue")]
    public async Task Execute(ICommandContext context, ReservedSession queuedPlayer, string reason = "")
    {
        // Not a player yet: they would get the default permissions on joining
        if (context.Origin == CommandOrigin.PLAYER && options.Value.DefaultPlayerPerm >= context.Permissions)
        {
            await context.ReplyAsync($"You're not allowed to kick {queuedPlayer.Name}");
            return;
        }
        if (!await playerKicker.KickPlayer(queuedPlayer.SessionId, reason))
        {
            await context.ReplyAsync($"Failed to kick '{queuedPlayer.Name}'");
        }
    }

    [Description("Kicks a player from the server")]
    public async Task Execute(ICommandContext context, Player playerToKick, string reason = "")
    {
        if (context.OriginId == playerToKick.SessionId)
        {
            await context.ReplyAsync("You can't kick yourself");
            return;
        }

        switch (context.Origin)
        {
            case CommandOrigin.PLAYER when playerToKick.Permissions >= context.Permissions:
                await context.ReplyAsync($"You're not allowed to kick {playerToKick.Name}");
                break;
            default:
                if (!await playerKicker.KickPlayer(playerToKick.SessionId, reason))
                {
                    await context.ReplyAsync($"Failed to kick '{playerToKick.Name}'");
                }
                break;
        }
    }
}
