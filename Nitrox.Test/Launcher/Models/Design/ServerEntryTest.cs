using System.Threading.Tasks;
using Nitrox.Launcher.Models.Design;
using Nitrox.Model.Configuration;
using Nitrox.Model.Serialization;

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

    [TestMethod]
    public void ConsoleKeepsOnlyTheNewestLines()
    {
        ServerEntry entry = new() { Name = "My World" };

        for (int i = 0; i < ServerEntry.MAX_OUTPUT_LINES + 10; i++)
        {
            entry.AddOutput(new OutputLine { LogText = $"line {i}", LocalTime = null, Type = OutputLineType.INFO_LOG });
        }

        entry.Output.Should().HaveCount(ServerEntry.MAX_OUTPUT_LINES);
        entry.Output[0].LogText.Should().Be("line 10");
        entry.Output[^1].LogText.Should().Be($"line {ServerEntry.MAX_OUTPUT_LINES + 9}");
    }

    [TestMethod]
    [DataRow("""{"Major":1,"Minor":9,"Build":0,"Revision":0}""", true, DisplayName = "World from before the save format")]
    [DataRow("""{"Major":1,"Minor":9,"Build":0,"Revision":0,"SaveFormat":99}""", true, DisplayName = "World from a newer build")]
    [DataRow("""{"Major":1,"Minor":9,"Build":0,"Revision":0,"SaveFormat":1}""", false, DisplayName = "World of this build")]
    [DataRow("", false, DisplayName = "World nobody has saved yet")]
    public async Task WorldOfAnotherSaveFormatIsRecognised(string versionJson, bool refused)
    {
        string saveDir = Path.Combine(Path.GetTempPath(), $"nitrox-entry-test-{Guid.NewGuid():N}", "My World");
        Directory.CreateDirectory(saveDir);
        try
        {
            NitroxConfig.CreateFile(saveDir, new SubnauticaServerOptions());
            File.WriteAllText(Path.Combine(saveDir, "Version.json"), versionJson);
            ServerEntry entry = new() { Name = "My World" };

            await entry.RefreshFromDirectoryAsync(saveDir);

            if (refused)
            {
                entry.SaveFormatMismatch.Should().Contain("\"My World\"");
            }
            else
            {
                entry.SaveFormatMismatch.Should().BeNull();
            }
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(saveDir)!, true);
        }
    }
}
