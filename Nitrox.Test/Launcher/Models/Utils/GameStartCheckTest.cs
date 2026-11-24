using Nitrox.Model.Helper;

namespace Nitrox.Launcher.Models.Utils;

[TestClass]
public class GameStartCheckTest
{
    [TestMethod]
    public void AReportWithoutProblemsNeedsNoMessage()
    {
        GameStartReport report = new(DateTime.UtcNow, true, []);

        GameStartCheck.Judge(report, true).Should().Be(GameStartCheck.Outcome.LOADED);
        GameStartCheck.Describe(GameStartCheck.Outcome.LOADED, report).Should().BeNull();
    }

    [TestMethod]
    public void FailedPatchesAreNamedToThePlayer()
    {
        GameStartReport report = new(DateTime.UtcNow, true, ["Patch Foo_Bar_Patch failed: method not found."]);

        GameStartCheck.Judge(report, true).Should().Be(GameStartCheck.Outcome.LOADED_WITH_PROBLEMS);
        GameStartCheck.Describe(GameStartCheck.Outcome.LOADED_WITH_PROBLEMS, report).Should().Contain("Foo_Bar_Patch");
    }

    [TestMethod]
    public void ARunningGameWithoutAReportNamesTheLikelyCauses()
    {
        GameStartCheck.Judge(null, true).Should().Be(GameStartCheck.Outcome.NOT_LOADED);
        GameStartCheck.Describe(GameStartCheck.Outcome.NOT_LOADED, null).Should().Contain("different Subnautica install").And.Contain("Linux").And.Contain("Player.log");
    }

    [TestMethod]
    public void AGameThatIsNoLongerRunningGivesNoWarning()
    {
        GameStartCheck.Judge(null, false).Should().Be(GameStartCheck.Outcome.GAME_NOT_RUNNING);
        GameStartCheck.Describe(GameStartCheck.Outcome.GAME_NOT_RUNNING, null).Should().BeNull();
    }

    [TestMethod]
    public void AFailedStartIsAnError()
    {
        GameStartReport report = new(DateTime.UtcNow, false, ["Could not load Nitrox.Model."]);

        GameStartCheck.Judge(report, true).Should().Be(GameStartCheck.Outcome.FAILED);
        GameStartCheck.Describe(GameStartCheck.Outcome.FAILED, report).Should().Contain("Could not load Nitrox.Model.");
    }
}
