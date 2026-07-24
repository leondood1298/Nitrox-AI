using Nitrox.Model.DataStructures;
using Nitrox.Model.DataStructures.Unity;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Bases;
using NitroxClient.GameLogic;
using Microsoft.Extensions.Logging.Abstractions;

namespace Nitrox.Server.Subnautica.Models.GameLogic.Entities;

[TestClass]
public sealed class MapRoomWorldResourceIndexTest
{
    private static readonly NitroxTechType quartz = new("Quartz");
    private static readonly NitroxTechType titanium = new("Titanium");

    [TestMethod]
    public void ResultsUseServerPositionForDuplicateAndPreserveOwnerOnlyOverrides()
    {
        NitroxId duplicateId = new("00000000-0000-0000-0000-000000000001");
        NitroxId secondId = new("00000000-0000-0000-0000-000000000002");
        List<MapRoomScanResultRecord> ownerResults =
        [
            new(duplicateId.ToString(), quartz, new NitroxVector3(99f, 0f, 0f)),
            new("override-result", quartz, new NitroxVector3(20f, 0f, 0f))
        ];
        List<WorldEntity> worldEntities =
        [
            World(duplicateId, quartz, new NitroxVector3(5f, 0f, 0f)),
            World(secondId, quartz, new NitroxVector3(10f, 0f, 0f)),
            World(new NitroxId(), titanium, new NitroxVector3(1f, 0f, 0f)),
            World(new NitroxId(), quartz, new NitroxVector3(301f, 0f, 0f))
        ];

        List<MapRoomScanResultRecord> merged = MapRoomWorldResourceIndex.MergeResults(ownerResults, worldEntities, quartz,
            NitroxVector3.Zero, 300f, 100);

        CollectionAssert.AreEqual(new[] { duplicateId.ToString(), secondId.ToString(), "override-result" },
            merged.Select(result => result.ResourceId).ToArray());
        Assert.AreEqual(new NitroxVector3(5f, 0f, 0f), merged[0].Position);
    }

    [TestMethod]
    public void ExactWorldResultOutsideRangeRemovesSpoofedInRangeOwnerRecord()
    {
        NitroxId exactId = new();
        List<MapRoomScanResultRecord> ownerResults =
        [
            new(exactId.ToString(), quartz, new NitroxVector3(10f, 0f, 0f)),
            new("override-result", quartz, new NitroxVector3(20f, 0f, 0f))
        ];

        List<MapRoomScanResultRecord> merged = MapRoomWorldResourceIndex.MergeResults(ownerResults,
            [World(exactId, quartz, new NitroxVector3(301f, 0f, 0f))], quartz, NitroxVector3.Zero, 300f, 100);

        CollectionAssert.AreEqual(new[] { "override-result" }, merged.Select(result => result.ResourceId).ToArray());
    }

    [TestMethod]
    public void ResultsAreDistanceThenIdOrderedAndCapped()
    {
        NitroxId laterId = new("00000000-0000-0000-0000-000000000002");
        NitroxId earlierId = new("00000000-0000-0000-0000-000000000001");
        List<WorldEntity> worldEntities =
        [
            World(laterId, quartz, new NitroxVector3(-10f, 0f, 0f)),
            World(earlierId, quartz, new NitroxVector3(10f, 0f, 0f)),
            World(new NitroxId("00000000-0000-0000-0000-000000000003"), quartz, new NitroxVector3(20f, 0f, 0f))
        ];

        List<MapRoomScanResultRecord> merged = MapRoomWorldResourceIndex.MergeResults([], worldEntities, quartz,
            NitroxVector3.Zero, 300f, 2);

        CollectionAssert.AreEqual(new[] { earlierId.ToString(), laterId.ToString() }, merged.Select(result => result.ResourceId).ToArray());
    }

