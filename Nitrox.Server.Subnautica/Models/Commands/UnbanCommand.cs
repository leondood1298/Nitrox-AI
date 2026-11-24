using System.ComponentModel;
using System.Linq;
using System.Net;
using Nitrox.Model.DataStructures.GameLogic;
using Nitrox.Server.Subnautica.Models.Commands.Core;
using Nitrox.Server.Subnautica.Services;

namespace Nitrox.Server.Subnautica.Models.Commands;

/// <remarks>
///     A ban can be lifted only by the rank that issued it or higher, so a moderator cannot undo an admin's ban.
/// </remarks>
[RequiresPermission(Perms.MODERATOR)]
internal sealed class UnbanCommand(BanService banService) : ICommandHandler<string>
{
    /// <remarks>
    ///     One text argument for both forms: a handler taking an address would lose to this one, which matches any text.
    /// </remarks>
    [Description("Removes a ban by IP address or by player name (see banlist)")]
    public async Task Execute(ICommandContext context, [Description("Banned IP address or player name")] string target)
    {
        target = target.Trim();
        Func<BanService.BanEntry, bool> matches = IPAddress.TryParse(target, out IPAddress? address) && !int.TryParse(target, out _)
            ? entry => address.Equals(entry.IP)
            : entry => string.Equals(entry.PlayerName, target, StringComparison.OrdinalIgnoreCase);

        BanService.UnbanResult result = await banService.UnbanAsync(matches, context.Permissions);
        if (result.Removed == 0 && result.Refused.Count == 0)
        {
            await context.ReplyAsync($"No active ban found for {target}");
            return;
        }
        if (result.Refused.Count > 0)
        {
            string ranks = string.Join(", ", result.Refused.Select(entry => (entry.IssuerRank ?? BanService.UNKNOWN_ISSUER_RANK).ToString()).Distinct());
            await context.ReplyAsync($"Only {ranks} or higher can lift the ban on {target}");
        }
        if (result.Removed > 0)
        {
            await context.ReplyAsync($"Removed ban for {target}");
        }
    }
}
