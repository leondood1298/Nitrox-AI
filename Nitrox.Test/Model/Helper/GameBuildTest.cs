namespace Nitrox.Model.Helper;

[TestClass]
public class GameBuildTest
{
    [TestMethod]
    public void ParseReadsTheBuildNumberWithSurroundingWhitespace()
    {
        GameBuild.Parse("83031\r\n").Should().Be(83031);
    }

    [TestMethod]
    public void ParseGivesNothingForTextThatIsNotABuildNumber()
    {
        GameBuild.Parse("").Should().BeNull();
        GameBuild.Parse(null).Should().BeNull();
        GameBuild.Parse("legacy").Should().BeNull();
        GameBuild.Parse("0").Should().BeNull();
    }

    [TestMethod]
    public void ClassifyJudgesBuildsAgainstTheMinimumAndTheNewestTested()
    {
        GameBuild.Classify(null, 100, 200).Should().Be(GameBuildSupport.UNKNOWN);
        GameBuild.Classify(99, 100, 200).Should().Be(GameBuildSupport.OUTDATED);
        GameBuild.Classify(100, 100, 200).Should().Be(GameBuildSupport.SUPPORTED);
        GameBuild.Classify(200, 100, 200).Should().Be(GameBuildSupport.SUPPORTED);
        GameBuild.Classify(201, 100, 200).Should().Be(GameBuildSupport.UNTESTED);
    }

    [TestMethod]
    public void TheBuiltInNewestTestedBuildIsNotOlderThanTheMinimum()
    {
        Nitrox.Model.Core.NitroxEnvironment.GameNewestTestedVersion.Should().BeGreaterThanOrEqualTo(Nitrox.Model.Core.NitroxEnvironment.GameMinimumVersion);
    }

    [TestMethod]
    public void TryReadReadsTheGamesVersionFile()
    {
        string dataFolder = Path.Combine(Path.GetTempPath(), $"GameBuildTest_{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(GameBuild.GetVersionFilePath(dataFolder))!);
            File.WriteAllText(GameBuild.GetVersionFilePath(dataFolder), "83031");

            GameBuild.TryRead(dataFolder).Should().Be(83031);
            GameBuild.TryRead(Path.Combine(dataFolder, "missing")).Should().BeNull();
        }
        finally
        {
            Directory.Delete(dataFolder, true);
        }
    }
}
