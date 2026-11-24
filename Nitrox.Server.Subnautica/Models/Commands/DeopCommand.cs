using System.ComponentModel;
using Nitrox.Model.DataStructures.GameLogic;
using Nitrox.Server.Subnautica.Models.Commands.ArgConverters;
using Nitrox.Server.Subnautica.Models.Commands.Core;
using Nitrox.Server.Subnautica.Models.GameLogic;

namespace Nitrox.Server.Subnautica.Models.Commands;

[RequiresPermission(Perms.ADMIN)]
internal sealed class DeopCommand(IOptions<SubnauticaServerOptions> options) : ICommandHandler<KnownPlayer>
{
    private readonly IOptions<SubnauticaServerOptions> options = options;

    [Description("Takes a given rank (admin or moderator) from a player, also one who is offline: they get the server's default rank")]
    public async Task Execute(ICommandContext context, [Description("Username to take the rank from")] KnownPlayer target)
    {
        Player targetPlayer = target.Player;
        Perms defaultPerms = options.Value.DefaultPlayerPerm;
        if (targetPlayer.IsOnline && targetPlayer.SessionId == context.OriginId)
        {
            await context.ReplyAsync("You can't deop yourself!");
            return;
        }
        if (!PlayerRanks.MayActOn(context.Permissions, PlayerRanks.RankOf(targetPlayer, defaultPerms)))
        {
            await context.ReplyAsync($"You're not allowed to remove admin permissions of {targetPlayer.Name}");
            return;
        }

        PlayerRanks.Revoke(targetPlayer, defaultPerms);
        if (targetPlayer.IsOnline)
        {
            await context.SendAsync(targetPlayer.SessionId, new PermsChanged(defaultPerms)); // Notify so they no longer get admin stuff on client (which would in any way stop working)
            await context.SendAsync(targetPlayer.SessionId, $"You were demoted to {defaultPerms}");
        }
        await context.ReplyAsync($"Updated {targetPlayer.Name}'s permissions to {defaultPerms}");
    }
}
