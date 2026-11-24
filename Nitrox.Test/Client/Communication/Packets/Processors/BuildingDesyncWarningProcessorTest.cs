using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.Packets;
using NitroxClient.Communication.Packets.Processors;
using NitroxClient.GameLogic.Bases;

namespace Nitrox.Test.Client.Communication.Packets.Processors;

[TestClass]
public sealed class BuildingDesyncWarningProcessorTest
{
    private static readonly NitroxId refusedBaseId = new("00000000-0000-0000-0000-000000000701");
    private static readonly NitroxId otherBaseId = new("00000000-0000-0000-0000-000000000702");

    /// <summary>
    ///     An operation refused on one base must not stop the player from building on the others.
    /// </summary>
    [TestMethod]
    public void OnlyTheBaseOfTheRefusedOperationIsMarked()
    {
        Dictionary<NitroxId, OperationTracker> trackers = new();

        BuildingDesyncWarningProcessor.MarkDesynced(new BuildingDesyncWarning(new Dictionary<NitroxId, int> { [refusedBaseId] = 4, [otherBaseId] = 9 }, refusedBaseId),
            id => trackers.TryGetValue(id, out OperationTracker tracker) ? tracker : trackers[id] = new OperationTracker());

        Assert.IsTrue(trackers[refusedBaseId].IsDesynced());
        Assert.AreEqual(4, trackers[refusedBaseId].LastOperationId);
        Assert.IsFalse(trackers.ContainsKey(otherBaseId));
    }

    [TestMethod]
    public void WithoutABaseEveryBaseIsMarked()
    {
        Dictionary<NitroxId, OperationTracker> trackers = new();

        BuildingDesyncWarningProcessor.MarkDesynced(new BuildingDesyncWarning(new Dictionary<NitroxId, int> { [refusedBaseId] = 4, [otherBaseId] = 9 }, null),
            id => trackers.TryGetValue(id, out OperationTracker tracker) ? tracker : trackers[id] = new OperationTracker());

        Assert.IsTrue(trackers[refusedBaseId].IsDesynced());
        Assert.IsTrue(trackers[otherBaseId].IsDesynced());
    }
}
