using System.ComponentModel;
using Nitrox.Model.DataStructures.GameLogic;
using Nitrox.Server.Subnautica.Models.Commands.ArgConverters;
using Nitrox.Server.Subnautica.Models.Commands.Core;
using Nitrox.Server.Subnautica.Models.GameLogic;

namespace Nitrox.Server.Subnautica.Models.Commands;

[RequiresPermission(Perms.MODERATOR)]
internal sealed class UnmuteCommand(IOptions<SubnauticaServerOptions> options) : ICommandHandler<KnownPlayer>
{
    private readonly IOptions<SubnauticaServerOptions> options = options;

    [Description("Removes a mute from a player, also one who is offline")]
    public async Task Execute(ICommandContext context, [Description("Player to unmute")] KnownPlayer target)
    {
        Player targetPlayer = target.Player;
        if (targetPlayer.IsOnline && context.OriginId == targetPlayer.SessionId)
        {
            await context.ReplyAsync("You can't unmute yourself");
            return;
        }
        if (!PlayerRanks.MayActOn(context.Permissions, PlayerRanks.RankOf(targetPlayer, options.Value.DefaultPlayerPerm)))
        {
            await context.ReplyAsync($"You're not allowed to unmute {targetPlayer.Name}");
            return;
        }
        if (!targetPlayer.IsMuted)
        {
            await context.ReplyAsync($"{targetPlayer.Name} is already unmuted");
            if (targetPlayer.IsOnline)
            {
                await context.ReplyAsync(new MutePlayer(targetPlayer.SessionId, false));
            }
            return;
        }

        PlayerMutes.Set(targetPlayer, false);
        if (targetPlayer.IsOnline)
        {
            await context.SendToAllAsync(new MutePlayer(targetPlayer.SessionId, false));
            await context.SendAsync(targetPlayer.SessionId, "You're no longer muted");
        }
        await context.ReplyAsync($"Unmuted {targetPlayer.Name}");
    }
}
