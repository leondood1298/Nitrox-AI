using System.ComponentModel;
using System.Linq;
using System.Net;
using Nitrox.Model.Core;
using Nitrox.Model.DataStructures.GameLogic;
using Nitrox.Server.Subnautica.Models.Administration;
using Nitrox.Server.Subnautica.Models.Commands.ArgConverters;
using Nitrox.Server.Subnautica.Models.Commands.Core;
using Nitrox.Server.Subnautica.Models.Communication;
using Nitrox.Server.Subnautica.Models.GameLogic;
using Nitrox.Server.Subnautica.Services;

namespace Nitrox.Server.Subnautica.Models.Commands;

[RequiresPermission(Perms.MODERATOR)]
internal sealed class BanCommand(PlayerManager playerManager, SessionManager sessionManager, BanService banService, IKickPlayer playerKicker, IOptions<SubnauticaServerOptions> options)
    : ICommandHandler<KnownPlayer, string, TimeSpan>, ICommandHandler<ReservedSession, string, TimeSpan>, ICommandHandler<IPAddress, string, TimeSpan>
{
    [Description("Bans a player who waits in the join queue by their key and IP address, kicking them")]
    public async Task Execute(ICommandContext context,
                              [Description("Queued player to ban")] ReservedSession target,
                              [Description("Ban reason")] string reason = "",
                              [Description("Duration like 30m/12h/7d/2w, omit for permanent")]
                              TimeSpan duration = default)
    {
        if (!PlayerRanks.MayActOn(context.Permissions, playerManager.GetRankOnJoin(target.SessionId)))
        {
            await context.ReplyAsync($"You're not allowed to ban {target.Name}");
            return;
        }
        IPEndPoint? endPoint = sessionManager.GetEndPoint(target.SessionId);
        if (endPoint is null)
        {
            await context.ReplyAsync($"Could not determine the IP address of '{target.Name}'");
            return;
        }

        await BanAsync(context, endPoint.Address, playerManager.GetPendingPublicKey(target.SessionId), target.Name, reason, duration);
    }

    [Description("Bans a player by their key, and by their IP address while online, kicking them; also an offline player")]
    public async Task Execute(ICommandContext context,
                              [Description("Player to ban")] KnownPlayer target,
                              [Description("Ban reason")] string reason = "",
                              [Description("Duration like 30m/12h/7d/2w, omit for permanent")]
                              TimeSpan duration = default)
    {
        Player player = target.Player;
        if (player.IsOnline && player.SessionId == context.OriginId)
        {
            await context.ReplyAsync("You can't ban yourself");
            return;
        }
        if (!PlayerRanks.MayActOn(context.Permissions, PlayerRanks.RankOf(player, options.Value.DefaultPlayerPerm)))
        {
            await context.ReplyAsync($"You're not allowed to ban {player.Name}");
            return;
        }

        IPAddress? address = null;
        if (player.IsOnline)
        {
            address = sessionManager.GetEndPoint(player.SessionId)?.Address;
        }
        else if (playerManager.TryGetSessionByName(player.Name, out SessionId queued) && playerManager.IsReserved(queued))
        {
            // Offline but already waiting to join again
            address = sessionManager.GetEndPoint(queued)?.Address;
        }
        if (address == null && player.IdentityPublicKey == null)
        {
            await context.ReplyAsync($"{player.Name} has no key yet and is not connected: there is nothing to ban them by");
            return;
        }

        await BanAsync(context, address, player.IdentityPublicKey, player.Name, reason, duration);
    }

    [Description("Bans a raw IP address, kicking anyone currently connected from it")]
    public async Task Execute(ICommandContext context,
                              [Description("IP address to ban")] IPAddress target,
                              [Description("Ban reason")] string reason = "",
                              [Description("Duration like 30m/12h/7d/2w, omit for permanent")]
                              TimeSpan duration = default) =>
        await BanAsync(context, target, null, null, reason, duration);

    /// <summary>
    ///     Bans an address and/or a key. Every player currently connected or queued from that address, or with that key, is
    ///     kicked; reconnecting under another name or from another address stays blocked.
    /// </summary>
    private async Task BanAsync(ICommandContext context, IPAddress? ip, string? publicKey, string? label, string? reason, TimeSpan duration)
    {
        Player[] affected = playerManager.GetConnectedPlayers()
                                         .Where(player => (ip != null && ip.Equals(sessionManager.GetEndPoint(player.SessionId)?.Address)) ||
                                                          (publicKey != null && player.IdentityPublicKey == publicKey))
                                         .ToArray();
        if (affected.Any(player => context.OriginId == player.SessionId))
        {
            await context.ReplyAsync("You can't ban yourself");
            return;
        }
        Player? outranking = affected.FirstOrDefault(player => !PlayerRanks.MayActOn(context.Permissions, player.Permissions));
        if (outranking != null)
        {
            await context.ReplyAsync($"You're not allowed to ban {outranking.Name}");
            return;
        }
        if (ip != null && NitroxEnvironment.IsReleaseMode && ip.IsPrivate())
        {
            if (publicKey == null)
            {
                Player? player = affected.FirstOrDefault();
                await context.ReplyAsync(player != null ? $"Player '{player.Name}' connected with a private IP address and can't be banned" : "IP address is private and can't be banned");
                return;
            }
            // A private address is shared by everyone on that network (or the host itself): the key alone is banned
            ip = null;
        }

        label ??= affected.Length == 1 ? affected[0].Name : null;
        reason = reason?.Trim();
        await banService.BanAsync(ip, publicKey, label, reason, context.OriginName, context.Permissions, duration);
        string kickReason = string.IsNullOrEmpty(reason) ? "Banned" : $"Banned: {reason}";
        foreach (Player player in affected.Where(player => player.IsOnline))
        {
            await playerKicker.KickPlayer(player.SessionId, kickReason);
        }
        // Queued players passed the ban check before the ban; they would otherwise join once their turn comes
        foreach (SessionId queued in playerManager.GetReservedSessions().Where(session => (ip != null && ip.Equals(sessionManager.GetEndPoint(session)?.Address)) ||
                                                                                          (publicKey != null && playerManager.GetPendingPublicKey(session) == publicKey)))
        {
            await playerKicker.KickPlayer(queued, kickReason);
        }

        string durationText = duration != TimeSpan.Zero ? $"for {duration}" : "permanently";
        string reasonText = string.IsNullOrEmpty(reason) ? "" : $" - {reason}";
        string byText = ip == null ? "by key" : publicKey == null ? $"({ip})" : $"({ip} and key)";
        string targetText = string.IsNullOrEmpty(label) ? ip?.ToString() ?? "player" : $"{label} {byText}";
        await context.ReplyAsync($"Banned {targetText} {durationText}{reasonText}");
    }
}
