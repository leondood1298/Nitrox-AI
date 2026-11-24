using NitroxClient.GameLogic.FMOD;
using static NitroxClient.GameLogic.FMOD.WorldAnimationSounds;

namespace Nitrox.Test.Client.GameLogic.FMOD;

[TestClass]
public sealed class WorldAnimationSoundsTest
{
    [TestMethod]
    public void TheLocalPlayersOwnAnimationSoundsAsUsual()
    {
        Decide(true, false, true).Should().Be(Handling.AS_USUAL);
    }

    [TestMethod]
    public void ASentSoundOfAnotherPlayersToolIsNotPlayedAgainHere()
    {
        Decide(false, true, true).Should().Be(Handling.SILENT, "that player's game sends it; playing it here as well would double it");
    }

    [TestMethod]
    public void AnUnsentSoundOfAnotherPlayersToolIsPlayedHere()
    {
        Decide(false, true, false).Should().Be(Handling.REPLAYED, "nobody sends it, so this copy is its only source");
    }

    [TestMethod]
    public void AWorldObjectsAnimationSoundIsEachGamesOwn()
    {
        Decide(false, false, true).Should().Be(Handling.REPLAYED);
    }
}