    [TestMethod]
    public void ResultCapPreservesOwnerOnlyOverrideMappings()
    {
        NitroxId closestId = new("00000000-0000-0000-0000-000000000001");
        List<MapRoomScanResultRecord> ownerResults = [new("override-result", quartz, new NitroxVector3(290f, 0f, 0f))];
        List<WorldEntity> worldEntities =
        [
            World(closestId, quartz, new NitroxVector3(1f, 0f, 0f)),
            World(new NitroxId("00000000-0000-0000-0000-000000000002"), quartz, new NitroxVector3(2f, 0f, 0f))
        ];

        List<MapRoomScanResultRecord> merged = MapRoomWorldResourceIndex.MergeResults(ownerResults, worldEntities, quartz,
            NitroxVector3.Zero, 300f, 2);

        CollectionAssert.AreEqual(new[] { closestId.ToString(), "override-result" }, merged.Select(result => result.ResourceId).ToArray());
    }

    [TestMethod]
    public void ScanTypesAddOnlyDetectableExactTypesWithinRange()
    {
        NitroxTechType leadOverride = new("Lead");
        NitroxTechType shale = new("ShaleChunk");
        List<WorldEntity> worldEntities =
        [
            World(new NitroxId(), quartz, new NitroxVector3(300f, 0f, 0f)),
            World(new NitroxId(), titanium, new NitroxVector3(10f, 0f, 0f)),
            World(new NitroxId(), shale, new NitroxVector3(301f, 0f, 0f))
        ];

        List<NitroxTechType> merged = MapRoomWorldResourceIndex.MergeScanTypes([leadOverride], [quartz, shale], worldEntities,
            NitroxVector3.Zero, 300f);

        CollectionAssert.AreEqual(new[] { leadOverride, quartz }, merged);
    }

    [TestMethod]
    public void QueryValidationAllowsInitializationSentinelAndClampsTinyMinimumDrift()
    {
        NitroxVector3 anchor = new(100f, 20f, -50f);
        Assert.IsTrue(MapRoomWorldResourceIndex.TryNormalizeQuery(NitroxVector3.Zero, 0f, anchor, out NitroxVector3 initializedOrigin, out float initialized));
        Assert.AreEqual(anchor, initializedOrigin);
        Assert.AreEqual(300f, initialized);
        Assert.IsTrue(MapRoomWorldResourceIndex.TryNormalizeQuery(NitroxVector3.Zero, 300f, anchor, out NitroxVector3 worldZeroOrigin, out _));
        Assert.AreEqual(NitroxVector3.Zero, worldZeroOrigin);
        Assert.IsTrue(MapRoomWorldResourceIndex.TryNormalizeQuery(anchor, 299.995f, anchor, out _, out float clamped));
        Assert.AreEqual(300f, clamped);
        Assert.IsTrue(MapRoomWorldResourceIndex.TryNormalizeQuery(anchor, 500f, anchor, out _, out float maximum));
        Assert.AreEqual(500f, maximum);
        Assert.IsFalse(MapRoomWorldResourceIndex.TryNormalizeQuery(anchor, 299f, anchor, out _, out _));
        Assert.IsFalse(MapRoomWorldResourceIndex.TryNormalizeQuery(anchor, 500.01f, anchor, out _, out _));
        Assert.IsFalse(MapRoomWorldResourceIndex.TryNormalizeQuery(new NitroxVector3(float.NaN, 0f, 0f), 300f, anchor, out _, out _));
        Assert.IsFalse(MapRoomWorldResourceIndex.TryNormalizeQuery(anchor, float.PositiveInfinity, anchor, out _, out _));
        Assert.IsFalse(MapRoomWorldResourceIndex.TryNormalizeQuery(anchor + new NitroxVector3(513f, 0f, 0f), 300f, anchor, out _, out _));
    }

    [TestMethod]
    public void MaximumRangeComesFromServerRoomUpgradeItems()
    {
        MapRoomEntity room = new(new NitroxId(), new NitroxId(), new NitroxInt3());
        Assert.AreEqual(300f, MapRoomWorldResourceIndex.GetMaximumScanRange(room));

        room.ChildEntities.Add(RangeUpgrade());
        Assert.AreEqual(350f, MapRoomWorldResourceIndex.GetMaximumScanRange(room));
        Assert.IsTrue(MapRoomWorldResourceIndex.IsRangeAllowed(room, 350.005f));
        Assert.IsFalse(MapRoomWorldResourceIndex.IsRangeAllowed(room, 350.02f));

        room.ChildEntities.AddRange([RangeUpgrade(), RangeUpgrade(), RangeUpgrade()]);
        Assert.AreEqual(500f, MapRoomWorldResourceIndex.GetMaximumScanRange(room));
        room.ChildEntities.Add(RangeUpgrade());
        Assert.AreEqual(500f, MapRoomWorldResourceIndex.GetMaximumScanRange(room));
    }

