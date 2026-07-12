using Nitrox.Model.DataStructures;
using Nitrox.Model.DataStructures.Unity;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Bases;

namespace Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities;

[TestClass]
public sealed class MapRoomScanResultsTest
{
    [TestMethod]
    public void NewGenerationClearsOldResultsAndRejectsOldGeneration()
    {
        MapRoomEntity room = CreateRoom();
        room.BeginScanResultGeneration(3);
        Assert.IsTrue(room.TryApplyScanResult(3, Result("quartz", 1f)));

        room.BeginScanResultGeneration(4);

        Assert.AreEqual(0, room.ScanResults.Count);
        Assert.IsFalse(room.TryApplyScanResult(3, Result("stale", 2f)));
        Assert.AreEqual(4, room.ScanResultGeneration);
    }

    [TestMethod]
    public void DuplicateResourceUpdatesWithoutDuplicating()
    {
        MapRoomEntity room = CreateRoom();
        room.BeginScanResultGeneration(1);

        Assert.IsTrue(room.TryApplyScanResult(1, Result("resource", 1f)));
        Assert.IsTrue(room.TryApplyScanResult(1, Result("resource", 9f)));
        long revision = room.ScanResultRevision;
        Assert.IsFalse(room.TryApplyScanResult(1, Result("resource", 9f)));

        Assert.AreEqual(1, room.ScanResults.Count);
        Assert.AreEqual(new NitroxVector3(9f, 0f, 0f), room.ScanResults[0].Position);
        Assert.AreEqual(revision, room.ScanResultRevision);
    }

    [TestMethod]
    public void RemoveIsIdempotentAndGenerationGuarded()
    {
        MapRoomEntity room = CreateRoom();
        room.BeginScanResultGeneration(2);
        room.TryApplyScanResult(2, Result("resource", 1f));
        long revision = room.ScanResultRevision;

        Assert.IsFalse(room.TryRemoveScanResult(1, "resource"));
        Assert.IsTrue(room.TryRemoveScanResult(2, "resource"));
        Assert.IsFalse(room.TryRemoveScanResult(2, "resource"));
        Assert.AreEqual(revision + 1, room.ScanResultRevision);
    }

    private static MapRoomScanResultRecord Result(string id, float x) => new(id, new NitroxTechType("Quartz"), new NitroxVector3(x, 0f, 0f));

    private static MapRoomEntity CreateRoom() => new(new NitroxId(), new NitroxId(), new NitroxInt3());
}
