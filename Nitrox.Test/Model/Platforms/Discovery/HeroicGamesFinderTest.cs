using Nitrox.Model.Platforms.Discovery.InstallationFinders;

namespace Nitrox.Model.Platforms.Discovery;

[TestClass]
public class HeroicGamesFinderTest
{
    [TestMethod]
    public void TryGetInstallPathReadsTheGamesRecord()
    {
        const string JSON = """{ "Jaguar": { "install_path": "D:\\Games\\Subnautica" } }""";

        HeroicGamesFinder.TryGetInstallPath(JSON, GameInfo.Subnautica, out string path).Should().BeTrue();
        path.Should().Be(@"D:\Games\Subnautica");
    }

    [TestMethod]
    public void TryGetInstallPathFindsNothingWithoutARecordOrPath()
    {
        HeroicGamesFinder.TryGetInstallPath("{}", GameInfo.Subnautica, out _).Should().BeFalse();
        HeroicGamesFinder.TryGetInstallPath("""{ "Jaguar": { "install_path": "" } }""", GameInfo.Subnautica, out _).Should().BeFalse();
    }
}
