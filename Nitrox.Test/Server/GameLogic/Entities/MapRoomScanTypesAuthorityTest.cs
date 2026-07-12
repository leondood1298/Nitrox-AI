using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;
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

        Assert.IsTrue(MapRoomScanTypesAuthority.TryApply(room, new MapRoomScanTypesSnapshot(room.Id, [titanium, quartz]), out List<NitroxTechType> accepted, out long revision));

        CollectionAssert.AreEqual(new[] { quartz, titanium }, accepted);
        Assert.AreEqual(1, revision);
        CollectionAssert.AreEqual(accepted, room.AvailableScanTypes);
    }

    [TestMethod]
    public void RejectsReplayDuplicatesNoneAndWrongRoomWithoutMutation()
    {
        MapRoomEntity room = CreateRoom();
        Assert.IsTrue(MapRoomScanTypesAuthority.TryApply(room, new MapRoomScanTypesSnapshot(room.Id, [quartz]), out _, out long revision));

        Assert.IsFalse(MapRoomScanTypesAuthority.TryApply(room, new MapRoomScanTypesSnapshot(room.Id, [quartz]), out _, out long replayRevision));
        Assert.IsFalse(MapRoomScanTypesAuthority.TryApply(room, new MapRoomScanTypesSnapshot(room.Id, [quartz, quartz]), out _, out _));
        Assert.IsFalse(MapRoomScanTypesAuthority.TryApply(room, new MapRoomScanTypesSnapshot(room.Id, [NitroxTechType.None]), out _, out _));
        Assert.IsFalse(MapRoomScanTypesAuthority.TryApply(room, new MapRoomScanTypesSnapshot(new NitroxId(), [titanium]), out _, out _));
        Assert.AreEqual(revision, replayRevision);
        CollectionAssert.AreEqual(new[] { quartz }, room.AvailableScanTypes);
    }

    [TestMethod]
    public void FirstEmptySnapshotIsAcceptedThenReplayIsRejected()
    {
        MapRoomEntity room = CreateRoom();

        Assert.IsTrue(MapRoomScanTypesAuthority.TryApply(room, new MapRoomScanTypesSnapshot(room.Id, []), out List<NitroxTechType> accepted, out long revision));
        Assert.IsFalse(MapRoomScanTypesAuthority.TryApply(room, new MapRoomScanTypesSnapshot(room.Id, []), out _, out long replayRevision));
        Assert.AreEqual(0, accepted.Count);
        Assert.AreEqual(1, revision);
        Assert.AreEqual(revision, replayRevision);
    }

    private static MapRoomEntity CreateRoom() => new(new NitroxId(), new NitroxId(), new NitroxInt3());
}
