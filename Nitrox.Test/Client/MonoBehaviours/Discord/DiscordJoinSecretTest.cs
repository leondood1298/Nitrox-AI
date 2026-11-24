using NitroxClient.MonoBehaviours.Discord;

namespace Nitrox.Test.Client.MonoBehaviours.Discord;

[TestClass]
public sealed class DiscordJoinSecretTest
{
    [TestMethod]
    public void AnAddressAndPortAreReadFromTheSecret()
    {
        DiscordJoinSecret.TryParse("203.0.113.7:11000", out string address, out int port).Should().BeTrue();
        address.Should().Be("203.0.113.7");
        port.Should().Be(11000);
    }

    [TestMethod]
    public void AnIpv6AddressKeepsItsOwnColons()
    {
        DiscordJoinSecret.TryParse("2001:db8::1:11000", out string address, out int port).Should().BeTrue();
        address.Should().Be("2001:db8::1");
        port.Should().Be(11000);
    }

    [TestMethod]
    public void ASecretWithoutAUsablePortIsRefused()
    {
        DiscordJoinSecret.TryParse("203.0.113.7:port", out _, out _).Should().BeFalse();
        DiscordJoinSecret.TryParse("203.0.113.7:70000", out _, out _).Should().BeFalse();
        DiscordJoinSecret.TryParse("203.0.113.7:", out _, out _).Should().BeFalse();
        DiscordJoinSecret.TryParse("11000", out _, out _).Should().BeFalse();
        DiscordJoinSecret.TryParse("", out _, out _).Should().BeFalse();
    }
}
