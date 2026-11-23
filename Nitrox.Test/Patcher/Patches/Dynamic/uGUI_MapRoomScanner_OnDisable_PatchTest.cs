using NitroxPatcher.Patches.Dynamic;

namespace Nitrox.Test.Patcher.Patches.Dynamic;

[TestClass]
public sealed class uGUI_MapRoomScanner_OnDisable_PatchTest
{
    [TestMethod]
    public void ScreenThatIsSwitchedOffNeverStopsTheScanByItself()
    {
        // The game's OnDisable stops the scan, whatever the screen was switched off for. A prefix that returns false
        // keeps the game's method from running. What a real loss of power does is covered by MapRoomScansTest.
        Assert.IsFalse(uGUI_MapRoomScanner_OnDisable_Patch.Prefix());
    }
}
