using Nitrox.Model.DataStructures.Unity;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Bases;
using Nitrox.Model.Subnautica.Packets;
using NitroxClient.GameLogic.Spawning.Metadata.Processor;

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

    [TestMethod]
    public void LocalPickupRemovesMatchingStableIdOnly()
    {
        List<ResourceTrackerDatabase.ResourceInfo> local = [Info("collected", 1f), Info("remaining", 2f)];

        Assert.IsTrue(MapRoomScanResults.RemoveFromList(local, "collected"));
        Assert.IsFalse(MapRoomScanResults.RemoveFromList(local, "missing"));

        Assert.AreEqual(1, local.Count);
        Assert.AreEqual("remaining", local[0].uniqueId);
    }

    [TestMethod]
    public void LocalPickupEvictsMatchingHudCacheNodeOnly()
    {
        HashSet<ResourceTrackerDatabase.ResourceInfo> local = [Info("collected", 1f), Info("remaining", 2f)];

        Assert.IsTrue(MapRoomScanResults.RemoveFromSet(local, "collected"));
        Assert.IsFalse(MapRoomScanResults.RemoveFromSet(local, "missing"));

        Assert.AreEqual(1, local.Count);
        Assert.AreEqual("remaining", local.Single().uniqueId);
    }

    [DataTestMethod]
    [DataRow(false, false, true)]
    [DataRow(false, true, true)]
    [DataRow(true, true, true)]
    [DataRow(true, false, false)]
    public void InitialMetadataAppliesStoppedStateEvenWhenTargetAlreadyMatches(bool metadataInitialized, bool targetChanged, bool expected)
    {
        Assert.AreEqual(expected, MapRoomMetadataProcessor.ShouldApplyScanningState(metadataInitialized, targetChanged));
    }

    [DataTestMethod]
    [DataRow(0L, 1L, true, 1, 0, true, true)]
    [DataRow(1L, 1L, true, 1, 0, true, false)]
    [DataRow(0L, 1L, false, 1, 0, true, false)]
    [DataRow(0L, 1L, true, 0, 0, true, false)]
    [DataRow(0L, 1L, true, 1, 0, false, false)]
    public void RepublishesProgressDiscoveredBeforeTargetAcceptance(long previousGeneration, long acceptedGeneration, bool targetAlreadySelected,
        int localProgress, int acceptedProgress, bool hasOwnership, bool expected)
    {
        Assert.AreEqual(expected, MapRoomScanResults.ShouldRepublishProgress(previousGeneration, acceptedGeneration, targetAlreadySelected,
            localProgress, acceptedProgress, hasOwnership));
    }

    private static ResourceTrackerDatabase.ResourceInfo Info(string id, float x) => new()
    {
        uniqueId = id,
        techType = TechType.Quartz,
        position = new UnityEngine.Vector3(x, 0f, 0f)
    };
}
