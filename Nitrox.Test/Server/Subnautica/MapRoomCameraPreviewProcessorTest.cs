using System.Threading.Tasks;
using Nitrox.Model.Core;
using Nitrox.Model.DataStructures;
using Nitrox.Model.DataStructures.Unity;
using Nitrox.Model.Packets;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Bases;
using Nitrox.Model.Subnautica.Packets;
using Nitrox.Server.Subnautica.Models.GameLogic;
using Nitrox.Server.Subnautica.Models.Packets.Core;
using Nitrox.Server.Subnautica.Models.Packets.Processors;
using Nitrox.Test.Model.Subnautica;

namespace Nitrox.Test.Server.Subnautica;

[TestClass]
public sealed class MapRoomCameraPreviewProcessorTest
{
    [TestMethod]
    public async Task AcceptedPreviewUsesCanonicalNumberAndCanPublishOnlyOncePerControlAcquisition()
    {
        ScannerRoomScenarioFixture scenario = await CreateControlledScenarioAsync();
        MapRoomCameraPreviewProcessor processor = new(scenario.Ownership, scenario.EntityRegistry,
            scenario.ControlLifecycle, scenario.Diagnostics);
        RecordingSender sender = new();
        byte[] jpeg = MapRoomCameraPreviewImageTest.CreateJpeg(256, 256);
        AuthProcessorContext context = new(scenario.PlayerA, sender);

        await processor.Process(context, new MapRoomCameraPreview(ScannerRoomScenarioFixture.CameraAId, 999, jpeg));
        await processor.Process(context, new MapRoomCameraPreview(ScannerRoomScenarioFixture.CameraAId, 999, jpeg));

        MapRoomCameraPreview accepted = sender.ToAll.Single();
        Assert.IsTrue(accepted.IsServerResponse);
        Assert.IsTrue(accepted.Granted);
        Assert.AreEqual(1, accepted.CameraNumber, "The server must ignore the request's presentation number.");
        Assert.AreEqual(1L, accepted.Revision);
        CollectionAssert.AreEqual(jpeg, accepted.JpegBytes);
        Assert.AreEqual(0, sender.ToOthers.Count, "Accepted revisions must include the sender via all-client broadcast.");
        Assert.AreEqual(1, sender.Replies.Count(packet => !packet.Granted));
    }

    [TestMethod]
    public async Task ValidUnregisteredLooseCameraUsesBoundedPresentationNumber()
    {
        ScannerRoomScenarioFixture scenario = new();
        Assert.IsNull(scenario.Room.GetCameraRecord(ScannerRoomScenarioFixture.CameraAId));
        Assert.IsTrue((await scenario.ControlLooseAsync(scenario.PlayerA,
            ScannerRoomScenarioFixture.CameraAId, true)).Granted);
        MapRoomCameraPreviewProcessor processor = new(scenario.Ownership, scenario.EntityRegistry,
            scenario.ControlLifecycle, scenario.Diagnostics);
        RecordingSender sender = new();

        await processor.Process(new AuthProcessorContext(scenario.PlayerA, sender),
            new MapRoomCameraPreview(ScannerRoomScenarioFixture.CameraAId, 37,
                MapRoomCameraPreviewImageTest.CreateJpeg(64, 64)));

        MapRoomCameraPreview accepted = sender.ToAll.Single();
        Assert.AreEqual(37, accepted.CameraNumber);
        Assert.IsTrue(accepted.Granted);
    }

    [DataTestMethod]
    [DataRow(0)]
    [DataRow(-1)]
    [DataRow(MapRoomCameraPreview.MAX_LOOSE_CAMERA_NUMBER + 1)]
    public async Task LooseCameraRejectsUnboundedPresentationNumberAndConsumesAttempt(int cameraNumber)
    {
        ScannerRoomScenarioFixture scenario = new();
        Assert.IsTrue((await scenario.ControlLooseAsync(scenario.PlayerA,
            ScannerRoomScenarioFixture.CameraAId, true)).Granted);
        MapRoomCameraPreviewProcessor processor = new(scenario.Ownership, scenario.EntityRegistry,
            scenario.ControlLifecycle, scenario.Diagnostics);
        RecordingSender sender = new();
        AuthProcessorContext context = new(scenario.PlayerA, sender);
        byte[] jpeg = MapRoomCameraPreviewImageTest.CreateJpeg(64, 64);

        await processor.Process(context,
            new MapRoomCameraPreview(ScannerRoomScenarioFixture.CameraAId, cameraNumber, jpeg));
        await processor.Process(context,
            new MapRoomCameraPreview(ScannerRoomScenarioFixture.CameraAId, 1, jpeg));

        Assert.AreEqual(0, sender.ToAll.Count);
        Assert.AreEqual(2, sender.Replies.Count(packet => !packet.Granted));
    }

