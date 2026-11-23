using System.Web;
using Nitrox.Launcher.ViewModels;

namespace Nitrox.Test.Launcher.ViewModels;

[TestClass]
public sealed class CrashWindowViewModelTest
{
    [TestMethod]
    public void ReportGoesToTheForksIssueForm()
    {
        string url = CrashWindowViewModel.CreateIssueUrl("NullReferenceException", "NullReferenceException\n   at Somewhere()", "1.9.0.0 (abc1234)", "Steam");

        url.Should().StartWith("https://github.com/leondood1298/Nitrox-AI/issues/new?");
        url.Should().NotContainEquivalentOf("SubnauticaNitrox");
        HttpUtility.ParseQueryString(new Uri(url).Query)["body"].Should().Contain("at Somewhere()");
    }

    [TestMethod]
    public void LongCrashTextIsCutShort()
    {
        string url = CrashWindowViewModel.CreateIssueUrl("Boom", new string('x', 20_000), "1.9.0.0", "Steam");

        url.Length.Should().BeLessThan(8_000);
    }
}
