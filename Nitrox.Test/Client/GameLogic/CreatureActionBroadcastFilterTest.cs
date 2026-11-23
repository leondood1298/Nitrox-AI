using Nitrox.Model.DataStructures;
using NitroxClient.GameLogic;

namespace Nitrox.Test.Client.GameLogic;

[TestClass]
public sealed class CreatureActionBroadcastFilterTest
{
    private const float RESEND_AFTER_SECONDS = 5f;

    [TestMethod]
    public void SameActionChosenEveryFrameIsSentOnce()
    {
        CreatureActionBroadcastFilter filter = new(RESEND_AFTER_SECONDS);
        NitroxId reaper = new();

        Assert.IsTrue(filter.ShouldSend(reaper, "SwimRandom", 10f));
        Assert.IsFalse(filter.ShouldSend(reaper, "SwimRandom", 10.016f));
        Assert.IsFalse(filter.ShouldSend(reaper, "SwimRandom", 14.9f));
    }

    [TestMethod]
    public void ChangedActionIsSentAtOnce()
    {
        CreatureActionBroadcastFilter filter = new(RESEND_AFTER_SECONDS);
        NitroxId reaper = new();
        filter.ShouldSend(reaper, "SwimRandom", 10f);

        Assert.IsTrue(filter.ShouldSend(reaper, "AttackLastTarget", 10.016f));
        Assert.IsTrue(filter.ShouldSend(reaper, "SwimRandom", 10.032f), "Going back to the earlier action is a change too.");
    }

    [TestMethod]
    public void UnchangedActionIsRepeatedForPlayersWhoLoadedTheCreatureLater()
    {
        CreatureActionBroadcastFilter filter = new(RESEND_AFTER_SECONDS);
        NitroxId reaper = new();
        filter.ShouldSend(reaper, "SwimRandom", 10f);

        Assert.IsTrue(filter.ShouldSend(reaper, "SwimRandom", 15.5f));
        Assert.IsFalse(filter.ShouldSend(reaper, "SwimRandom", 16f), "The repeat restarts the wait.");
    }

    [TestMethod]
    public void NoActionIsOnlySentWhenAReplayedActionEnds()
    {
        CreatureActionBroadcastFilter filter = new(RESEND_AFTER_SECONDS);
        NitroxId peeper = new();

        Assert.IsFalse(filter.ShouldSend(peeper, CreatureActionBroadcastFilter.NO_ACTION, 10f), "A creature that never did a replayed action costs nothing.");
        Assert.IsTrue(filter.ShouldSend(peeper, "AttackLastTarget", 11f));
        Assert.IsTrue(filter.ShouldSend(peeper, CreatureActionBroadcastFilter.NO_ACTION, 12f));
        Assert.IsFalse(filter.ShouldSend(peeper, CreatureActionBroadcastFilter.NO_ACTION, 20f), "Doing nothing is not repeated.");
    }

    [TestMethod]
    public void CreaturesAreTrackedSeparately()
    {
        CreatureActionBroadcastFilter filter = new(RESEND_AFTER_SECONDS);
        NitroxId first = new();
        NitroxId second = new();
        filter.ShouldSend(first, "SwimRandom", 10f);

        Assert.IsTrue(filter.ShouldSend(second, "SwimRandom", 10f));
        Assert.IsFalse(filter.ShouldSend(first, "SwimRandom", 10.5f));
    }

    [TestMethod]
    public void TrackingStaysBoundedAndOnlyCostsARepeat()
    {
        CreatureActionBroadcastFilter filter = new(RESEND_AFTER_SECONDS, maxTrackedCreatures: 2);
        NitroxId first = new();
        filter.ShouldSend(first, "SwimRandom", 10f);
        filter.ShouldSend(new NitroxId(), "SwimRandom", 10f);

        Assert.IsTrue(filter.ShouldSend(new NitroxId(), "SwimRandom", 10f), "A third creature is still handled.");
        Assert.IsTrue(filter.ShouldSend(first, "SwimRandom", 10.1f), "After starting over, known creatures are sent again once.");
        Assert.IsFalse(filter.ShouldSend(first, "SwimRandom", 10.2f));
    }
}
