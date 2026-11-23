using Nitrox.Launcher.Models.Design;

namespace Nitrox.Test.Launcher.Models.Design;

/// <summary>
///     The server finds its worlds from its own command line, so the launcher has to tell it where the world it starts
///     lies.
/// </summary>
[TestClass]
public sealed class ServerEntryTest
{
    private static readonly string dataPath = Path.Combine(Path.GetTempPath(), "nitrox-test-data");
    private static readonly string defaultDataPath = Path.Combine(Path.GetTempPath(), "nitrox-default-data");

    [TestMethod]
    public void ServerIsToldTheDataFolderTheWorldLiesIn()
    {
        List<string> arguments = ServerEntry.GetServerArguments(Path.Combine(dataPath, "saves", "My World"), "game", defaultDataPath);

        arguments.Should().Equal("--save", "My World", "--game-path", "game", "--data-path", dataPath, "--saves-path", Path.Combine(dataPath, "saves"));
    }

    [TestMethod]
    public void TrailingSeparatorOnTheWorldFolderChangesNothing()
    {
        List<string> arguments = ServerEntry.GetServerArguments(Path.Combine(dataPath, "saves", "My World") + Path.DirectorySeparatorChar, "game", defaultDataPath);

        arguments[1].Should().Be("My World");
        arguments[5].Should().Be(dataPath);
    }

    [TestMethod]
    public void WorldsFolderOfTheLaunchersChoiceIsPassedOn()
    {
        // The data folder cannot express a worlds folder that is not named "saves", so it goes as --saves-path
        List<string> arguments = ServerEntry.GetServerArguments(Path.Combine(dataPath, "worlds", "My World"), "game", defaultDataPath);

        arguments[5].Should().Be(defaultDataPath);
        arguments.Should().ContainInConsecutiveOrder("--saves-path", Path.Combine(dataPath, "worlds"));
    }
}
