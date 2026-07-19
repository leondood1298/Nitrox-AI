using Nitrox.Model.DataStructures;
using Nitrox.Model.DataStructures.Unity;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Bases;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;
using Nitrox.Model.Subnautica.Packets;
using Microsoft.Extensions.Logging.Abstractions;
using Nitrox.Server.Subnautica.Models.Packets.Processors;

namespace Nitrox.Server.Subnautica.Models.GameLogic.Entities;

[TestClass]
public sealed class MapRoomScanResultAuthorityTest
{
    private static readonly NitroxTechType quartz = new("Quartz");
    private static readonly NitroxTechType titanium = new("Titanium");

    [TestMethod]
    public void AddUpdateRemoveMutateCanonicalResults()
    {
        MapRoomEntity room = CreateRoom();

        Assert.IsTrue(Apply(room, Change(room, "resource", 1f)));
        Assert.IsTrue(Apply(room, Change(room, "resource", 2f)));
        Assert.AreEqual(1, room.ScanResults.Count);
        Assert.AreEqual(new NitroxVector3(2f, 0f, 0f), room.ScanResults[0].Position);
        Assert.IsTrue(Apply(room, Change(room, "resource", 0f, removed: true)));
        Assert.AreEqual(0, room.ScanResults.Count);
    }

    [TestMethod]
    public void RejectsStaleGenerationWrongTargetAndDuplicateReplay()
    {
        MapRoomEntity room = CreateRoom();
        MapRoomScanResultChanged accepted = Change(room, "resource", 1f);
        Assert.IsTrue(Apply(room, accepted));

        Assert.IsFalse(Apply(room, accepted));
        Assert.IsFalse(Apply(room, new MapRoomScanResultChanged(room.Id, 4, "stale", quartz, NitroxVector3.Zero)));
        Assert.IsFalse(Apply(room, new MapRoomScanResultChanged(room.Id, 5, "wrong", titanium, NitroxVector3.Zero)));
        Assert.AreEqual(1, room.ScanResults.Count);
    }

    [TestMethod]
    public void RejectsEmptyIdServerResponseAndUnknownRemoval()
    {
        MapRoomEntity room = CreateRoom();

        Assert.IsFalse(Apply(room, Change(room, "", 1f)));
        Assert.IsFalse(Apply(room, new MapRoomScanResultChanged(new NitroxId(), 5, "wrong-room", quartz, NitroxVector3.Zero)));
        Assert.IsFalse(Apply(room, new MapRoomScanResultChanged(room.Id, 5, "server", quartz, NitroxVector3.Zero, isServerResponse: true)));
        Assert.IsFalse(Apply(room, Change(room, "missing", 0f, removed: true)));
    }

    [TestMethod]
    public void RejectsOversizedIdsAndNonFinitePositions()
    {
        MapRoomEntity room = CreateRoom();
        string oversizedId = new('x', 257);

        Assert.IsFalse(Apply(room, new MapRoomScanResultChanged(room.Id, 5, oversizedId, quartz, NitroxVector3.Zero)));
        Assert.IsFalse(Apply(room, new MapRoomScanResultChanged(room.Id, 5, "nan", quartz, new NitroxVector3(float.NaN, 0f, 0f))));
        Assert.IsFalse(Apply(room, new MapRoomScanResultChanged(room.Id, 5, "infinity", quartz, new NitroxVector3(0f, float.PositiveInfinity, 0f))));
        Assert.IsTrue(Apply(room, new MapRoomScanResultChanged(room.Id, 5, "remove", quartz, NitroxVector3.Zero)));
        Assert.IsTrue(Apply(room, new MapRoomScanResultChanged(room.Id, 5, "remove", quartz, new NitroxVector3(float.NaN, 0f, 0f), removed: true)));
        Assert.AreEqual(0, room.ScanResults.Count);
    }