    [TestMethod]
    public void MaximumRangeTraversalHandlesCyclesNullsAndDuplicateIds()
    {
        MapRoomEntity room = new(new NitroxId(), new NitroxId(), new NitroxInt3());
        WorldEntity cycle = World(new NitroxId(), new NitroxTechType("Container"), NitroxVector3.Zero);
        NitroxId duplicateId = new();
        WorldEntity countedUpgrade = World(duplicateId, new NitroxTechType("MapRoomUpgradeScanRange"), NitroxVector3.Zero);
        WorldEntity duplicateUpgrade = World(duplicateId, new NitroxTechType("MapRoomUpgradeScanRange"), NitroxVector3.Zero);
        duplicateUpgrade.ChildEntities = null!;
        cycle.ChildEntities.AddRange([cycle, countedUpgrade, null!]);
        room.ChildEntities.AddRange([cycle, duplicateUpgrade, null!]);

        Assert.AreEqual(350f, MapRoomWorldResourceIndex.GetMaximumScanRange(room));
    }

    [TestMethod]
    public void MaximumRangeTraversalHandlesVeryDeepGraphsAndStopsAtCap()
    {
        MapRoomEntity room = new(new NitroxId(), new NitroxId(), new NitroxInt3());
        WorldEntity root = World(new NitroxId(), new NitroxTechType("Container"), NitroxVector3.Zero);
        room.ChildEntities.Add(root);
        WorldEntity current = root;
        for (int index = 0; index < 20000; index++)
        {
            WorldEntity child = World(new NitroxId(), new NitroxTechType("Container"), NitroxVector3.Zero);
            current.ChildEntities.Add(child);
            current = child;
        }
        current.ChildEntities.AddRange([RangeUpgrade(), RangeUpgrade(), RangeUpgrade(), RangeUpgrade(), RangeUpgrade()]);

        Assert.AreEqual(500f, MapRoomWorldResourceIndex.GetMaximumScanRange(room));
    }

    [TestMethod]
    public void ScanAnchorResolvesFromPersistedRoomParent()
    {
        EntityRegistry registry = new(NullLogger<EntityRegistry>.Instance);
        NitroxId parentId = new();
        NitroxVector3 parentPosition = new(-877.9f, -186.4f, -710.3f);
        registry.AddEntity(World(parentId, new NitroxTechType("Base"), parentPosition));
        MapRoomEntity room = new(new NitroxId(), parentId, new NitroxInt3());

        Assert.IsTrue(MapRoomWorldResourceIndex.TryResolveScanAnchor(registry, room, out NitroxVector3 anchor));
        Assert.AreEqual(parentPosition, anchor);
    }

    [TestMethod]
    public void ClientAndServerRangeContractMatches()
    {
        Assert.AreEqual(MapRoomUpgradeEffects.BASE_RANGE, MapRoomWorldResourceIndex.DEFAULT_SCAN_RANGE);
        Assert.AreEqual(MapRoomUpgradeEffects.RANGE_PER_MODULE, MapRoomWorldResourceIndex.SCAN_RANGE_PER_MODULE);
        Assert.AreEqual(MapRoomUpgradeEffects.MAX_RANGE, MapRoomWorldResourceIndex.MAX_SCAN_RANGE);
    }

    private static WorldEntity World(NitroxId id, NitroxTechType techType, NitroxVector3 position) =>
        new(position, NitroxQuaternion.Identity, NitroxVector3.One, techType, 0, id.ToString(), true, id, null);

    private static WorldEntity RangeUpgrade() => World(new NitroxId(), new NitroxTechType("MapRoomUpgradeScanRange"), NitroxVector3.Zero);
}
