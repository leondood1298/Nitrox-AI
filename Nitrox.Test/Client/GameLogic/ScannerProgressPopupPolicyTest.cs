using NitroxClient.GameLogic;

namespace Nitrox.Test.Client.GameLogic;

[TestClass]
public sealed class ScannerProgressPopupPolicyTest
{
    [TestMethod]
    public void ShowsOnlyForNewRemotePartialProgressAfterInitialSync()
    {
        Assert.IsTrue(ScannerProgressPopupPolicy.ShouldShow(false, false, 1, 2, 4, true, true));
        Assert.IsFalse(ScannerProgressPopupPolicy.ShouldShow(false, false, 2, 2, 4, true, true));
        Assert.IsFalse(ScannerProgressPopupPolicy.ShouldShow(false, false, 1, 2, 4, true, false));
        Assert.IsFalse(ScannerProgressPopupPolicy.ShouldShow(false, false, 1, 2, 4, false, false));
    }

    [TestMethod]
    public void SuppressesResearchedCompletedAndSingleFragmentPopups()
    {
        Assert.IsFalse(ScannerProgressPopupPolicy.ShouldShow(true, false, 0, 1, 4, true, true));
        Assert.IsFalse(ScannerProgressPopupPolicy.ShouldShow(false, true, 3, 4, 4, true, true));
        Assert.IsFalse(ScannerProgressPopupPolicy.ShouldShow(false, false, 0, 1, 1, true, true));
    }
}
