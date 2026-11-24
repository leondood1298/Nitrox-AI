using System.ComponentModel;
using Nitrox.Model.DataStructures.GameLogic;
using Nitrox.Server.Subnautica.Models.Commands.ArgConverters;
using Nitrox.Server.Subnautica.Models.Commands.Core;
using Nitrox.Server.Subnautica.Models.GameLogic;

namespace Nitrox.Server.Subnautica.Models.Commands;

[RequiresPermission(Perms.MODERATOR)]
internal sealed class MuteCommand(IOptions<SubnauticaServerOptions> options) : ICommandHandler<KnownPlayer>
{
    private readonly IOptions<SubnauticaServerOptions> options = options;

    [Description("Stops a player's chat and whispers, also one who is offline; kept when they join again")]
    public async Task Execute(ICommandContext context, [Description("Player to mute")] KnownPlayer target)
    {
        Player targetPlayer = target.Player;
        if (targetPlayer.IsOnline && context.OriginId == targetPlayer.SessionId)
        {
            await context.ReplyAsync("You can't mute yourself");
            return;
        }
        if (!PlayerRanks.MayActOn(context.Permissions, PlayerRanks.RankOf(targetPlayer, options.Value.DefaultPlayerPerm)))
        {
            await context.ReplyAsync($"You're not allowed to mute {targetPlayer.Name}");
            return;
        }

        if (targetPlayer.IsMuted)
        {
            await context.ReplyAsync($"{targetPlayer.Name} is already muted");
            if (targetPlayer.IsOnline)
            {
                // Send state anyway in case it got desynced.
                await context.ReplyAsync(new MutePlayer(targetPlayer.SessionId, true));
            }
            return;
        }

        PlayerMutes.Set(targetPlayer, true);
        if (targetPlayer.IsOnline)
        {
            await context.SendToAllAsync(new MutePlayer(targetPlayer.SessionId, true));
            await context.SendAsync(targetPlayer.SessionId, "You're now muted");
        }
        await context.ReplyAsync($"Muted {targetPlayer.Name}");
    }
}
