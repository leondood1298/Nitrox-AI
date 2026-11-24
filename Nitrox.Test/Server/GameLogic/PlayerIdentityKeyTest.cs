using Nitrox.Server.Subnautica.Models.GameLogic;

namespace Nitrox.Test.Server.GameLogic;

[TestClass]
public sealed class PlayerIdentityKeyTest
{
    [TestMethod]
    public void TheSaveKeepsAHashNotTheKey()
    {
        string hash = PlayerIdentityKey.Hash("secret")!;

        hash.Should().NotContain("secret");
        PlayerIdentityKey.Hash("secret").Should().Be(hash);
        PlayerIdentityKey.Hash("other").Should().NotBe(hash);
    }

    [TestMethod]
    public void NoKeySentHasNoHash()
    {
        PlayerIdentityKey.Hash(null).Should().BeNull();
        PlayerIdentityKey.Hash("").Should().BeNull();
    }

    [TestMethod]
    public void ANameWithoutKeyAcceptsAnyGame()
    {
        PlayerIdentityKey.Matches(null, null).Should().BeTrue();
        PlayerIdentityKey.Matches(null, PlayerIdentityKey.Hash("a")).Should().BeTrue();
    }

    [TestMethod]
    public void ANameWithKeyAcceptsOnlyThatKey()
    {
        string stored = PlayerIdentityKey.Hash("a")!;

        PlayerIdentityKey.Matches(stored, PlayerIdentityKey.Hash("a")).Should().BeTrue();
        PlayerIdentityKey.Matches(stored, PlayerIdentityKey.Hash("b")).Should().BeFalse();
        PlayerIdentityKey.Matches(stored, null).Should().BeFalse();
    }
}
