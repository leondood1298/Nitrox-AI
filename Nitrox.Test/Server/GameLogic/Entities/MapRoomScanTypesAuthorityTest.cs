using Nitrox.Model.DataStructures;
using Nitrox.Model.DataStructures.Unity;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Bases;
using Nitrox.Model.Subnautica.Packets;

namespace Nitrox.Server.Subnautica.Models.GameLogic.Entities;

[TestClass]
public sealed class MapRoomScanTypesAuthorityTest
{
    private static readonly NitroxTechType quartz = new("Quartz");
    private static readonly NitroxTechType titanium = new("Titanium");

    [TestMethod]
    public void SnapshotNormalizesPersistsAndRevisionsCanonicalTypes()
    {
        MapRoomEntity room = CreateRoom();

        Assert.IsTrue(MapRoomScanTypesAuthority.TryApply(room, Snapshot(room, [titanium, quartz]), [], NitroxVector3.Zero, out List<NitroxTechType> accepted, out long revision));

        CollectionAssert.AreEqual(new[] { quartz, titanium }, accepted);
        Assert.AreEqual(1, revision);
        CollectionAssert.AreEqual(accepted, room.AvailableScanTypes);
    }

    [TestMethod]
    public void AcceptsReplayIdempotentlyAndRejectsInvalidTypesAndWrongRoom()
    {
        MapRoomEntity room = CreateRoom();
        Assert.IsTrue(MapRoomScanTypesAuthority.TryApply(room, Snapshot(room, [quartz]), [], NitroxVector3.Zero, out _, out long revision));

        Assert.IsTrue(MapRoomScanTypesAuthority.TryApply(room, Snapshot(room, [quartz]), [], NitroxVector3.Zero, out List<NitroxTechType> replay, out long replayRevision));
        Assert.IsFalse(MapRoomScanTypesAuthority.TryApply(room, Snapshot(room, [quartz, quartz]), [], NitroxVector3.Zero, out _, out _));
        Assert.IsFalse(MapRoomScanTypesAuthority.TryApply(room, Snapshot(room, [NitroxTechType.None]), [], NitroxVector3.Zero, out _, out _));
        Assert.IsFalse(MapRoomScanTypesAuthority.TryApply(room, Snapshot(room, [quartz], [titanium, titanium]), [], NitroxVector3.Zero, out _, out _));
        Assert.IsFalse(MapRoomScanTypesAuthority.TryApply(room, Snapshot(room, [quartz], [NitroxTechType.None]), [], NitroxVector3.Zero, out _, out _));
        Assert.IsFalse(MapRoomScanTypesAuthority.TryApply(room,
            new MapRoomScanTypesSnapshot(new NitroxId(), [titanium], [titanium], NitroxVector3.Zero, 300f), [], NitroxVector3.Zero, out _, out _));
        Assert.AreEqual(revision, replayRevision);
        CollectionAssert.AreEqual(new[] { quartz }, replay);
        CollectionAssert.AreEqual(new[] { quartz }, room.AvailableScanTypes);
    }

    [TestMethod]
    public void FirstEmptySnapshotInitializesThenReplayIsIdempotent()
    {
        MapRoomEntity room = CreateRoom();

        Assert.IsTrue(MapRoomScanTypesAuthority.TryApply(room, Snapshot(room, [], []), [], NitroxVector3.Zero, out List<NitroxTechType> accepted, out long revision));
        Assert.IsTrue(MapRoomScanTypesAuthority.TryApply(room, Snapshot(room, [], []), [], NitroxVector3.Zero, out _, out long replayRevision));
        Assert.AreEqual(0, accepted.Count);
        Assert.AreEqual(1, revision);
        Assert.AreEqual(revision, replayRevision);
    }

    [TestMethod]
    public void SnapshotUnionsOnlyDetectableExactWorldTypesInRange()
    {
        MapRoomEntity room = CreateRoom();
        NitroxTechType lead = new("Lead");
        List<WorldEntity> worldEntities =
        [
            World(quartz, new NitroxVector3(100f, 0f, 0f)),
            World(titanium, new NitroxVector3(100f, 0f, 0f)),
            World(new NitroxTechType("ShaleChunk"), new NitroxVector3(301f, 0f, 0f))
        ];

        Assert.IsTrue(MapRoomScanTypesAuthority.TryApply(room, Snapshot(room, [lead], [quartz]), worldEntities, NitroxVector3.Zero,
            out List<NitroxTechType> accepted, out _));

        CollectionAssert.AreEqual(new[] { lead, quartz }, accepted);
    }

    [TestMethod]
    public void SnapshotRejectsInvalidOriginAndRange()
    {
        MapRoomEntity room = CreateRoom();

        Assert.IsFalse(MapRoomScanTypesAuthority.TryApply(room,
            new MapRoomScanTypesSnapshot(room.Id, [], [], new NitroxVector3(float.PositiveInfinity, 0f, 0f), 300f), [], NitroxVector3.Zero, out _, out _));
        Assert.IsFalse(MapRoomScanTypesAuthority.TryApply(room,
            new MapRoomScanTypesSnapshot(room.Id, [], [], NitroxVector3.Zero, 501f), [], NitroxVector3.Zero, out _, out _));
        Assert.IsFalse(MapRoomScanTypesAuthority.TryApply(room,
            new MapRoomScanTypesSnapshot(room.Id, [], [], new NitroxVector3(513f, 0f, 0f), 300f), [], NitroxVector3.Zero, out _, out _));
    }

    private static MapRoomEntity CreateRoom() => new(new NitroxId(), new NitroxId(), new NitroxInt3());

    private static MapRoomScanTypesSnapshot Snapshot(MapRoomEntity room, List<NitroxTechType> types,
        List<NitroxTechType>? detectable = null) =>
        new(room.Id, types, detectable ?? [quartz, titanium], NitroxVector3.Zero, 300f);

    private static WorldEntity World(NitroxTechType techType, NitroxVector3 position) =>
        new(position, NitroxQuaternion.Identity, NitroxVector3.One, techType, 0, techType.ToString(), true, new NitroxId(), null);
}
