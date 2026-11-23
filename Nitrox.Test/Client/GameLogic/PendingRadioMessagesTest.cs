using NitroxClient.GameLogic;

namespace Nitrox.Test.Client.GameLogic;

[TestClass]
public sealed class PendingRadioMessagesTest
{
    [TestMethod]
    public void TryMoveToFront_MessageFurtherBack_BecomesTheHead()
    {
        List<string> pending = ["RadioA", "RadioB", "RadioC"];

        PendingRadioMessages.TryMoveToFront(pending, "RadioC").Should().BeTrue();

        pending.Should().Equal("RadioC", "RadioA", "RadioB");
    }

    [TestMethod]
    public void TryMoveToFront_MessageNotPending_LeavesTheQueue()
    {
        List<string> pending = ["RadioA"];

        PendingRadioMessages.TryMoveToFront(pending, "RadioB").Should().BeFalse();

        pending.Should().Equal("RadioA");
    }

    [TestMethod]
    public void Merge_MessageReceivedWhileLoading_IsKeptOnce()
    {
        List<string> pending = ["RadioB", "RadioC"];

        PendingRadioMessages.Merge(pending, ["RadioA", "RadioB"], _ => false);

        pending.Should().Equal("RadioA", "RadioB", "RadioC");
    }

    [TestMethod]
    public void Merge_EmptyQueue_TakesTheServerQueue()
    {
        List<string> pending = [];

        PendingRadioMessages.Merge(pending, ["RadioA", "RadioB"], _ => false);

        pending.Should().Equal("RadioA", "RadioB");
    }

    [TestMethod]
    public void Merge_MessagePlayedByAnotherPlayerWhileLoading_IsNotPending()
    {
        // RadioA is still in an older snapshot's queue, RadioB came by packet before another player played both
        List<string> pending = ["RadioB"];
        HashSet<string> completed = ["OnPlayRadioA", "OnPlayRadioB"];

        PendingRadioMessages.Merge(pending, ["RadioA", "RadioC"], completed.Contains);

        pending.Should().Equal("RadioC");
    }

    [TestMethod]
    public void GetPlayedGoal_IsTheGoalTheGameCompletesWhenItPlaysTheMessage()
    {
        PendingRadioMessages.GetPlayedGoal("RadioSunbeam4").Should().Be("OnPlayRadioSunbeam4");
    }
}
