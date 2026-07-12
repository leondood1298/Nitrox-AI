using Microsoft.VisualStudio.TestTools.UnitTesting;
using NitroxClient.GameLogic;

namespace Nitrox.Test.Client.GameLogic;

[TestClass]
public sealed class MapRoomUpgradeEffectsTest
{
    [TestMethod]
    public void CalculatesAllMixedZeroToFourModuleEffects()
    {
        float[] expectedRanges = [300f, 350f, 400f, 450f, 500f];
        float[] expectedIntervals = [14f, 11f, 8f, 5f, 2f];

        for (int rangeModules = 0; rangeModules <= 4; rangeModules++)
        {
            for (int speedModules = 0; speedModules <= 4; speedModules++)
            {
                Assert.AreEqual(expectedRanges[rangeModules], MapRoomUpgradeEffects.ScanRange(rangeModules));
                Assert.AreEqual(expectedIntervals[speedModules], MapRoomUpgradeEffects.ScanInterval(speedModules));
            }
        }
    }

    [TestMethod]
    public void ClampsInvalidAndExcessModuleCounts()
    {
        Assert.AreEqual(300f, MapRoomUpgradeEffects.ScanRange(-1));
        Assert.AreEqual(500f, MapRoomUpgradeEffects.ScanRange(20));
        Assert.AreEqual(14f, MapRoomUpgradeEffects.ScanInterval(-1));
        Assert.AreEqual(1f, MapRoomUpgradeEffects.ScanInterval(20));
    }
}