    [TestMethod]
    public async Task DuplicateRoomRegistrationCannotFallBackToPresentationNumber()
    {
        ScannerRoomScenarioFixture scenario = await CreateControlledScenarioAsync();
        MapRoomEntity duplicateRoom = new(new NitroxId("00000000-0000-0000-0000-000000000900"),
            new NitroxId("00000000-0000-0000-0000-000000000901"), new NitroxInt3());
        lock (duplicateRoom)
        {
            duplicateRoom.GetOrAssignCameraNumber(ScannerRoomScenarioFixture.CameraAId, 7);
        }
        scenario.EntityRegistry.AddEntity(duplicateRoom);
        MapRoomCameraPreviewProcessor processor = new(scenario.Ownership, scenario.EntityRegistry,
            scenario.ControlLifecycle, scenario.Diagnostics);
        RecordingSender sender = new();

        await processor.Process(new AuthProcessorContext(scenario.PlayerA, sender),
            new MapRoomCameraPreview(ScannerRoomScenarioFixture.CameraAId, 7,
                MapRoomCameraPreviewImageTest.CreateJpeg(64, 64)));

        Assert.AreEqual(0, sender.ToAll.Count);
        Assert.AreEqual(1, sender.Replies.Count(packet => !packet.Granted));
    }

    [TestMethod]
    public async Task DuplicateControlAcquireDoesNotReopenConsumedPreview()
    {
        ScannerRoomScenarioFixture scenario = await CreateControlledScenarioAsync();
        MapRoomCameraPreviewProcessor processor = new(scenario.Ownership, scenario.EntityRegistry,
            scenario.ControlLifecycle, scenario.Diagnostics);
        RecordingSender sender = new();
        byte[] jpeg = MapRoomCameraPreviewImageTest.CreateJpeg(64, 64);
        AuthProcessorContext context = new(scenario.PlayerA, sender);

        await processor.Process(context, new MapRoomCameraPreview(ScannerRoomScenarioFixture.CameraAId, 1, jpeg));
        MapRoomCameraControl duplicate = await scenario.ControlAsync(scenario.PlayerA,
            ScannerRoomScenarioFixture.CameraAId, 0, true);
        Assert.IsTrue(duplicate.Granted);
        await processor.Process(context, new MapRoomCameraPreview(ScannerRoomScenarioFixture.CameraAId, 1, jpeg));

        Assert.AreEqual(1, sender.ToAll.Count);
        Assert.AreEqual(1, sender.Replies.Count(packet => !packet.Granted));
    }

    [TestMethod]
    public async Task BackgroundSimulationRefreshPreservesExclusiveControlUntilPreviewPublishes()
    {
        ScannerRoomScenarioFixture scenario = await CreateControlledScenarioAsync();
        Assert.IsTrue(scenario.EntityRegistry.TryGetEntityById(ScannerRoomScenarioFixture.CameraAId,
            out Entity cameraEntity));

        Assert.IsTrue(scenario.EntitySimulation.TryAssignEntityToPlayer(cameraEntity, scenario.PlayerA,
            shouldEntityMove: true, out SimulatedEntity? refreshedAssignment));
        Assert.IsNotNull(refreshedAssignment);
        Assert.AreEqual(SimulationLockType.EXCLUSIVE, refreshedAssignment.LockType,
            "A background visibility refresh must report the effective interactive lock.");
        Assert.IsTrue(scenario.Ownership.TryGetLock(ScannerRoomScenarioFixture.CameraAId,
            out SimulationOwnershipData.PlayerLock serverLock));
        Assert.AreEqual(SimulationLockType.EXCLUSIVE, serverLock.LockType,
            "A background transient acquisition must not downgrade active camera control.");

        MapRoomCameraPreviewProcessor processor = new(scenario.Ownership, scenario.EntityRegistry,
            scenario.ControlLifecycle, scenario.Diagnostics);
        RecordingSender sender = new();
        await processor.Process(new AuthProcessorContext(scenario.PlayerA, sender),
            new MapRoomCameraPreview(ScannerRoomScenarioFixture.CameraAId, 1,
                MapRoomCameraPreviewImageTest.CreateJpeg(64, 64)));

        Assert.AreEqual(1, sender.ToAll.Count,
            "The preview must remain eligible after the background assignment interleaves with control.");
        Assert.AreEqual(0, sender.Replies.Count);
    }

