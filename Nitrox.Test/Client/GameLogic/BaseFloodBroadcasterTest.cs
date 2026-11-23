using NitroxClient.GameLogic;

namespace Nitrox.Test.Client.GameLogic;

[TestClass]
public sealed class BaseFloodBroadcasterTest
{
    [TestMethod]
    public void DryBaseIsNotReported()
    {
        Assert.IsFalse(BaseFloodBroadcaster.ShouldSend(null, 0f));
    }

    [TestMethod]
    public void FirstWaterIsReported()
    {
        Assert.IsTrue(BaseFloodBroadcaster.ShouldSend(null, 1.5f));
    }

    [TestMethod]
    public void SmallChangeWaitsForALargerOne()
    {
        Assert.IsFalse(BaseFloodBroadcaster.ShouldSend(1.5f, 1.51f));
        Assert.IsTrue(BaseFloodBroadcaster.ShouldSend(1.5f, 1.6f));
    }

    /// <summary>
    ///     After the leaks are welded the water drains. A joiner must then find the base dry, not at the last high level.
    /// </summary>
    [TestMethod]
    public void DrainingToDryIsReported()
    {
        Assert.IsTrue(BaseFloodBroadcaster.ShouldSend(0.02f, 0f));
        Assert.IsTrue(BaseFloodBroadcaster.ShouldSend(3f, 1f));
    }

    [TestMethod]
    public void TotalWaterAddsTheCells()
    {
        Assert.AreEqual(1.5f, BaseFloodBroadcaster.TotalWater([0.5f, 1f, 0f]));
        Assert.AreEqual(0f, BaseFloodBroadcaster.TotalWater(null));
    }
}
