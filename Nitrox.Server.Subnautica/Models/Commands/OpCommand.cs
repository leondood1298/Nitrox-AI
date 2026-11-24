using System.ComponentModel;
using Nitrox.Model.DataStructures.GameLogic;
using Nitrox.Server.Subnautica.Models.Commands.ArgConverters;
using Nitrox.Server.Subnautica.Models.Commands.Core;
using Nitrox.Server.Subnautica.Models.GameLogic;

namespace Nitrox.Server.Subnautica.Models.Commands;

[RequiresPermission(Perms.ADMIN)]
internal sealed class OpCommand(IOptions<SubnauticaServerOptions> options) : ICommandHandler<KnownPlayer>
{
    private const Perms OP_PERMS = Perms.ADMIN;
    private readonly IOptions<SubnauticaServerOptions> options = options;

    [Description("Makes a player admin, also one who is offline; kept when they join again")]
    public async Task Execute(ICommandContext context, [Description("The players name to make an admin")] KnownPlayer target)
    {
        Player targetPlayer = target.Player;
        Perms currentRank = PlayerRanks.RankOf(targetPlayer, options.Value.DefaultPlayerPerm);
        // Making someone of a higher rank "admin" would demote them
        if (currentRank >= OP_PERMS)
        {
            await context.ReplyAsync($"{targetPlayer.Name} is already {currentRank}");
            return;
        }

        PlayerRanks.Grant(targetPlayer, OP_PERMS);
        if (targetPlayer.IsOnline)
        {
            // We need to notify this player that he can show all the admin-related stuff
            await context.SendAsync(targetPlayer.SessionId, new PermsChanged(OP_PERMS));
            await context.SendAsync(targetPlayer.SessionId, $"You were promoted to {OP_PERMS}");
        }
        await context.ReplyAsync($"Updated {targetPlayer.Name}\'s permissions to {OP_PERMS}");
    }
}
