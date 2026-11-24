using NitroxClient.GameLogic.FMOD;

namespace Nitrox.Test.Client.GameLogic.FMOD;

[TestClass]
public sealed class RemotePlayerSoundRulesTest
{
    [TestMethod]
    public void GoingThroughTheSurfaceEitherWaySplashes()
    {
        RemotePlayerSoundRules.ShouldSplash(0.5f, -0.5f, false, 5f).Should().BeTrue();
        RemotePlayerSoundRules.ShouldSplash(-0.5f, 0.5f, false, 5f).Should().BeTrue();
    }

    [TestMethod]
    public void StayingOnOneSideDoesNotSplash()
    {
        RemotePlayerSoundRules.ShouldSplash(-3f, -2f, false, 5f).Should().BeFalse();
        RemotePlayerSoundRules.ShouldSplash(3f, 2f, false, 5f).Should().BeFalse();
    }

    [TestMethod]
    public void BobbingAtTheSurfaceSplashesAtMostOncePerInterval()
    {
        RemotePlayerSoundRules.ShouldSplash(0.1f, -0.1f, false, RemotePlayerSoundRules.SPLASH_INTERVAL / 2).Should().BeFalse();
    }

    [TestMethod]
    public void InsideABaseOrVehicleThereIsNoSplash()
    {
        RemotePlayerSoundRules.ShouldSplash(0.5f, -0.5f, true, 5f).Should().BeFalse();
    }

    [TestMethod]
    public void TheSeaglideRunsWhenItsHolderMovesFastUnderwater()
    {
        RemotePlayerSoundRules.SeaglideRuns(RemotePlayerSoundRules.SEAGLIDE_RUNNING_SPEED + 1f, true).Should().BeTrue();
        RemotePlayerSoundRules.SeaglideRuns(1f, true).Should().BeFalse();
        RemotePlayerSoundRules.SeaglideRuns(RemotePlayerSoundRules.SEAGLIDE_RUNNING_SPEED + 1f, false).Should().BeFalse();
    }
}
