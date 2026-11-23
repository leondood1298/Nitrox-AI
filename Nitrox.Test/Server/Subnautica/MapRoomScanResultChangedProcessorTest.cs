using System.Threading.Tasks;
using Nitrox.Model.DataStructures;
using Nitrox.Model.DataStructures.Unity;
using Nitrox.Model.Packets;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Bases;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;
using Nitrox.Model.Subnautica.Packets;
using Nitrox.Server.Subnautica.Models.Packets.Processors;

namespace Nitrox.Test.Server.Subnautica;

[TestClass]
public sealed class MapRoomScanResultChangedProcessorTest
{
    [TestMethod]
    public async Task BlockedLiveExactUnloadRepliesWithSameRevisionCanonicalSnapshot()
    {
        ScannerRoomScenarioFixture scenario = new();
        NitroxTechType quartz = new("Quartz");
        NitroxId resourceId = new("00000000-0000-0000-0000-000000000301");
        scenario.Room.Metadata = new MapRoomMetadata(quartz, 0, 5, 1);
        scenario.Room.BeginScanResultGeneration(5);
        scenario.Room.TryApplyScanResult(5, new MapRoomScanResultRecord(resourceId.ToString(), quartz, new NitroxVector3(10f, 0f, 0f)));
        long revision = scenario.Room.ScanResultRevision;
        AddRoomParent(scenario);
        scenario.EntityRegistry.AddEntity(new WorldEntity(new NitroxVector3(10f, 0f, 0f), NitroxQuaternion.Identity,
            NitroxVector3.One, quartz, 0, resourceId.ToString(), true, resourceId, null));
        Assert.IsTrue(scenario.Ownership.TryAcquire(scenario.Room.Id, scenario.PlayerA, SimulationLockType.TRANSIENT));
        MapRoomScanResultChangedProcessor processor = new(scenario.Manager);
        MapRoomScanResultChanged unload = new(scenario.Room.Id, 5, resourceId.ToString(), quartz, new NitroxVector3(10f, 0f, 0f),
            removed: true, isRangeExit: false, scanOrigin: NitroxVector3.Zero, scanRange: 300f);

        await processor.Process(scenario.Context(scenario.PlayerA), unload);
        List<Packet> replies = scenario.SentTo<Packet>(scenario.PlayerA);

        Assert.AreEqual(1, replies.Count);
        Assert.IsInstanceOfType<MapRoomScanResultSnapshot>(replies[0]);
        MapRoomScanResultSnapshot correction = (MapRoomScanResultSnapshot)replies[0];
        Assert.IsTrue(correction.IsServerResponse && correction.Granted);
        Assert.AreEqual(5, correction.Generation);
        Assert.AreEqual(revision, correction.Revision);
        Assert.AreEqual(revision, scenario.Room.ScanResultRevision);
        CollectionAssert.AreEqual(new[] { resourceId.ToString() }, correction.Results.Select(result => result.ResourceId).ToArray());
    }

