using System.Collections.Generic;
using System.Linq;
using System.Net;

namespace Nitrox.Server.Subnautica.Models.Communication;

/// <summary>
///     How many connections that have not joined as a player yet the server keeps open.
/// </summary>
/// <remarks>
///     A friend's game needs one such connection while it joins; a handful from one address covers retries. Players who
///     have joined do not count, so a full server of friends is never refused here.
/// </remarks>
internal static class PendingConnectionLimit
{
    public const int MAX_PENDING = 16;
    public const int MAX_PENDING_PER_ADDRESS = 4;

    public static bool Allows(IEnumerable<IPAddress> pendingAddresses, IPAddress newAddress)
    {
        IPAddress[] pending = pendingAddresses.ToArray();
        return pending.Length < MAX_PENDING && pending.Count(address => Same(address, newAddress)) < MAX_PENDING_PER_ADDRESS;
    }

    private static bool Same(IPAddress a, IPAddress b)
    {
        // The same machine arrives as 1.2.3.4 or ::ffff:1.2.3.4 depending on the socket
        IPAddress left = a.IsIPv4MappedToIPv6 ? a.MapToIPv4() : a;
        IPAddress right = b.IsIPv4MappedToIPv6 ? b.MapToIPv4() : b;
        return left.Equals(right);
    }
}