    [TestMethod]
    public void SnapshotAtomicallyReplacesResultsWithOneRevision()
    {
        MapRoomEntity room = CreateRoom();
        room.TryApplyScanResult(5, new MapRoomScanResultRecord("old", quartz, NitroxVector3.Zero));
        long revision = room.ScanResultRevision;
        MapRoomScanResultSnapshot snapshot = new(room.Id, 5,
        [
            new MapRoomScanResultRecord("first", quartz, new NitroxVector3(1f, 0f, 0f)),
            new MapRoomScanResultRecord("second", quartz, new NitroxVector3(2f, 0f, 0f))
        ], NitroxVector3.Zero, 300f);

        Assert.IsTrue(MapRoomScanResultAuthority.TryApplySnapshot(room, snapshot, [], NitroxVector3.Zero, out List<MapRoomScanResultRecord> accepted, out long acceptedRevision));

        CollectionAssert.AreEqual(new[] { "first", "second" }, accepted.Select(result => result.ResourceId).ToArray());
        Assert.AreEqual(revision + 1, acceptedRevision);
        Assert.AreEqual(2, room.ScanResults.Count);
        Assert.IsTrue(MapRoomScanResultAuthority.TryApplySnapshot(room, snapshot, [], NitroxVector3.Zero, out List<MapRoomScanResultRecord> replay, out long replayRevision));
        Assert.AreEqual(acceptedRevision, replayRevision);
        CollectionAssert.AreEqual(accepted.Select(result => result.ResourceId).ToArray(), replay.Select(result => result.ResourceId).ToArray());
    }

    [TestMethod]
    public void SnapshotRejectsDuplicatesWrongTargetAndStaleGenerationWithoutMutation()
    {
        MapRoomEntity room = CreateRoom();
        List<MapRoomScanResultRecord> duplicate = [new("same", quartz, NitroxVector3.Zero), new("same", quartz, NitroxVector3.One)];
        List<MapRoomScanResultRecord> wrongTarget = [new("wrong", titanium, NitroxVector3.Zero)];

        Assert.IsFalse(MapRoomScanResultAuthority.TryApplySnapshot(room, Snapshot(room, duplicate), [], NitroxVector3.Zero, out _, out _));
        Assert.IsFalse(MapRoomScanResultAuthority.TryApplySnapshot(room, Snapshot(room, wrongTarget), [], NitroxVector3.Zero, out _, out _));
        Assert.IsFalse(MapRoomScanResultAuthority.TryApplySnapshot(room, Snapshot(room, [], generation: 4), [], NitroxVector3.Zero, out _, out _));
        Assert.AreEqual(0, room.ScanResults.Count);
    }

    [TestMethod]
    public void SnapshotRejectsInvalidIdsAndPositionsWithoutPartialMutation()
    {
        MapRoomEntity room = CreateRoom();
        List<MapRoomScanResultRecord> invalid =
        [
            new("valid", quartz, NitroxVector3.Zero),
            new(new string('x', 257), quartz, NitroxVector3.One),
            new("nan", quartz, new NitroxVector3(0f, 0f, float.NaN))
        ];

        Assert.IsFalse(MapRoomScanResultAuthority.TryApplySnapshot(room, Snapshot(room, invalid), [], NitroxVector3.Zero, out _, out _));
        Assert.AreEqual(0, room.ScanResults.Count);
    }

