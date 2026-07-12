using Nitrox.Model.DataStructures.Unity;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Bases;
using Nitrox.Model.Subnautica.Packets;

namespace NitroxClient.GameLogic;

[TestClass]
public sealed class MapRoomScanResultsTest
{
    private static readonly NitroxTechType quartz = new("Quartz");

    [TestMethod]
    public void SnapshotReplacesLocalResultsInCanonicalOrder()
    {
        List<ResourceTrackerDatabase.ResourceInfo> local = [Info("stale", 0f)];
        List<MapRoomScanResultRecord> snapshot =
        [
            new("first", quartz, new NitroxVector3(1f, 0f, 0f)),
            new("second", quartz, new NitroxVector3(2f, 0f, 0f))
        ];

        MapRoomScanResults.ReconcileSnapshot(local, snapshot);

        CollectionAssert.AreEqual(new[] { "first", "second" }, local.Select(info => info.uniqueId).ToArray());
    }

    [TestMethod]
    public void DeltaAddsUpdatesAndRemovesByStableId()
    {
        List<ResourceTrackerDatabase.ResourceInfo> local = [Info("resource", 1f)];
        Nitrox.Model.DataStructures.NitroxId roomId = new();

        MapRoomScanResults.ApplyDeltaToList(local, new MapRoomScanResultChanged(roomId, 1, "resource", quartz, new NitroxVector3(4f, 0f, 0f)));
        MapRoomScanResults.ApplyDeltaToList(local, new MapRoomScanResultChanged(roomId, 1, "new", quartz, new NitroxVector3(2f, 0f, 0f)));
        MapRoomScanResults.ApplyDeltaToList(local, new MapRoomScanResultChanged(roomId, 1, "resource", quartz, NitroxVector3.Zero, removed: true));

        Assert.AreEqual(1, local.Count);
        Assert.AreEqual("new", local[0].uniqueId);
    }

    private static ResourceTrackerDatabase.ResourceInfo Info(string id, float x) => new()
    {
        uniqueId = id,
        techType = TechType.Quartz,
        position = new UnityEngine.Vector3(x, 0f, 0f)
    };
}
