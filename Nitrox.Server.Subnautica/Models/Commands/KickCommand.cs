using System.ComponentModel;
using Nitrox.Model.DataStructures.GameLogic;
using Nitrox.Server.Subnautica.Models.Administration;
using Nitrox.Server.Subnautica.Models.Commands.ArgConverters;
using Nitrox.Server.Subnautica.Models.Commands.Core;
using Nitrox.Server.Subnautica.Models.GameLogic;

namespace Nitrox.Server.Subnautica.Models.Commands;

[RequiresPermission(Perms.MODERATOR)]
internal sealed class KickCommand(IKickPlayer playerKicker, PlayerManager playerManager) : ICommandHandler<Player, string>, ICommandHandler<ReservedSession, string>
{
    private readonly IKickPlayer playerKicker = playerKicker;
    private readonly PlayerManager playerManager = playerManager;

    [Description("Kicks a player who waits in the join queue")]
    public async Task Execute(ICommandContext context, ReservedSession queuedPlayer, string reason = "")
    {
        // Not a player yet: judged by the rank they will have on joining
        if (!PlayerRanks.MayActOn(context.Permissions, playerManager.GetRankOnJoin(queuedPlayer.SessionId)))
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

        if (!PlayerRanks.MayActOn(context.Permissions, playerToKick.Permissions))
        {
            await context.ReplyAsync($"You're not allowed to kick {playerToKick.Name}");
            return;
        }
        if (!await playerKicker.KickPlayer(playerToKick.SessionId, reason))
        {
            await context.ReplyAsync($"Failed to kick '{playerToKick.Name}'");
        }
    }
}
