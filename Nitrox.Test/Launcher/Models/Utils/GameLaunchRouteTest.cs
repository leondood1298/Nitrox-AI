namespace Nitrox.Launcher.Models.Utils;

[TestClass]
public class GameLaunchRouteTest
{
    [TestMethod]
    public void TheDefaultStartGoesThroughSteam()
    {
        GameLaunchRoute.ShouldSkipSteam("-vrmode none", false, false, false, 0, true).Should().BeFalse();
    }

    [TestMethod]
    public void AnInstallSteamDoesNotKnowIsStartedDirectly()
    {
        GameLaunchRoute.ShouldSkipSteam("-vrmode none", true, false, false, 0, false).Should().BeTrue();
    }

    [TestMethod]
    public void ASecondCopyIsStartedDirectlyWhateverTheArgumentsOrBigPicture()
    {
        GameLaunchRoute.ShouldSkipSteam("", false, true, true, 0, true).Should().BeTrue();
        GameLaunchRoute.ShouldSkipSteam("-vrmode none", true, true, true, 0, true).Should().BeTrue();
    }

    [TestMethod]
    public void TheFirstCopyWithMultipleInstancesAllowedFollowsTheVrModeRule()
    {
        GameLaunchRoute.ShouldSkipSteam("-vrmode none", false, true, false, 0, true).Should().BeTrue();
        GameLaunchRoute.ShouldSkipSteam("", false, true, false, 0, true).Should().BeFalse();
    }

    [TestMethod]
    public void VrModeAndInstantLaunchKeepTheirRoutes()
    {
        GameLaunchRoute.ShouldSkipSteam("-vrmode openvr", false, false, false, 0, true).Should().BeTrue();
        GameLaunchRoute.ShouldSkipSteam("-vrmode none", false, false, false, 2, true).Should().BeTrue();
        GameLaunchRoute.ShouldSkipSteam("-vrmode openvr", false, false, false, 1, true).Should().BeFalse();
    }
}
