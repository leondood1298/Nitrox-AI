using Nitrox.Model.DataStructures.Unity;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Bases;
using Nitrox.Model.Subnautica.Packets;
using NitroxClient.GameLogic.Spawning.Metadata.Processor;
using NitroxPatcher.Patches.Dynamic;

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
    public void CanonicalCorrectionSnapshotRestoresLocallyUnloadedResult()
    {
        List<ResourceTrackerDatabase.ResourceInfo> local = [Info("server-exact", 10f)];
        Assert.IsTrue(MapRoomScanResults.RemoveFromList(local, "server-exact"));
        Assert.AreEqual(0, local.Count);

        MapRoomScanResults.ReconcileSnapshot(local,
        [
            new MapRoomScanResultRecord("server-exact", quartz, new NitroxVector3(10f, 0f, 0f))
        ]);

        Assert.AreEqual(1, local.Count);
        Assert.AreEqual("server-exact", local[0].uniqueId);
    }

    [TestMethod]
    public void DeltaAddsUpdatesAndRemovesByStableId()
    {
        List<ResourceTrackerDatabase.ResourceInfo> local = [Info("resource", 1f), Info("resource", 1f)];
        Nitrox.Model.DataStructures.NitroxId roomId = new();

        MapRoomScanResults.ApplyDeltaToList(local, new MapRoomScanResultChanged(roomId, 1, "resource", quartz, new NitroxVector3(4f, 0f, 0f)));
        MapRoomScanResults.ApplyDeltaToList(local, new MapRoomScanResultChanged(roomId, 1, "new", quartz, new NitroxVector3(2f, 0f, 0f)));
        MapRoomScanResults.ApplyDeltaToList(local, new MapRoomScanResultChanged(roomId, 1, "resource", quartz, NitroxVector3.Zero, removed: true));

        Assert.AreEqual(1, local.Count);
        Assert.AreEqual("new", local[0].uniqueId);
    }

    [TestMethod]
    public void LiveDiscoveryReplacesSyntheticStableIdAndCanonicalEchoPreservesLiveReference()
    {
        ResourceTrackerDatabase.ResourceInfo synthetic = Info("hybrid", 1f);
        ResourceTrackerDatabase.ResourceInfo live = Info("hybrid", 2f);
        List<ResourceTrackerDatabase.ResourceInfo> local = [synthetic, live];

        Assert.IsTrue(MapRoomScanResults.UpsertLiveDiscoveredResource(local, live));
        Assert.AreEqual(1, local.Count);
        Assert.AreSame(live, local[0]);
        Assert.AreEqual(2f, local[0].position.x);

        MapRoomScanResults.ApplyDeltaToList(local,
            new MapRoomScanResultChanged(new Nitrox.Model.DataStructures.NitroxId(), 1, "hybrid", quartz,
                new NitroxVector3(3f, 0f, 0f)));

        Assert.AreEqual(1, local.Count);
        Assert.AreSame(live, local[0], "The accepted server echo must update rather than replace the live object.");
        Assert.AreEqual(3f, live.position.x);
        Assert.AreEqual(TechType.Quartz, live.techType);
    }

    [TestMethod]
    public void InitializedNonOwnerDiscoveryLeavesCanonicalObjectAndCoordinatesUntouched()
    {
        ResourceTrackerDatabase.ResourceInfo canonical = Info("canonical", 1f);
        ResourceTrackerDatabase.ResourceInfo newlyLive = Info("canonical", 9f);
        List<ResourceTrackerDatabase.ResourceInfo> local = [canonical];

        bool runVanilla = MapRoomScanResultBroadcaster.ShouldRunVanillaResults(
            resultStateInitialized: true, hasAuthority: false);
        if (runVanilla)
        {
            local.Add(newlyLive);
        }

        Assert.IsFalse(runVanilla);
        Assert.AreEqual(1, local.Count);
        Assert.AreSame(canonical, local[0]);
        Assert.AreEqual(1f, canonical.position.x);
    }

    [TestMethod]
    public void InitializedNonOwnerRemovalLeavesCanonicalObjectPresent()
    {
        ResourceTrackerDatabase.ResourceInfo canonical = Info("canonical", 1f);
        List<ResourceTrackerDatabase.ResourceInfo> local = [canonical];

        bool runVanilla = MapRoomScanResultBroadcaster.ShouldRunVanillaResults(
            resultStateInitialized: true, hasAuthority: false);
        if (runVanilla)
        {
            local.Remove(canonical);
        }

        Assert.IsFalse(runVanilla);
        Assert.AreEqual(1, local.Count);
        Assert.AreSame(canonical, local[0]);
    }

    [DataTestMethod]
    [DataRow(false, false, true)]
    [DataRow(false, true, true)]
    [DataRow(true, false, false)]
    [DataRow(true, true, true)]
    public void VanillaResultMutationRunsBeforeInitializationOrForOwner(bool initialized, bool owner, bool expected)
    {
        Assert.AreEqual(expected, MapRoomScanResultBroadcaster.ShouldRunVanillaResults(initialized, owner));
    }

    [TestMethod]
    public void OwnerOutOfRangeEvictionRemovesAllStableIdDuplicates()
    {
        ResourceTrackerDatabase.ResourceInfo discovered = Info("outside", 400f);
        List<ResourceTrackerDatabase.ResourceInfo> local =
        [
            Info("outside", 1f),
            discovered,
            Info("inside", 2f)
        ];

        Assert.IsTrue(MapRoomScanResults.EvictDiscoveredResourceFromList(local, discovered));
        Assert.AreEqual(1, local.Count);
        Assert.AreEqual("inside", local[0].uniqueId);
    }

    [TestMethod]
    public void InvalidEmptyIdEvictionFallsBackToLiveReference()
    {
        ResourceTrackerDatabase.ResourceInfo invalid = Info(string.Empty, 1f);
        ResourceTrackerDatabase.ResourceInfo otherInvalid = Info(string.Empty, 2f);
        List<ResourceTrackerDatabase.ResourceInfo> local = [otherInvalid, invalid];

        Assert.IsTrue(MapRoomScanResults.EvictDiscoveredResourceFromList(local, invalid));
        Assert.AreEqual(1, local.Count);
        Assert.AreSame(otherInvalid, local[0]);
    }

    [TestMethod]
    public void LocalPickupRemovesMatchingStableIdOnly()
    {
        List<ResourceTrackerDatabase.ResourceInfo> local = [Info("collected", 1f), Info("collected", 1f), Info("remaining", 2f)];

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

    [TestMethod]
    public void LocalPickupFallsBackToMatchingTypeAndPositionWhenStableIdChanged()
    {
        List<ResourceTrackerDatabase.ResourceInfo> local =
        [
            Info("legacy-id", 1f),
            Info("nearby-resource", 1.2f),
            Info("same-id-elsewhere", 3f)
        ];

        Assert.IsTrue(MapRoomScanResults.RemoveFromList(local, "same-id-elsewhere", TechType.Quartz, new UnityEngine.Vector3(1f, 0f, 0f)));

        Assert.AreEqual(1, local.Count);
        Assert.AreEqual("nearby-resource", local[0].uniqueId);
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

    [TestMethod]
    public void ScanCancellationPublishesImmediately()
    {
        Assert.IsTrue(MapRoomFunctionality_StartScanning_Patch.ShouldPublishImmediately(TechType.None));
        Assert.IsFalse(MapRoomFunctionality_StartScanning_Patch.ShouldPublishImmediately(TechType.ShaleChunk));
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
