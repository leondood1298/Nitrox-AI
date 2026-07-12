using Nitrox.Model.DataStructures;
using Nitrox.Model.DataStructures.Unity;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Bases;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;
using Nitrox.Model.Subnautica.Packets;

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

        Assert.IsTrue(MapRoomScanResultAuthority.TryApply(room, Change(room, "resource", 1f)));
        Assert.IsTrue(MapRoomScanResultAuthority.TryApply(room, Change(room, "resource", 2f)));
        Assert.AreEqual(1, room.ScanResults.Count);
        Assert.AreEqual(new NitroxVector3(2f, 0f, 0f), room.ScanResults[0].Position);
        Assert.IsTrue(MapRoomScanResultAuthority.TryApply(room, Change(room, "resource", 0f, removed: true)));
        Assert.AreEqual(0, room.ScanResults.Count);
    }

    [TestMethod]
    public void RejectsStaleGenerationWrongTargetAndDuplicateReplay()
    {
        MapRoomEntity room = CreateRoom();
        MapRoomScanResultChanged accepted = Change(room, "resource", 1f);
        Assert.IsTrue(MapRoomScanResultAuthority.TryApply(room, accepted));

        Assert.IsFalse(MapRoomScanResultAuthority.TryApply(room, accepted));
        Assert.IsFalse(MapRoomScanResultAuthority.TryApply(room, new MapRoomScanResultChanged(room.Id, 4, "stale", quartz, NitroxVector3.Zero)));
        Assert.IsFalse(MapRoomScanResultAuthority.TryApply(room, new MapRoomScanResultChanged(room.Id, 5, "wrong", titanium, NitroxVector3.Zero)));
        Assert.AreEqual(1, room.ScanResults.Count);
    }

    [TestMethod]
    public void RejectsEmptyIdServerResponseAndUnknownRemoval()
    {
        MapRoomEntity room = CreateRoom();

        Assert.IsFalse(MapRoomScanResultAuthority.TryApply(room, Change(room, "", 1f)));
        Assert.IsFalse(MapRoomScanResultAuthority.TryApply(room, new MapRoomScanResultChanged(new NitroxId(), 5, "wrong-room", quartz, NitroxVector3.Zero)));
        Assert.IsFalse(MapRoomScanResultAuthority.TryApply(room, new MapRoomScanResultChanged(room.Id, 5, "server", quartz, NitroxVector3.Zero, isServerResponse: true)));
        Assert.IsFalse(MapRoomScanResultAuthority.TryApply(room, Change(room, "missing", 0f, removed: true)));
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
        ]);

        Assert.IsTrue(MapRoomScanResultAuthority.TryApplySnapshot(room, snapshot, out List<MapRoomScanResultRecord> accepted, out long acceptedRevision));

        CollectionAssert.AreEqual(new[] { "first", "second" }, accepted.Select(result => result.ResourceId).ToArray());
        Assert.AreEqual(revision + 1, acceptedRevision);
        Assert.AreEqual(2, room.ScanResults.Count);
        Assert.IsFalse(MapRoomScanResultAuthority.TryApplySnapshot(room, snapshot, out _, out long replayRevision));
        Assert.AreEqual(acceptedRevision, replayRevision);
    }

    [TestMethod]
    public void SnapshotRejectsDuplicatesWrongTargetAndStaleGenerationWithoutMutation()
    {
        MapRoomEntity room = CreateRoom();
        List<MapRoomScanResultRecord> duplicate = [new("same", quartz, NitroxVector3.Zero), new("same", quartz, NitroxVector3.One)];
        List<MapRoomScanResultRecord> wrongTarget = [new("wrong", titanium, NitroxVector3.Zero)];

        Assert.IsFalse(MapRoomScanResultAuthority.TryApplySnapshot(room, new MapRoomScanResultSnapshot(room.Id, 5, duplicate), out _, out _));
        Assert.IsFalse(MapRoomScanResultAuthority.TryApplySnapshot(room, new MapRoomScanResultSnapshot(room.Id, 5, wrongTarget), out _, out _));
        Assert.IsFalse(MapRoomScanResultAuthority.TryApplySnapshot(room, new MapRoomScanResultSnapshot(room.Id, 4, []), out _, out _));
        Assert.AreEqual(0, room.ScanResults.Count);
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
}
