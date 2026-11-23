using NitroxClient.GameLogic.PlayerLogic.PlayerPreferences;

namespace Nitrox.Test.Client.GameLogic;

/// <summary>
///     The key sent to a server comes from the installation's secret and that server's address only.
/// </summary>
[TestClass]
public sealed class PlayerKeysTest
{
    private static readonly byte[] secret = Enumerable.Range(0, 32).Select(i => (byte)i).ToArray();

    [TestMethod]
    public void TheSameServerGetsTheSameKey()
    {
        PlayerKeys.Derive(secret, "192.168.0.2").Should().Be(PlayerKeys.Derive(secret, " 192.168.0.2 "));
    }

    [TestMethod]
    public void ServersGetDifferentKeys()
    {
        PlayerKeys.Derive(secret, "192.168.0.2").Should().NotBe(PlayerKeys.Derive(secret, "10.0.0.5"));
    }

    [TestMethod]
    public void InstallationsGetDifferentKeys()
    {
        byte[] otherSecret = secret.Reverse().ToArray();

        PlayerKeys.Derive(otherSecret, "192.168.0.2").Should().NotBe(PlayerKeys.Derive(secret, "192.168.0.2"));
    }
}
