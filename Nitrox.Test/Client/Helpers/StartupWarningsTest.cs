using Nitrox.Model.Helper;

namespace NitroxClient.Helpers;

[TestClass]
public class StartupWarningsTest
{
    [TestMethod]
    public void NothingToSayGivesNoMessage()
    {
        StartupWarnings.BuildMainMenuMessage([], 83031, GameBuildSupport.SUPPORTED, 83031, "{patches}", "{build}").Should().BeNull();
    }

    [TestMethod]
    public void AnUntestedBuildAndFailedPatchesAreBothNamed()
    {
        string? message = StartupWarnings.BuildMainMenuMessage(["A_Patch: missing method", "B_Patch: bad IL"], 90000, GameBuildSupport.UNTESTED, 83031,
                                                              "Failed: {patches}", "Build {build} is newer than {tested}");

        message.Should().Be("Build 90000 is newer than 83031\n\nFailed: A_Patch, B_Patch");
    }

    [TestMethod]
    public void ManyFailedPatchesAreCountedAfterTheFirstFive()
    {
        StartupWarnings.DescribeFailedPatches("{patches}", Enumerable.Range(1, 7).Select(i => $"P{i}: x")).Should().Be("P1, P2, P3, P4, P5 (+2)");
    }
}
