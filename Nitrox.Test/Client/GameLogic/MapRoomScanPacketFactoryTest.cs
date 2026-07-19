using Nitrox.Model.DataStructures;
using Nitrox.Model.DataStructures.Unity;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Bases;
using Nitrox.Model.Subnautica.Packets;
using UnityEngine;

namespace NitroxClient.GameLogic;

[TestClass]
public sealed class MapRoomScanPacketFactoryTest
{
    [TestMethod]
    public void ResultSnapshotIncludesScannerQuery()
    {
        NitroxId roomId = new();
        Vector3 origin = new(1f, 2f, 3f);
        List<MapRoomScanResultRecord> results = [new("resource", new NitroxTechType("Quartz"), new NitroxVector3(4f, 5f, 6f))];

        MapRoomScanResultSnapshot packet = MapRoomScanResultBroadcaster.CreateSnapshotPacket(roomId, 7, results, origin, 350f);

        Assert.AreEqual(roomId, packet.MapRoomId);
        Assert.AreEqual(7, packet.Generation);
        Assert.AreSame(results, packet.Results);
        Assert.AreEqual(new NitroxVector3(1f, 2f, 3f), packet.ScanOrigin);
        Assert.AreEqual(350f, packet.ScanRange);
    }

    [TestMethod]
    public void ResultDeltaDistinguishesTrackerUnloadFromRangeExit()
    {
        NitroxId roomId = new();
        ResourceTrackerDatabase.ResourceInfo info = new()
        {
            uniqueId = "resource",
            techType = TechType.Quartz,
            position = new Vector3(301f, 0f, 0f)
        };

        MapRoomScanResultChanged unload = MapRoomScanResultBroadcaster.CreateChangedPacket(roomId, 3, info, removed: true,
            isRangeExit: false, scanOrigin: new Vector3(1f, 2f, 3f), scanRange: 300f);
        MapRoomScanResultChanged rangeExit = MapRoomScanResultBroadcaster.CreateChangedPacket(roomId, 3, info, removed: true,
            isRangeExit: true, scanOrigin: new Vector3(1f, 2f, 3f), scanRange: 300f);

        Assert.IsFalse(unload.IsRangeExit);
        Assert.IsTrue(rangeExit.IsRangeExit);
        Assert.AreEqual(new NitroxVector3(1f, 2f, 3f), rangeExit.ScanOrigin);
        Assert.AreEqual(300f, rangeExit.ScanRange);
    }

    [TestMethod]
    public void ScanTypesSnapshotKeepsAvailableAndDetectableTypesDistinct()
    {
        NitroxId roomId = new();
        Vector3 origin = new(-10f, 20f, 30f);

        MapRoomScanTypesSnapshot packet = MapRoomScanTypes.CreateSnapshotPacket(roomId, [TechType.Quartz],
            [TechType.Quartz, TechType.ShaleChunk], origin, 400f);

        CollectionAssert.AreEqual(new[] { new NitroxTechType("Quartz") }, packet.TechTypes);
        CollectionAssert.AreEqual(new[] { new NitroxTechType("Quartz"), new NitroxTechType("ShaleChunk") }, packet.DetectableTechTypes);
        Assert.AreEqual(new NitroxVector3(-10f, 20f, 30f), packet.ScanOrigin);
        Assert.AreEqual(400f, packet.ScanRange);
    }
}