    [TestMethod]
    public async Task ReleaseThenRealReacquisitionOpensOneNewPreview()
    {
        ScannerRoomScenarioFixture scenario = await CreateControlledScenarioAsync();
        MapRoomCameraPreviewProcessor processor = new(scenario.Ownership, scenario.EntityRegistry,
            scenario.ControlLifecycle, scenario.Diagnostics);
        RecordingSender sender = new();
        byte[] jpeg = MapRoomCameraPreviewImageTest.CreateJpeg(64, 64);
        AuthProcessorContext context = new(scenario.PlayerA, sender);

        await processor.Process(context, new MapRoomCameraPreview(ScannerRoomScenarioFixture.CameraAId, 1, jpeg));
        Assert.IsTrue((await scenario.ControlAsync(scenario.PlayerA, ScannerRoomScenarioFixture.CameraAId, 0, false)).Granted);
        Assert.IsTrue((await scenario.ControlAsync(scenario.PlayerA, ScannerRoomScenarioFixture.CameraAId, 0, true)).Granted);
        await processor.Process(context, new MapRoomCameraPreview(ScannerRoomScenarioFixture.CameraAId, 1, jpeg));

        CollectionAssert.AreEqual(new long[] { 1, 2 }, sender.ToAll.Select(packet => packet.Revision).ToArray());
    }

    [TestMethod]
    public async Task SequentialControllersPublishCanonicalRevisionsToAllClientsIncludingEachSender()
    {
        ScannerRoomScenarioFixture scenario = await CreateControlledScenarioAsync();
        MapRoomCameraPreviewProcessor processor = new(scenario.Ownership, scenario.EntityRegistry,
            scenario.ControlLifecycle, scenario.Diagnostics);
        byte[] jpeg = MapRoomCameraPreviewImageTest.CreateJpeg(64, 64);
        RecordingSender firstSender = new();

        await processor.Process(new AuthProcessorContext(scenario.PlayerA, firstSender),
            new MapRoomCameraPreview(ScannerRoomScenarioFixture.CameraAId, 1, jpeg));
        Assert.IsTrue((await scenario.ControlAsync(scenario.PlayerA,
            ScannerRoomScenarioFixture.CameraAId, 0, false)).Granted);
        Assert.IsTrue((await scenario.ControlAsync(scenario.PlayerB,
            ScannerRoomScenarioFixture.CameraAId, 0, true)).Granted);

        RecordingSender secondSender = new();
        await processor.Process(new AuthProcessorContext(scenario.PlayerB, secondSender),
            new MapRoomCameraPreview(ScannerRoomScenarioFixture.CameraAId, 1, jpeg));

        Assert.AreEqual(1L, firstSender.ToAll.Single().Revision);
        Assert.AreEqual(2L, secondSender.ToAll.Single().Revision);
        Assert.AreEqual(0, firstSender.ToOthers.Count);
        Assert.AreEqual(0, secondSender.ToOthers.Count);
    }

