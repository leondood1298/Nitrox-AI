using System.ComponentModel;
using Nitrox.Model.DataStructures.GameLogic;
using Nitrox.Server.Subnautica.Models.Commands.ArgConverters;
using Nitrox.Server.Subnautica.Models.Commands.Core;
using Nitrox.Server.Subnautica.Models.GameLogic;

namespace Nitrox.Server.Subnautica.Models.Commands;

[RequiresPermission(Perms.MODERATOR)]
internal sealed class PromoteCommand(IOptions<SubnauticaServerOptions> options) : ICommandHandler<KnownPlayer, Perms>
{
    private readonly IOptions<SubnauticaServerOptions> options = options;

    [Description("Sets a player's rank, also one who is offline; kept when they join again")]
    public async Task Execute(ICommandContext context, [Description("The username to change the permissions of")] KnownPlayer target, [Description("Permission level")] Perms newPerms)
    {
        Player targetPlayer = target.Player;
        if (targetPlayer.IsOnline && context.OriginId == targetPlayer.SessionId)
        {
            await context.ReplyAsync("You can't promote yourself");
            return;
        }
        if (context.Permissions < newPerms)
        {
            await context.ReplyAsync($"Your permissions ({context.Permissions}) must be at least the perms you want to assign ({newPerms})");
            return;
        }
        // Like deop: a player of the same rank is out of reach, so moderators cannot strip each other.
        if (!PlayerRanks.MayActOn(context.Permissions, PlayerRanks.RankOf(targetPlayer, options.Value.DefaultPlayerPerm)))
        {
            await context.ReplyAsync($"You're not allowed to update {targetPlayer.Name}\'s permissions");
            return;
        }

        PlayerRanks.Grant(targetPlayer, newPerms);
        if (targetPlayer.IsOnline)
        {
            await context.SendAsync(targetPlayer.SessionId, new PermsChanged(newPerms));
            await context.SendAsync(targetPlayer.SessionId, $"You've been promoted to {newPerms}");
        }
        await context.ReplyAsync($"Updated {targetPlayer.Name}\'s permissions to {newPerms}");
    }
}
