using System.Net;
using Nitrox.Model.DataStructures.GameLogic;

namespace Nitrox.Server.Subnautica.Models.GameLogic;

/// <summary>
///     The rules for a player's rank, in one place: what a player gets on joining, how a rank is given and taken, and who
///     may act on whom.
/// </summary>
internal static class PlayerRanks
{
    /// <summary>
    ///     The rank a joining player gets.
    /// </summary>
    /// <remarks>
    ///     A rank given before (op, deop, promote, /login) is kept only when the joining game proved it holds the key the name
    ///     was already tied to: a name tied anew (first join, forgotten key, new world identity) starts from the default, so
    ///     nobody inherits a rank by taking a free name. Localhost admin is added on top and never kept.
    /// </remarks>
    public static Perms OnJoin(Player player, Perms defaultPerms, bool localhostIsAdmin, IPAddress? address) =>
        OnJoin(player.IdentityVerifiedThisSession, player.GrantedPermissions, defaultPerms, localhostIsAdmin, address);

    /// <param name="keyVerified">The game proved the key the name was already tied to.</param>
    /// <param name="granted">The rank given earlier by a command, if any.</param>
    public static Perms OnJoin(bool keyVerified, Perms? granted, Perms defaultPerms, bool localhostIsAdmin, IPAddress? address)
    {
        Perms rank = keyVerified && granted.HasValue ? granted.Value : defaultPerms;
        if (localhostIsAdmin && IsLoopback(address) && rank < Perms.ADMIN)
        {
            rank = Perms.ADMIN;
        }
        return rank;
    }

    /// <summary>
    ///     Only the loopback address counts as the host: the host's other addresses are also what any player on the same
    ///     network, or arriving through the router, could appear as.
    /// </summary>
    public static bool IsLoopback(IPAddress? address) => address != null && IPAddress.IsLoopback(address.TryExtractMappedIPv4());

    /// <summary>
    ///     Gives <paramref name="rank" /> now and for later joins.
    /// </summary>
    public static void Grant(Player player, Perms rank)
    {
        player.Permissions = rank;
        player.GrantedPermissions = rank;
    }

    /// <summary>
    ///     Drops a given rank: the player has the server's default now and on later joins.
    /// </summary>
    public static void Revoke(Player player, Perms defaultPerms)
    {
        player.GrantedPermissions = null;
        player.Permissions = defaultPerms;
    }

    /// <summary>
    ///     Unties the name from its key. The rank goes with it, since the next game to join may be anyone's.
    /// </summary>
    public static void Untie(Player player)
    {
        player.IdentityPublicKey = null;
        player.GrantedPermissions = null;
    }

    /// <summary>
    ///     Whether someone of rank <paramref name="actor" /> may kick, ban, mute or change the rank of someone of rank
    ///     <paramref name="target" />: only of a lower rank.
    /// </summary>
    public static bool MayActOn(Perms actor, Perms target) => actor > target;

    /// <summary>
    ///     The rank of an offline player, as far as commands that act on them are concerned.
    /// </summary>
    public static Perms RankOf(Player player, Perms defaultPerms) => player.IsOnline ? player.Permissions : player.GrantedPermissions ?? defaultPerms;
}
