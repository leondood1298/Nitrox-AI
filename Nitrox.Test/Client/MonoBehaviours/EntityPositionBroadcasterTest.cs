using NitroxClient.MonoBehaviours;

namespace Nitrox.Test.Client.MonoBehaviours;

[TestClass]
public sealed class EntityPositionBroadcasterTest
{
    [TestMethod]
    public void ACreaturePushedAwayFromItsPathIsSentAgain()
    {
        Assert.IsTrue(EntityPositionBroadcaster.ShouldRefreshSpline(EntityPositionBroadcaster.SPLINE_REFRESH_SECONDS, 3f));
    }

    [TestMethod]
    public void ACreatureThatBarelyMovedIsNotSentAgain()
    {
        Assert.IsFalse(EntityPositionBroadcaster.ShouldRefreshSpline(10f, 0.2f));
    }

    [TestMethod]
    public void ACreatureJustSentIsNotSentAgainAtOnce()
    {
        Assert.IsFalse(EntityPositionBroadcaster.ShouldRefreshSpline(0.1f, 5f));
    }
}