    [TestMethod]
    public void DeltaAdditionRequiresValidatedInstalledRangeQuery()
    {
        MapRoomEntity room = CreateRoom();
        MapRoomScanResultChanged valid = new(room.Id, 5, "valid", quartz, new NitroxVector3(100f, 0f, 0f),
            scanOrigin: NitroxVector3.Zero, scanRange: 300f);

        Assert.IsFalse(MapRoomScanResultAuthority.TryApply(room, valid, Registry(), null, out _));
        Assert.IsTrue(Apply(room, valid));
        Assert.IsFalse(Apply(room, new MapRoomScanResultChanged(room.Id, 5, "outside", quartz, new NitroxVector3(301f, 0f, 0f),
            scanOrigin: NitroxVector3.Zero, scanRange: 300f)));
        Assert.IsFalse(Apply(room, new MapRoomScanResultChanged(room.Id, 5, "remote", quartz, new NitroxVector3(600f, 0f, 0f),
            scanOrigin: new NitroxVector3(513f, 0f, 0f), scanRange: 300f)));
        Assert.IsFalse(Apply(room, new MapRoomScanResultChanged(room.Id, 5, "uninstalled-range", quartz, new NitroxVector3(340f, 0f, 0f),
            scanOrigin: NitroxVector3.Zero, scanRange: 350f)));
        room.ChildEntities.Add(RangeUpgrade());
        Assert.IsTrue(Apply(room, new MapRoomScanResultChanged(room.Id, 5, "installed-range", quartz, new NitroxVector3(340f, 0f, 0f),
            scanOrigin: NitroxVector3.Zero, scanRange: 350f)));
    }

    [TestMethod]
    public void DeltaAdditionCapRejectsNewIdButAllowsExistingUpdate()
    {
        MapRoomEntity room = CreateRoom();
        room.ScanResults = Enumerable.Range(0, MapRoomScanResultAuthority.MAX_SNAPSHOT_RESULTS)
                                     .Select(index => new MapRoomScanResultRecord($"resource-{index}", quartz, NitroxVector3.Zero))
                                     .ToList();

        Assert.IsFalse(Apply(room, new MapRoomScanResultChanged(room.Id, 5, "overflow", quartz, new NitroxVector3(1f, 0f, 0f),
            scanOrigin: NitroxVector3.Zero, scanRange: 300f)));
        Assert.IsTrue(Apply(room, new MapRoomScanResultChanged(room.Id, 5, "resource-0", quartz, new NitroxVector3(2f, 0f, 0f),
            scanOrigin: NitroxVector3.Zero, scanRange: 300f)));
        Assert.AreEqual(MapRoomScanResultAuthority.MAX_SNAPSHOT_RESULTS, room.ScanResults.Count);
        Assert.AreEqual(new NitroxVector3(2f, 0f, 0f), room.ScanResults[0].Position);
    }

    [TestMethod]
    public void ExactDeltaUsesAuthoritativeWorldPositionWhileOverrideUsesReportedPosition()
    {
        MapRoomEntity room = CreateRoom();
        NitroxId exactId = new();
        EntityRegistry registry = Registry(World(exactId, quartz, new NitroxVector3(25f, 0f, 0f)));

        Assert.IsTrue(Apply(room, new MapRoomScanResultChanged(room.Id, 5, exactId.ToString(), quartz,
            new NitroxVector3(900f, 0f, 0f), scanOrigin: NitroxVector3.Zero, scanRange: 300f), registry));
        Assert.AreEqual(new NitroxVector3(25f, 0f, 0f), room.ScanResults.Single().Position);

        NitroxId outsideId = new();
        registry.AddEntity(World(outsideId, quartz, new NitroxVector3(301f, 0f, 0f)));
        Assert.IsFalse(Apply(room, new MapRoomScanResultChanged(room.Id, 5, outsideId.ToString(), quartz,
            new NitroxVector3(10f, 0f, 0f), scanOrigin: NitroxVector3.Zero, scanRange: 300f), registry));

        Assert.IsTrue(Apply(room, new MapRoomScanResultChanged(room.Id, 5, "override", quartz,
            new NitroxVector3(40f, 0f, 0f), scanOrigin: NitroxVector3.Zero, scanRange: 300f), registry));
        Assert.AreEqual(new NitroxVector3(40f, 0f, 0f), room.ScanResults.Single(result => result.ResourceId == "override").Position);
    }

