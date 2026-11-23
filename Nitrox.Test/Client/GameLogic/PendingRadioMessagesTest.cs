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
}
