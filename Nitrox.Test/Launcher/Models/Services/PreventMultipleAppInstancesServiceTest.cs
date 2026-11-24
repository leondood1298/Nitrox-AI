using Nitrox.Launcher.Models.Services;

namespace Nitrox.Test.Launcher.Models.Services;

[TestClass]
public sealed class PreventMultipleAppInstancesServiceTest
{
    private static readonly string buildA = Path.Combine(Path.GetTempPath(), "nitrox-build-a");
    private static readonly string buildB = Path.Combine(Path.GetTempPath(), "nitrox-build-b");

    [TestMethod]
    public void LauncherOfAnotherInstallationIsNotTakenForThisOne()
    {
        PreventMultipleAppInstancesService.IsSameInstallation(buildB, buildA).Should().BeFalse();
    }

    [TestMethod]
    public void LauncherOfThisInstallationIsBroughtForward()
    {
        PreventMultipleAppInstancesService.IsSameInstallation(buildA + Path.DirectorySeparatorChar, buildA).Should().BeTrue();
        PreventMultipleAppInstancesService.IsSameInstallation(null, buildA).Should().BeTrue("a launcher whose folder cannot be read keeps the old one-at-a-time rule");
    }
}