    [TestMethod]
    public void TrackerUnloadCannotRemoveLiveExactResultButOwnerOnlyOverrideCanBeRemoved()
    {
        MapRoomEntity room = CreateRoom();
        NitroxId exactId = new();
        room.TryApplyScanResult(5, new MapRoomScanResultRecord(exactId.ToString(), quartz, new NitroxVector3(10f, 0f, 0f)));
        EntityRegistry exactRegistry = Registry(World(exactId, quartz, new NitroxVector3(10f, 0f, 0f)));
        MapRoomScanResultChanged unload = new(room.Id, 5, exactId.ToString(), quartz, new NitroxVector3(10f, 0f, 0f),
            removed: true, scanOrigin: NitroxVector3.Zero, scanRange: 300f);

        Assert.IsFalse(MapRoomScanResultAuthority.TryApply(room, unload, exactRegistry, NitroxVector3.Zero, out bool correctionRequired));
        Assert.IsTrue(correctionRequired);
        Assert.AreEqual(1, room.ScanResults.Count);
        MapRoomScanResultSnapshot correction = MapRoomScanResultChangedProcessor.CreateCorrectionSnapshot(room, unload);
        Assert.IsTrue(correction.IsServerResponse && correction.Granted);
        Assert.AreEqual(room.ScanResultRevision, correction.Revision);
        CollectionAssert.AreEqual(new[] { exactId.ToString() }, correction.Results.Select(result => result.ResourceId).ToArray());

        MapRoomScanResultChanged invalidQuery = new(room.Id, 5, exactId.ToString(), quartz, new NitroxVector3(10f, 0f, 0f),
            removed: true, scanOrigin: new NitroxVector3(513f, 0f, 0f), scanRange: 300f);
        Assert.IsFalse(MapRoomScanResultAuthority.TryApply(room, invalidQuery, exactRegistry, NitroxVector3.Zero, out bool invalidCorrection));
        Assert.IsFalse(invalidCorrection);

        EntityRegistry overrideRegistry = Registry(World(exactId, titanium, new NitroxVector3(10f, 0f, 0f)));
        Assert.IsTrue(Apply(room, Change(room, exactId.ToString(), 10f, removed: true), overrideRegistry));
        Assert.AreEqual(0, room.ScanResults.Count);
    }

    [TestMethod]
    public void OrdinaryExactRemovalOutsideValidatedRangeRemovesCanonicalResult()
    {
        MapRoomEntity room = CreateRoom();
        NitroxId exactId = new();
        room.TryApplyScanResult(5, new MapRoomScanResultRecord(exactId.ToString(), quartz, new NitroxVector3(10f, 0f, 0f)));
        EntityRegistry registry = Registry(World(exactId, quartz, new NitroxVector3(301f, 0f, 0f)));
        MapRoomScanResultChanged removal = new(room.Id, 5, exactId.ToString(), quartz, new NitroxVector3(10f, 0f, 0f),
            removed: true, scanOrigin: NitroxVector3.Zero, scanRange: 300f);

        Assert.IsTrue(MapRoomScanResultAuthority.TryApply(room, removal, registry, NitroxVector3.Zero, out bool correctionRequired));
        Assert.IsFalse(correctionRequired);
        Assert.AreEqual(0, room.ScanResults.Count);
    }

