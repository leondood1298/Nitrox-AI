using NitroxClient.GameLogic.Bases;

namespace Nitrox.Test.Client.GameLogic.Bases;

[TestClass]
public sealed class OperationTrackerTest
{
    /// <summary>
    ///     Build packets held while a player joined or resynced may already be in the snapshot of their base.
    /// </summary>
    [TestMethod]
    public void OperationInTheSnapshotIsAlreadyApplied()
    {
        OperationTracker tracker = new();
        tracker.ApplySnapshot(5);

        Assert.IsTrue(tracker.IsAlreadyApplied(4));
        Assert.IsTrue(tracker.IsAlreadyApplied(5));
        Assert.IsFalse(tracker.IsAlreadyApplied(6));
    }

    [TestMethod]
    public void NothingIsAppliedToAnUnknownBase()
    {
        Assert.IsFalse(new OperationTracker().IsAlreadyApplied(0));
    }

    /// <summary>
    ///     A resync answers a refused operation with the operation the base is at, which the desync warning had already
    ///     given. Taking that snapshot has to clear the desync, or the player could never build there again.
    /// </summary>
    [TestMethod]
    public void SnapshotAtTheKnownOperationClearsTheDesync()
    {
        OperationTracker tracker = new() { LastOperationId = 7, FailedOperations = 1 };
        Assert.IsTrue(tracker.IsDesynced());

        tracker.ApplySnapshot(7);

        Assert.IsFalse(tracker.IsDesynced());
        Assert.AreEqual(7, tracker.LastOperationId);
    }

    [TestMethod]
    public void OlderSnapshotDoesNotUndoNewerOperations()
    {
        OperationTracker tracker = new();
        tracker.ApplySnapshot(3);
        tracker.RegisterOperation(4);

        tracker.ApplySnapshot(3);

        Assert.AreEqual(4, tracker.LastOperationId);
    }
}
