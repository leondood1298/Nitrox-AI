using System.Net;
using Nitrox.Server.Subnautica.Models.Communication;

namespace Nitrox.Test.Server.Communication;

[TestClass]
public sealed class PendingConnectionLimitTest
{
    private static readonly IPAddress stranger = IPAddress.Parse("203.0.113.7");

    [TestMethod]
    public void FriendJoiningIsAllowed()
    {
        PendingConnectionLimit.Allows([], IPAddress.Parse("198.51.100.2")).Should().BeTrue();
    }

    [TestMethod]
    public void OneAddressCannotHoldMoreThanItsShare()
    {
        IPAddress[] pending = Enumerable.Repeat(stranger, PendingConnectionLimit.MAX_PENDING_PER_ADDRESS).ToArray();

        PendingConnectionLimit.Allows(pending, stranger.MapToIPv6()).Should().BeFalse("the same machine may arrive as an IPv4-mapped IPv6 address");
        PendingConnectionLimit.Allows(pending, IPAddress.Parse("198.51.100.2")).Should().BeTrue();
    }

    [TestMethod]
    public void ManyAddressesTogetherAreCapped()
    {
        IPAddress[] pending = Enumerable.Range(1, PendingConnectionLimit.MAX_PENDING).Select(i => IPAddress.Parse($"203.0.113.{i}")).ToArray();

        PendingConnectionLimit.Allows(pending, IPAddress.Parse("198.51.100.2")).Should().BeFalse();
    }
}