    [TestMethod]
    public void RangeExitUsesAuthoritativeExactPositionAndRejectsForgedQuery()
    {
        MapRoomEntity room = CreateRoom();
        NitroxId outsideId = new();
        room.TryApplyScanResult(5, new MapRoomScanResultRecord(outsideId.ToString(), quartz, new NitroxVector3(250f, 0f, 0f)));
        EntityRegistry outsideRegistry = Registry(World(outsideId, quartz, new NitroxVector3(301f, 0f, 0f)));

        Assert.IsTrue(Apply(room, RangeExit(room, outsideId.ToString(), new NitroxVector3(1f, 0f, 0f)), outsideRegistry, NitroxVector3.Zero));
        Assert.AreEqual(0, room.ScanResults.Count);

        NitroxId insideId = new();
        room.TryApplyScanResult(5, new MapRoomScanResultRecord(insideId.ToString(), quartz, new NitroxVector3(100f, 0f, 0f)));
        EntityRegistry insideRegistry = Registry(World(insideId, quartz, new NitroxVector3(100f, 0f, 0f)));
        MapRoomScanResultChanged rejectedInside = RangeExit(room, insideId.ToString(), new NitroxVector3(301f, 0f, 0f));
        Assert.IsFalse(MapRoomScanResultAuthority.TryApply(room, rejectedInside, insideRegistry, NitroxVector3.Zero,
            out bool correctionRequired));
        Assert.IsTrue(correctionRequired);
        MapRoomScanResultChanged invalidQuery = RangeExit(room, insideId.ToString(), new NitroxVector3(301f, 0f, 0f),
            scanOrigin: new NitroxVector3(513f, 0f, 0f));
        Assert.IsFalse(MapRoomScanResultAuthority.TryApply(room, invalidQuery, insideRegistry, NitroxVector3.Zero,
            out bool invalidCorrection));
        Assert.IsFalse(invalidCorrection);
        Assert.IsFalse(Apply(room, RangeExit(room, insideId.ToString(), new NitroxVector3(301f, 0f, 0f)), insideRegistry));
        Assert.AreEqual(1, room.ScanResults.Count);
    }

    [TestMethod]
    public void OwnerOnlyRangeExitUsesReportedPositionAndInstalledRange()
    {
        MapRoomEntity room = CreateRoom();
        string overrideId = "override-result";
        room.TryApplyScanResult(5, new MapRoomScanResultRecord(overrideId, quartz, new NitroxVector3(250f, 0f, 0f)));

        Assert.IsFalse(Apply(room, RangeExit(room, overrideId, new NitroxVector3(100f, 0f, 0f)), scanAnchor: NitroxVector3.Zero));
        Assert.IsFalse(Apply(room, RangeExit(room, overrideId, new NitroxVector3(351f, 0f, 0f), scanRange: 350f), scanAnchor: NitroxVector3.Zero));
        Assert.IsTrue(Apply(room, RangeExit(room, overrideId, new NitroxVector3(301f, 0f, 0f)), scanAnchor: NitroxVector3.Zero));
        Assert.AreEqual(0, room.ScanResults.Count);
    }

    [TestMethod]
    public void SnapshotMergesExactWorldTypesAndKeepsOwnerOnlyResults()
    {
        MapRoomEntity room = CreateRoom();
        NitroxId exactId = new("00000000-0000-0000-0000-000000000001");
        List<MapRoomScanResultRecord> ownerResults = [new("override-result", quartz, new NitroxVector3(20f, 0f, 0f))];
        List<WorldEntity> worldEntities =
        [
            World(exactId, quartz, new NitroxVector3(10f, 0f, 0f)),
            World(new NitroxId(), titanium, new NitroxVector3(5f, 0f, 0f)),
            World(new NitroxId(), quartz, new NitroxVector3(301f, 0f, 0f))
        ];

        Assert.IsTrue(MapRoomScanResultAuthority.TryApplySnapshot(room, Snapshot(room, ownerResults), worldEntities, NitroxVector3.Zero,
            out List<MapRoomScanResultRecord> accepted, out _));

        CollectionAssert.AreEqual(new[] { exactId.ToString(), "override-result" }, accepted.Select(result => result.ResourceId).ToArray());
    }