    [TestMethod]
    public async Task NonOwnerCannotConsumeControllersPreviewOpportunity()
    {
        ScannerRoomScenarioFixture scenario = await CreateControlledScenarioAsync();
        MapRoomCameraPreviewProcessor processor = new(scenario.Ownership, scenario.EntityRegistry,
            scenario.ControlLifecycle, scenario.Diagnostics);
        byte[] jpeg = MapRoomCameraPreviewImageTest.CreateJpeg(64, 64);
        RecordingSender attackerSender = new();

        await processor.Process(new AuthProcessorContext(scenario.PlayerB, attackerSender),
            new MapRoomCameraPreview(ScannerRoomScenarioFixture.CameraAId, 1, jpeg));
        RecordingSender ownerSender = new();
        await processor.Process(new AuthProcessorContext(scenario.PlayerA, ownerSender),
            new MapRoomCameraPreview(ScannerRoomScenarioFixture.CameraAId, 1, jpeg));

        Assert.AreEqual(1, attackerSender.Replies.Count(packet => !packet.Granted));
        Assert.AreEqual(1, ownerSender.ToAll.Count);
    }

    [TestMethod]
    public async Task MalformedOwnerPayloadConsumesTheSingleOpportunity()
    {
        ScannerRoomScenarioFixture scenario = await CreateControlledScenarioAsync();
        MapRoomCameraPreviewProcessor processor = new(scenario.Ownership, scenario.EntityRegistry,
            scenario.ControlLifecycle, scenario.Diagnostics);
        RecordingSender sender = new();
        AuthProcessorContext context = new(scenario.PlayerA, sender);

        await processor.Process(context, new MapRoomCameraPreview(ScannerRoomScenarioFixture.CameraAId, 1, [1, 2, 3]));
        await processor.Process(context, new MapRoomCameraPreview(ScannerRoomScenarioFixture.CameraAId, 1,
            MapRoomCameraPreviewImageTest.CreateJpeg(64, 64)));

        Assert.AreEqual(0, sender.ToAll.Count);
        Assert.AreEqual(2, sender.Replies.Count(packet => !packet.Granted));
    }

    [TestMethod]
    public async Task CanonicalCleanupCannotLeaveReusablePreviewEligibility()
    {
        ScannerRoomScenarioFixture scenario = await CreateControlledScenarioAsync();
        Assert.IsTrue(scenario.Ownership.RevokeOwnerOfId(ScannerRoomScenarioFixture.CameraAId,
            out SimulationOwnershipData.PlayerLock revoked));
        Assert.IsTrue(scenario.ControlReleaseFactory.TryCreate(ScannerRoomScenarioFixture.CameraAId, revoked,
            "test_cleanup", out _));
        Assert.IsTrue(scenario.Ownership.TryToAcquire(ScannerRoomScenarioFixture.CameraAId, scenario.PlayerA,
            SimulationLockType.EXCLUSIVE));
        MapRoomCameraPreviewProcessor processor = new(scenario.Ownership, scenario.EntityRegistry,
            scenario.ControlLifecycle, scenario.Diagnostics);
        RecordingSender sender = new();

        await processor.Process(new AuthProcessorContext(scenario.PlayerA, sender),
            new MapRoomCameraPreview(ScannerRoomScenarioFixture.CameraAId, 1,
                MapRoomCameraPreviewImageTest.CreateJpeg(64, 64)));

        Assert.AreEqual(0, sender.ToAll.Count);
        Assert.AreEqual(1, sender.Replies.Count(packet => !packet.Granted));
    }

    [TestMethod]
    public async Task CleanupOfLegacyTransientLockClearsConsumedEligibilityBeforeReacquisition()
    {
        ScannerRoomScenarioFixture scenario = await CreateControlledScenarioAsync();
        MapRoomCameraPreviewProcessor processor = new(scenario.Ownership, scenario.EntityRegistry,
            scenario.ControlLifecycle, scenario.Diagnostics);
        RecordingSender sender = new();
        byte[] jpeg = MapRoomCameraPreviewImageTest.CreateJpeg(64, 64);
        AuthProcessorContext context = new(scenario.PlayerA, sender);
        await processor.Process(context,
            new MapRoomCameraPreview(ScannerRoomScenarioFixture.CameraAId, 1, jpeg));

        // Model a lock left transient by an older/background path. Cleanup emits no control-release packet
        // for it, but must still remove the consumed preview acquisition state.
        Assert.IsTrue(scenario.Ownership.TryToAcquire(ScannerRoomScenarioFixture.CameraAId, scenario.PlayerA,
            SimulationLockType.TRANSIENT));
        Assert.IsTrue(scenario.Ownership.RevokeOwnerOfId(ScannerRoomScenarioFixture.CameraAId,
            out SimulationOwnershipData.PlayerLock revoked));
        Assert.AreEqual(SimulationLockType.TRANSIENT, revoked.LockType);
        Assert.IsFalse(scenario.ControlReleaseFactory.TryCreate(ScannerRoomScenarioFixture.CameraAId, revoked,
            "legacy_transient_cleanup", out _));

        Assert.IsTrue((await scenario.ControlAsync(scenario.PlayerA, ScannerRoomScenarioFixture.CameraAId, 0, true)).Granted);
        await processor.Process(context,
            new MapRoomCameraPreview(ScannerRoomScenarioFixture.CameraAId, 1, jpeg));

        CollectionAssert.AreEqual(new long[] { 1, 2 }, sender.ToAll.Select(packet => packet.Revision).ToArray());
    }

