using NitroxClient.GameLogic.PlayerLogic.PlayerPreferences;

namespace Nitrox.Test.Client.GameLogic;

/// <summary>
///     The key sent to a server comes from the installation's secret and the world's identity only, never from the
///     address the server is reached through.
/// </summary>
[TestClass]
public sealed class PlayerKeysTest
{
    private static readonly byte[] secret = Enumerable.Range(0, 32).Select(i => (byte)i).ToArray();
    private const string WORLD = "6F1E0C2B9A8D4E7F00112233445566AA";

    [TestMethod]
    public void TheSameWorldGetsTheSameKey()
    {
        PlayerKeys.Derive(secret, WORLD).Should().Be(PlayerKeys.Derive(secret, WORLD));
    }

    [TestMethod]
    public void WorldsGetDifferentKeys()
    {
        PlayerKeys.Derive(secret, WORLD).Should().NotBe(PlayerKeys.Derive(secret, "0123456789ABCDEF0123456789ABCDEF"));
    }

    [TestMethod]
    public void InstallationsGetDifferentKeys()
    {
        byte[] otherSecret = secret.Reverse().ToArray();

        PlayerKeys.Derive(otherSecret, WORLD).Should().NotBe(PlayerKeys.Derive(secret, WORLD));
    }
}