    [TestMethod]
    public void StopSnapshotNeverSupplementsWorldEntitiesWithNoneTechType()
    {
        MapRoomEntity room = CreateRoom();
        room.Metadata = new MapRoomMetadata(NitroxTechType.None, 0, 5, 9);
        List<WorldEntity> worldEntities = [World(new NitroxId(), NitroxTechType.None, new NitroxVector3(10f, 0f, 0f))];

        Assert.IsTrue(MapRoomScanResultAuthority.TryApplySnapshot(room, Snapshot(room, []), worldEntities, NitroxVector3.Zero,
            out List<MapRoomScanResultRecord> accepted, out _));
        Assert.IsFalse(MapRoomScanResultAuthority.TryApplySnapshot(room,
            Snapshot(room, [new MapRoomScanResultRecord("forged-none", NitroxTechType.None, new NitroxVector3(1f, 0f, 0f))]),
            worldEntities, NitroxVector3.Zero, out _, out _));
        Assert.IsFalse(Apply(room, new MapRoomScanResultChanged(room.Id, 5, "delta-none", NitroxTechType.None,
            new NitroxVector3(1f, 0f, 0f), scanOrigin: NitroxVector3.Zero, scanRange: 300f)));

        Assert.AreEqual(0, accepted.Count);
        Assert.AreEqual(0, room.ScanResults.Count);
    }

    [TestMethod]
    public void SnapshotRejectsInvalidQueryAndOwnerResultOutsideRange()
    {
        MapRoomEntity room = CreateRoom();

        Assert.IsFalse(MapRoomScanResultAuthority.TryApplySnapshot(room,
            new MapRoomScanResultSnapshot(room.Id, 5, [], new NitroxVector3(float.NaN, 0f, 0f), 300f), [], NitroxVector3.Zero, out _, out _));
        Assert.IsFalse(MapRoomScanResultAuthority.TryApplySnapshot(room,
            new MapRoomScanResultSnapshot(room.Id, 5, [], NitroxVector3.Zero, 299f), [], NitroxVector3.Zero, out _, out _));
        Assert.IsFalse(MapRoomScanResultAuthority.TryApplySnapshot(room,
            new MapRoomScanResultSnapshot(room.Id, 5, [], new NitroxVector3(513f, 0f, 0f), 300f), [], NitroxVector3.Zero, out _, out _));
        Assert.IsFalse(MapRoomScanResultAuthority.TryApplySnapshot(room,
            new MapRoomScanResultSnapshot(room.Id, 5, [], NitroxVector3.Zero, 350f), [], NitroxVector3.Zero, out _, out _));
        room.ChildEntities.Add(RangeUpgrade());
        Assert.IsTrue(MapRoomScanResultAuthority.TryApplySnapshot(room,
            new MapRoomScanResultSnapshot(room.Id, 5, [], NitroxVector3.Zero, 350.005f), [], NitroxVector3.Zero, out _, out _));
        Assert.IsFalse(MapRoomScanResultAuthority.TryApplySnapshot(room,
            new MapRoomScanResultSnapshot(room.Id, 5, [], NitroxVector3.Zero, 400f), [], NitroxVector3.Zero, out _, out _));
        room.ChildEntities.AddRange([RangeUpgrade(), RangeUpgrade(), RangeUpgrade()]);
        Assert.IsTrue(MapRoomScanResultAuthority.TryApplySnapshot(room,
            new MapRoomScanResultSnapshot(room.Id, 5, [], NitroxVector3.Zero, 500f), [], NitroxVector3.Zero, out _, out _));
        Assert.IsFalse(MapRoomScanResultAuthority.TryApplySnapshot(room,
            Snapshot(room, [new MapRoomScanResultRecord("outside", quartz, new NitroxVector3(301f, 0f, 0f))]), [], NitroxVector3.Zero, out _, out _));
    }

