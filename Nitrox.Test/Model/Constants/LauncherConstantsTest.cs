using Nitrox.Model.Constants;

namespace Nitrox.Test.Model.Constants;

[TestClass]
public sealed class LauncherConstantsTest
{
    private static readonly string buildA = Path.Combine(Path.GetTempPath(), "nitrox-build-a");
    private static readonly string buildB = Path.Combine(Path.GetTempPath(), "nitrox-build-b");

    [TestMethod]
    public void EachInstallationHasItsOwnPipeAndConnectionFile()
    {
        LauncherConstants.GetNamedPipeName(buildA).Should().NotBe(LauncherConstants.GetNamedPipeName(buildB));
        LauncherConstants.GetConnectionFileName(buildA).Should().NotBe(LauncherConstants.GetConnectionFileName(buildB));
        LauncherConstants.GetNamedPipeName(buildA).Should().NotBe(LauncherConstants.GRPC_NAMED_PIPE_NAME, "upstream Nitrox uses that one");
    }

    [TestMethod]
    public void LauncherAndItsServersAgreeOnTheNamesWhateverTheSpelling()
    {
        LauncherConstants.GetNamedPipeName(buildA + Path.DirectorySeparatorChar).Should().Be(LauncherConstants.GetNamedPipeName(buildA.ToUpperInvariant()));
    }
}