    [TestMethod]
    public async Task GenericDowngradeCannotReplaceCanonicalControlRelease()
    {
        ScannerRoomScenarioFixture scenario = await CreateControlledScenarioAsync();
        Assert.IsFalse((await scenario.RequestOwnershipAsync(scenario.PlayerA,
            ScannerRoomScenarioFixture.CameraAId, SimulationLockType.TRANSIENT)).LockAcquired);
        Assert.IsTrue(scenario.Ownership.TryGetLock(ScannerRoomScenarioFixture.CameraAId,
            out SimulationOwnershipData.PlayerLock preservedLock));
        Assert.AreEqual(SimulationLockType.EXCLUSIVE, preservedLock.LockType,
            "Only the canonical MapRoomCameraControl release may end active camera control.");
        Assert.IsTrue(scenario.ControlLifecycle.IsActiveController(
            ScannerRoomScenarioFixture.CameraAId, scenario.PlayerA.SessionId));
        Assert.IsTrue((await scenario.RequestOwnershipAsync(scenario.PlayerA,
            ScannerRoomScenarioFixture.CameraAId, SimulationLockType.EXCLUSIVE)).LockAcquired);
        MapRoomCameraPreviewProcessor processor = new(scenario.Ownership, scenario.EntityRegistry,
            scenario.ControlLifecycle, scenario.Diagnostics);
        RecordingSender sender = new();

        await processor.Process(new AuthProcessorContext(scenario.PlayerA, sender),
            new MapRoomCameraPreview(ScannerRoomScenarioFixture.CameraAId, 1,
                MapRoomCameraPreviewImageTest.CreateJpeg(64, 64)));

        Assert.AreEqual(1, sender.ToAll.Count);
        Assert.AreEqual(0, sender.Replies.Count);
    }

    private static async Task<ScannerRoomScenarioFixture> CreateControlledScenarioAsync()
    {
        ScannerRoomScenarioFixture scenario = new();
        Assert.IsTrue((await scenario.DockAsync(scenario.PlayerA, ScannerRoomScenarioFixture.CameraAId, 0, true)).Granted);
        Assert.IsTrue((await scenario.ControlAsync(scenario.PlayerA, ScannerRoomScenarioFixture.CameraAId, 0, true)).Granted);
        return scenario;
    }

    private sealed class RecordingSender : IPacketSender
    {
        internal List<MapRoomCameraPreview> Replies { get; } = [];
        internal List<MapRoomCameraPreview> ToAll { get; } = [];
        internal List<MapRoomCameraPreview> ToOthers { get; } = [];

        public ValueTask SendPacketAsync<T>(T packet, SessionId sessionId) where T : Packet
        {
            if (packet is MapRoomCameraPreview preview)
            {
                Replies.Add(preview);
            }
            return ValueTask.CompletedTask;
        }

        public ValueTask SendPacketToAllAsync<T>(T packet) where T : Packet
        {
            if (packet is MapRoomCameraPreview preview)
            {
                ToAll.Add(preview);
            }
            return ValueTask.CompletedTask;
        }

        public ValueTask SendPacketToOthersAsync<T>(T packet, SessionId excludedSessionId) where T : Packet
        {
            if (packet is MapRoomCameraPreview preview)
            {
                ToOthers.Add(preview);
            }
            return ValueTask.CompletedTask;
        }
    }
}
