using Nitrox.Model.Core;
using Nitrox.Model.DataStructures;
using Nitrox.Model.DataStructures.GameLogic;
using Nitrox.Model.DataStructures.Unity;
using Nitrox.Model.Packets;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Bases;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;
using Nitrox.Model.Subnautica.Packets;
using Microsoft.Extensions.Logging.Abstractions;
using Nitrox.Server.Subnautica.Models.GameLogic;
using Nitrox.Server.Subnautica.Models.GameLogic.Entities;
using Nitrox.Server.Subnautica.Models.Packets.Core;
using Nitrox.Server.Subnautica.Models.Packets.Processors;
using System.Threading.Tasks;

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
        Assert.IsTrue(scenario.Ownership.TryToAcquire(scenario.Room.Id, scenario.PlayerA, SimulationLockType.TRANSIENT));
        MapRoomScanResultChangedProcessor processor = new(scenario.EntityRegistry, scenario.Ownership, null!, new MapRoomScanResultSubscriptions());
        RecordingSender sender = new();
        MapRoomScanResultChanged unload = new(scenario.Room.Id, 5, resourceId.ToString(), quartz, new NitroxVector3(10f, 0f, 0f),
            removed: true, isRangeExit: false, scanOrigin: NitroxVector3.Zero, scanRange: 300f);

        await processor.Process(new AuthProcessorContext(scenario.PlayerA, sender), unload);

        Assert.AreEqual(1, sender.Replies.Count);
        Assert.IsInstanceOfType<MapRoomScanResultSnapshot>(sender.Replies[0]);
        MapRoomScanResultSnapshot correction = (MapRoomScanResultSnapshot)sender.Replies[0];
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
        Assert.IsTrue(scenario.Ownership.TryToAcquire(scenario.Room.Id, scenario.PlayerA, SimulationLockType.TRANSIENT));
        PlayerManager playerManager = new(null!, null!, NullLogger<PlayerManager>.Instance);
        MapRoomScanResultChangedProcessor processor = new(scenario.EntityRegistry, scenario.Ownership, playerManager, new MapRoomScanResultSubscriptions());
        RecordingSender sender = new();
        MapRoomScanResultChanged unload = new(scenario.Room.Id, 5, resourceId.ToString(), quartz, new NitroxVector3(10f, 0f, 0f),
            removed: true, isRangeExit: false, scanOrigin: NitroxVector3.Zero, scanRange: 300f);

        await processor.Process(new AuthProcessorContext(scenario.PlayerA, sender), unload);

        Assert.AreEqual(1, sender.Replies.Count);
        Assert.IsInstanceOfType<MapRoomScanResultChanged>(sender.Replies[0]);
        MapRoomScanResultChanged accepted = (MapRoomScanResultChanged)sender.Replies[0];
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
        Assert.IsTrue(scenario.Ownership.TryToAcquire(scenario.Room.Id, scenario.PlayerA, SimulationLockType.TRANSIENT));
        MapRoomScanResultChangedProcessor processor = new(scenario.EntityRegistry, scenario.Ownership, null!, new MapRoomScanResultSubscriptions());
        RecordingSender sender = new();
        MapRoomScanResultChanged rangeExit = new(scenario.Room.Id, 5, resourceId.ToString(), quartz, new NitroxVector3(301f, 0f, 0f),
            removed: true, isRangeExit: true, scanOrigin: NitroxVector3.Zero, scanRange: 300f);

        await processor.Process(new AuthProcessorContext(scenario.PlayerA, sender), rangeExit);

        Assert.AreEqual(1, sender.Replies.Count);
        Assert.IsInstanceOfType<MapRoomScanResultSnapshot>(sender.Replies[0]);
        MapRoomScanResultSnapshot correction = (MapRoomScanResultSnapshot)sender.Replies[0];
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

    private sealed class RecordingSender : IPacketSender
    {
        internal List<Packet> Replies { get; } = [];

        public ValueTask SendPacketAsync<T>(T packet, SessionId sessionId) where T : Packet
        {
            Replies.Add(packet);
            return ValueTask.CompletedTask;
        }

        public ValueTask SendPacketToAllAsync<T>(T packet) where T : Packet => ValueTask.CompletedTask;

        public ValueTask SendPacketToOthersAsync<T>(T packet, SessionId excludedSessionId) where T : Packet => ValueTask.CompletedTask;
    }
}
