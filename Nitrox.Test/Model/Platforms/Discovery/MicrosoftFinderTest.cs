using Nitrox.Model.Platforms.Discovery.InstallationFinders;
using Nitrox.Test.Model.Platforms;

namespace Nitrox.Model.Platforms.Discovery;

[TestClass]
public class MicrosoftFinderTest
{
    [OSTestMethod(OperatingSystems.Windows)]
    public void CandidatePathsStartAtTheDriveRootNotTheDrivesCurrentFolder()
    {
        string candidate = MicrosoftFinder.CandidatePaths([@"C:\", @"D:\"], "Subnautica").First();

        candidate.Should().Be(@"C:\XboxGames\Subnautica\Content");
        Path.IsPathFullyQualified(candidate).Should().BeTrue();
    }
}
