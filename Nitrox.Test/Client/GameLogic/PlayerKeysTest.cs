using Nitrox.Model.MultiplayerSession;
using NitroxClient.GameLogic.PlayerLogic.PlayerPreferences;

namespace Nitrox.Test.Client.GameLogic;

/// <summary>
///     The game proves its key by signing the server's challenge with the world's identity; the key itself never leaves it.
/// </summary>
[TestClass]
public sealed class PlayerKeysTest
{
    private const string WORLD = "6F1E0C2B9A8D4E7F00112233445566AA";
    private static readonly Lazy<string> storedKey = new(() => PlayerKeys.Prove("", WORLD, "x").PrivateKeyXml);

    [TestMethod]
    public void TheServerAcceptsTheGamesSignature()
    {
        PlayerKeys.Proof proof = PlayerKeys.Prove(storedKey.Value, WORLD, "challenge-1").Proof;

        PlayerIdentityProof.Verify(proof.PublicKey, proof.Signature, WORLD, "challenge-1").Should().BeTrue();
    }

    [TestMethod]
    public void ASignatureFitsOnlyItsChallengeAndWorld()
    {
        PlayerKeys.Proof proof = PlayerKeys.Prove(storedKey.Value, WORLD, "challenge-1").Proof;

        PlayerIdentityProof.Verify(proof.PublicKey, proof.Signature, WORLD, "challenge-2").Should().BeFalse();
        PlayerIdentityProof.Verify(proof.PublicKey, proof.Signature, "0123456789ABCDEF0123456789ABCDEF", "challenge-1").Should().BeFalse();
    }

    [TestMethod]
    public void AStoredKeyIsUsedAgain()
    {
        (string kept, PlayerKeys.Proof first) = PlayerKeys.Prove(storedKey.Value, WORLD, "a");
        PlayerKeys.Proof second = PlayerKeys.Prove(storedKey.Value, WORLD, "b").Proof;

        kept.Should().Be(storedKey.Value);
        second.PublicKey.Should().Be(first.PublicKey);
    }

    [TestMethod]
    public void AnUnreadableStoredKeyIsReplaced()
    {
        (string kept, PlayerKeys.Proof proof) = PlayerKeys.Prove("not a key", WORLD, "a");

        kept.Should().NotBe("not a key");
        PlayerIdentityProof.Verify(proof.PublicKey, proof.Signature, WORLD, "a").Should().BeTrue();
    }

    [TestMethod]
    public void TheKeyHasTheExpectedSize()
    {
        PlayerKeys.Proof proof = PlayerKeys.Prove(storedKey.Value, WORLD, "a").Proof;

        PlayerIdentityProof.TryParsePublicKey(proof.PublicKey, out _).Should().BeTrue();
    }
}
