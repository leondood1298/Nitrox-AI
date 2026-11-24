using System.ComponentModel;
using Nitrox.Model.DataStructures.GameLogic;
using Nitrox.Server.Subnautica.Models.Commands.Core;
using Nitrox.Server.Subnautica.Models.GameLogic;

namespace Nitrox.Server.Subnautica.Models.Commands;

/// <summary>
///     A name is tied to the game installation it first joined from. A player who moved to another computer, reset their
///     game or joins through another address is refused until an admin unties the name.
/// </summary>
[RequiresPermission(Perms.ADMIN)]
internal sealed class ForgetKeyCommand(PlayerManager playerManager, IOptions<SubnauticaServerOptions> options) : ICommandHandler<string>
{
    private readonly PlayerManager playerManager = playerManager;
    private readonly IOptions<SubnauticaServerOptions> options = options;

    [Description("Unties a player name from the game it first joined from, so the next game joining with that name keeps it (a given rank is dropped)")]
    public async Task Execute(ICommandContext context, [Description("The player name")] string playerName)
    {
        if (!playerManager.TryGetKnownPlayerByName(playerName, out Player? player))
        {
            await context.ReplyAsync($"No player named {playerName} has joined this server");
            return;
        }
        // Untying drops the player's given rank, so it is a demotion
        if (!PlayerRanks.MayActOn(context.Permissions, PlayerRanks.RankOf(player, options.Value.DefaultPlayerPerm)))
        {
            await context.ReplyAsync($"You're not allowed to untie {playerName}");
            return;
        }
        playerManager.ForgetIdentityKey(playerName);
        await context.ReplyAsync($"{playerName} is no longer tied to a game; the next game to join with that name keeps it, with the default rank");
    }
}
