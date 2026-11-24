namespace Nitrox.Model.Platforms.Store;

[TestClass]
public class HeroicGamesTest
{
    [TestMethod]
    public void EveryArgumentIsEscapedSoPathsWithSpacesAndAmpersandsSurvive()
    {
        string uri = HeroicGames.BuildLaunchUri("Jaguar", @"-vrmode none --data-path ""D:\My & Data""", @"C:\Nitrox Launcher", true);

        uri.Should().Be("heroic://launch?runner=legendary&appName=Jaguar&arg=--nitrox&arg=C%3A%5CNitrox%20Launcher&arg=-vrmode&arg=none&arg=--data-path&arg=D%3A%5CMy%20%26%20Data");
    }

    [TestMethod]
    public void ASinglePlayerStartPassesNoLauncherPath()
    {
        HeroicGames.BuildLaunchUri("Jaguar", "-vrmode none", null, true).Should().NotContain("nitrox");
    }

    [TestMethod]
    public void OffWindowsTheLauncherPathIsGivenAsAWinePath()
    {
        HeroicGames.BuildLaunchUri("Jaguar", "", "/home/me/nitrox", false).Should().Contain("arg=--nitrox&arg=Z%3A%5Chome%5Cme%5Cnitrox");
    }
}
