using Nitrox.Model.GameLogic.FMOD;

namespace Nitrox.Test.Model.GameLogic.FMOD;

[TestClass]
public sealed class SoundHelperTest
{
    [TestMethod]
    public void ARepeatedNonPositionalSoundWithARadiusFadesByDistance()
    {
        SoundHelper.FadesByReplayDistance(true, false, 20f).Should().BeTrue();
    }

    [TestMethod]
    public void APositionalSoundFadesByItself()
    {
        SoundHelper.FadesByReplayDistance(true, true, 20f).Should().BeFalse();
    }

    [TestMethod]
    public void ASoundWithoutARadiusIsThePlayersOwnAndKeepsItsVolume()
    {
        SoundHelper.FadesByReplayDistance(true, false, 0f).Should().BeFalse();
        SoundHelper.FadesByReplayDistance(false, false, 20f).Should().BeFalse();
    }

    [TestMethod]
    public void VolumeIsReducedOnceLinearlyOverTheRadius()
    {
        SoundHelper.CalculateVolume(10f, 20f, 1f).Should().BeApproximately(0.5f, 0.0001f);
        SoundHelper.CalculateVolume(25f, 20f, 1f).Should().Be(0f);
    }
}
