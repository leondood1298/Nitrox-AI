using NitroxPatcher.Patches.Dynamic;

namespace Nitrox.Test.Patcher.Patches.Dynamic;

[TestClass]
public sealed class uGUI_MapRoomScanner_OnDisable_PatchTest
{
    [TestMethod]
    public void GameNeverStopsTheScanBecauseTheScreenIsSwitchedOff()
    {
        // The game's OnDisable stops the scan. A prefix that returns false keeps the game's method from running.
        Assert.IsFalse(uGUI_MapRoomScanner_OnDisable_Patch.Prefix());
    }
}
