using System.IO;
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

        CollectionAssert.AreEqual(new[] { "--save", "My World", "--game-path", "game", "--data-path", dataPath }, arguments);
    }

    [TestMethod]
    public void TrailingSeparatorOnTheWorldFolderChangesNothing()
    {
        List<string> arguments = ServerEntry.GetServerArguments(Path.Combine(dataPath, "saves", "My World") + Path.DirectorySeparatorChar, "game", defaultDataPath);

        Assert.AreEqual("My World", arguments[1]);
        Assert.AreEqual(dataPath, arguments[5]);
    }

    [TestMethod]
    public void WorldOutsideASavesFolderFallsBackToTheLaunchersDataFolder()
    {
        // The server can only look in <data folder>/saves, so a custom worlds folder cannot be expressed
        List<string> arguments = ServerEntry.GetServerArguments(Path.Combine(dataPath, "worlds", "My World"), "game", defaultDataPath);

        Assert.AreEqual(defaultDataPath, arguments[5]);
    }
}