    [TestMethod]
    public async Task OrdinaryLiveExactRemovalOutsideRangeIsAcceptedWithoutCorrectionSnapshot()
    {
        ScannerRoomScenarioFixture scenario = new();
        NitroxTechType quartz = new("Quartz");
        NitroxId resourceId = new("00000000-0000-0000-0000-000000000302");
        scenario.Room.Metadata = new MapRoomMetadata(quartz, 0, 5, 1);
        scenario.Room.BeginScanResultGeneration(5);
        scenario.Room.TryApplyScanResult(5, new MapRoomScanResultRecord(resourceId.ToString(), quartz, new NitroxVector3(10f, 0f, 0f)));
        long revision = scenario.Room.ScanResultRevision;
        AddRoomParent(scenario);
        scenario.EntityRegistry.AddEntity(new WorldEntity(new NitroxVector3(301f, 0f, 0f), NitroxQuaternion.Identity,
            NitroxVector3.One, quartz, 0, resourceId.ToString(), true, resourceId, null));
        Assert.IsTrue(scenario.Ownership.TryAcquire(scenario.Room.Id, scenario.PlayerA, SimulationLockType.TRANSIENT));
        MapRoomScanResultChangedProcessor processor = new(scenario.Manager);
        MapRoomScanResultChanged unload = new(scenario.Room.Id, 5, resourceId.ToString(), quartz, new NitroxVector3(10f, 0f, 0f),
            removed: true, isRangeExit: false, scanOrigin: NitroxVector3.Zero, scanRange: 300f);

        await processor.Process(scenario.Context(scenario.PlayerA), unload);
        List<Packet> replies = scenario.SentTo<Packet>(scenario.PlayerA);

        Assert.AreEqual(1, replies.Count);
        Assert.IsInstanceOfType<MapRoomScanResultChanged>(replies[0]);
        MapRoomScanResultChanged accepted = (MapRoomScanResultChanged)replies[0];
        Assert.IsTrue(accepted.IsServerResponse && accepted.Granted && accepted.Removed && !accepted.IsRangeExit);
        Assert.AreEqual(revision + 1, accepted.Revision);
        Assert.AreEqual(revision + 1, scenario.Room.ScanResultRevision);
        Assert.AreEqual(0, scenario.Room.ScanResults.Count);
    }

    [TestMethod]
    public async Task RejectedLiveExactRangeExitRepliesWithSameRevisionCanonicalSnapshot()
    {
        ScannerRoomScenarioFixture scenario = new();
        NitroxTechType quartz = new("Quartz");
        NitroxId resourceId = new("00000000-0000-0000-0000-000000000303");
        scenario.Room.Metadata = new MapRoomMetadata(quartz, 0, 5, 1);
        scenario.Room.BeginScanResultGeneration(5);
        scenario.Room.TryApplyScanResult(5, new MapRoomScanResultRecord(resourceId.ToString(), quartz, new NitroxVector3(100f, 0f, 0f)));
        long revision = scenario.Room.ScanResultRevision;
        AddRoomParent(scenario);
        scenario.EntityRegistry.AddEntity(new WorldEntity(new NitroxVector3(100f, 0f, 0f), NitroxQuaternion.Identity,
            NitroxVector3.One, quartz, 0, resourceId.ToString(), true, resourceId, null));
        Assert.IsTrue(scenario.Ownership.TryAcquire(scenario.Room.Id, scenario.PlayerA, SimulationLockType.TRANSIENT));
        MapRoomScanResultChangedProcessor processor = new(scenario.Manager);
        MapRoomScanResultChanged rangeExit = new(scenario.Room.Id, 5, resourceId.ToString(), quartz, new NitroxVector3(301f, 0f, 0f),
            removed: true, isRangeExit: true, scanOrigin: NitroxVector3.Zero, scanRange: 300f);

        await processor.Process(scenario.Context(scenario.PlayerA), rangeExit);
        List<Packet> replies = scenario.SentTo<Packet>(scenario.PlayerA);

        Assert.AreEqual(1, replies.Count);
        Assert.IsInstanceOfType<MapRoomScanResultSnapshot>(replies[0]);
        MapRoomScanResultSnapshot correction = (MapRoomScanResultSnapshot)replies[0];
        Assert.IsTrue(correction.IsServerResponse && correction.Granted);
        Assert.AreEqual(revision, correction.Revision);
        Assert.AreEqual(revision, scenario.Room.ScanResultRevision);
        CollectionAssert.AreEqual(new[] { resourceId.ToString() }, correction.Results.Select(result => result.ResourceId).ToArray());
    }

    private static void AddRoomParent(ScannerRoomScenarioFixture scenario)
    {
        scenario.EntityRegistry.AddEntity(new WorldEntity(NitroxVector3.Zero, NitroxQuaternion.Identity, NitroxVector3.One,
            new NitroxTechType("Base"), 0, ScannerRoomScenarioFixture.RoomParentId.ToString(), true,
            ScannerRoomScenarioFixture.RoomParentId, null));
    }
}
