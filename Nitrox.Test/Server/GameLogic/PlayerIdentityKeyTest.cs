using Nitrox.Server.Subnautica.Models.GameLogic;
using Nitrox.Test.Server.Helper;

namespace Nitrox.Test.Server.GameLogic;

[TestClass]
public sealed class PlayerIdentityKeyTest
{
    [TestMethod]
    public void ANameWithoutKeyAcceptsAnyGame()
    {
        PlayerIdentityKey.Matches(null, null).Should().BeTrue();
        PlayerIdentityKey.Matches(null, ConnectedPlayers.PublicKeyOf("a")).Should().BeTrue();
    }

    [TestMethod]
    public void ANameWithKeyAcceptsOnlyThatKey()
    {
        string stored = ConnectedPlayers.PublicKeyOf("a");

        PlayerIdentityKey.Matches(stored, ConnectedPlayers.PublicKeyOf("a")).Should().BeTrue();
        PlayerIdentityKey.Matches(stored, ConnectedPlayers.PublicKeyOf("b")).Should().BeFalse();
        PlayerIdentityKey.Matches(stored, null).Should().BeFalse();
    }
}