    [TestMethod]
    public void EntityRemovalInvalidatesMatchingResultsAcrossRooms()
    {
        NitroxId resourceId = new();
        MapRoomEntity first = CreateRoom();
        MapRoomEntity second = CreateRoom();
        MapRoomEntity unaffected = CreateRoom();
        first.TryApplyScanResult(5, new MapRoomScanResultRecord(resourceId.ToString(), quartz, NitroxVector3.Zero));
        second.TryApplyScanResult(5, new MapRoomScanResultRecord(resourceId.ToString(), quartz, NitroxVector3.One));
        unaffected.TryApplyScanResult(5, new MapRoomScanResultRecord("other", quartz, NitroxVector3.Zero));
        long firstRevision = first.ScanResultRevision;

        List<MapRoomScanResultChanged> removals = MapRoomScanResultAuthority.InvalidateResource([first, second, unaffected], resourceId);

        Assert.AreEqual(2, removals.Count);
        Assert.AreEqual(firstRevision + 1, first.ScanResultRevision);
        Assert.IsTrue(removals.All(packet => packet.Removed && !packet.IsRangeExit && packet.IsServerResponse && packet.Granted &&
            packet.ResourceId == resourceId.ToString()));
        Assert.AreEqual(0, first.ScanResults.Count);
        Assert.AreEqual(0, second.ScanResults.Count);
        Assert.AreEqual(1, unaffected.ScanResults.Count);
    }

    [TestMethod]
    public void UnknownEntityRemovalDoesNotMutateResultsOrRevisions()
    {
        MapRoomEntity room = CreateRoom();
        room.TryApplyScanResult(5, new MapRoomScanResultRecord("known", quartz, NitroxVector3.Zero));
        long revision = room.ScanResultRevision;

        List<MapRoomScanResultChanged> removals = MapRoomScanResultAuthority.InvalidateResource([room], new NitroxId());

        Assert.AreEqual(0, removals.Count);
        Assert.AreEqual(revision, room.ScanResultRevision);
        Assert.AreEqual(1, room.ScanResults.Count);
    }

    private static MapRoomEntity CreateRoom()
    {
        MapRoomEntity room = new(new NitroxId(), new NitroxId(), new NitroxInt3())
        {
            Metadata = new MapRoomMetadata(quartz, 0, 5, 9)
        };
        room.BeginScanResultGeneration(5);
        return room;
    }

    private static MapRoomScanResultChanged Change(MapRoomEntity room, string resourceId, float x, bool removed = false) =>
        new(room.Id, 5, resourceId, quartz, new NitroxVector3(x, 0f, 0f), removed);

    private static MapRoomScanResultChanged RangeExit(MapRoomEntity room, string resourceId, NitroxVector3 position,
        NitroxVector3? scanOrigin = null, float scanRange = 300f) =>
        new(room.Id, 5, resourceId, quartz, position, removed: true, isRangeExit: true,
            scanOrigin: scanOrigin ?? NitroxVector3.Zero, scanRange: scanRange);

    private static bool Apply(MapRoomEntity room, MapRoomScanResultChanged change, EntityRegistry? registry = null,
        NitroxVector3? scanAnchor = null) =>
        MapRoomScanResultAuthority.TryApply(room, change, registry ?? Registry(), scanAnchor ?? (!change.Removed ? NitroxVector3.Zero : null), out _);

    private static EntityRegistry Registry(params WorldEntity[] entities)
    {
        EntityRegistry registry = new(NullLogger<EntityRegistry>.Instance);
        registry.AddEntities(entities);
        return registry;
    }

    private static MapRoomScanResultSnapshot Snapshot(MapRoomEntity room, List<MapRoomScanResultRecord> results, long generation = 5) =>
        new(room.Id, generation, results, NitroxVector3.Zero, 300f);

    private static WorldEntity World(NitroxId id, NitroxTechType techType, NitroxVector3 position) =>
        new(position, NitroxQuaternion.Identity, NitroxVector3.One, techType, 0, id.ToString(), true, id, null);

    private static WorldEntity RangeUpgrade() => World(new NitroxId(), new NitroxTechType("MapRoomUpgradeScanRange"), NitroxVector3.Zero);
}
