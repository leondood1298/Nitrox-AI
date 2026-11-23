using System.Threading.Tasks;
using Nitrox.Model.DataStructures;
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
        MapRoomCameraPreviewProcessor processor = new(scenario.Manager);
        byte[] jpeg = MapRoomCameraPreviewImageTest.CreateJpeg(256, 256);
        AuthProcessorContext context = scenario.Context(scenario.PlayerA);

        await processor.Process(context, new MapRoomCameraPreview(ScannerRoomScenarioFixture.CameraAId, 999, jpeg));
        await processor.Process(context, new MapRoomCameraPreview(ScannerRoomScenarioFixture.CameraAId, 999, jpeg));

        MapRoomCameraPreview accepted = scenario.SentToAll<MapRoomCameraPreview>().Single();
        Assert.IsTrue(accepted.IsServerResponse);
        Assert.IsTrue(accepted.Granted);
        Assert.AreEqual(1, accepted.CameraNumber, "The server must ignore the request's presentation number.");
        Assert.AreEqual(1L, accepted.Revision);
        CollectionAssert.AreEqual(jpeg, accepted.JpegBytes);
        Assert.AreEqual(0, scenario.SentToOthers<MapRoomCameraPreview>().Count, "Accepted revisions must include the sender via all-client broadcast.");
        Assert.AreEqual(1, scenario.SentTo<MapRoomCameraPreview>(scenario.PlayerA).Count(packet => !packet.Granted));
    }

    [TestMethod]
    public async Task PreviewIsShownUnderTheNumberTheServerGaveTheCamera()
    {
        ScannerRoomScenarioFixture scenario = new();
        int cameraNumber = scenario.CameraState(ScannerRoomScenarioFixture.CameraAId).CameraNumber;
        Assert.IsTrue((await scenario.ControlLooseAsync(scenario.PlayerA,
            ScannerRoomScenarioFixture.CameraAId, true)).Granted);
        MapRoomCameraPreviewProcessor processor = new(scenario.Manager);

        // Whatever number the client's own list has the camera under.
        await processor.Process(scenario.Context(scenario.PlayerA),
            new MapRoomCameraPreview(ScannerRoomScenarioFixture.CameraAId, 37,
                MapRoomCameraPreviewImageTest.CreateJpeg(64, 64)));

        MapRoomCameraPreview accepted = scenario.SentToAll<MapRoomCameraPreview>().Single();
        Assert.IsTrue(cameraNumber > 0);
        Assert.AreEqual(cameraNumber, accepted.CameraNumber);
        Assert.IsTrue(accepted.Granted);
    }

    [TestMethod]
    public async Task DuplicateControlAcquireDoesNotReopenConsumedPreview()
    {
        ScannerRoomScenarioFixture scenario = await CreateControlledScenarioAsync();
        MapRoomCameraPreviewProcessor processor = new(scenario.Manager);
        byte[] jpeg = MapRoomCameraPreviewImageTest.CreateJpeg(64, 64);
        AuthProcessorContext context = scenario.Context(scenario.PlayerA);

        await processor.Process(context, new MapRoomCameraPreview(ScannerRoomScenarioFixture.CameraAId, 1, jpeg));
        MapRoomCameraControl duplicate = await scenario.ControlAsync(scenario.PlayerA,
            ScannerRoomScenarioFixture.CameraAId, 0, true);
        Assert.IsTrue(duplicate.Granted);
        await processor.Process(context, new MapRoomCameraPreview(ScannerRoomScenarioFixture.CameraAId, 1, jpeg));

        Assert.AreEqual(1, scenario.SentToAll<MapRoomCameraPreview>().Count);
        Assert.AreEqual(1, scenario.SentTo<MapRoomCameraPreview>(scenario.PlayerA).Count(packet => !packet.Granted));
    }

    [TestMethod]
    public async Task BackgroundSimulationRefreshPreservesExclusiveControlUntilPreviewPublishes()
    {
        ScannerRoomScenarioFixture scenario = await CreateControlledScenarioAsync();
        Assert.IsTrue(scenario.EntityRegistry.TryGetEntityById(ScannerRoomScenarioFixture.CameraAId,
            out Entity cameraEntity));

        SimulatedEntity refreshedAssignment = scenario.EntitySimulation.TryAcquireEntity(scenario.PlayerA, cameraEntity, propagate: true).Single();
        Assert.AreEqual(SimulationLockType.EXCLUSIVE, refreshedAssignment.LockType,
            "A background visibility refresh must report the effective interactive lock.");
        Assert.IsTrue(scenario.Ownership.TryGetLock(ScannerRoomScenarioFixture.CameraAId,
            out SimulationOwnershipData.PlayerLock serverLock));
        Assert.AreEqual(SimulationLockType.EXCLUSIVE, serverLock.LockType,
            "A background transient acquisition must not downgrade active camera control.");

        MapRoomCameraPreviewProcessor processor = new(scenario.Manager);
        await processor.Process(scenario.Context(scenario.PlayerA),
            new MapRoomCameraPreview(ScannerRoomScenarioFixture.CameraAId, 1,
                MapRoomCameraPreviewImageTest.CreateJpeg(64, 64)));

        Assert.AreEqual(1, scenario.SentToAll<MapRoomCameraPreview>().Count,
            "The preview must remain eligible after the background assignment interleaves with control.");
        Assert.AreEqual(0, scenario.SentTo<MapRoomCameraPreview>(scenario.PlayerA).Count);
    }

    [TestMethod]
    public async Task ReleaseThenRealReacquisitionOpensOneNewPreview()
    {
        ScannerRoomScenarioFixture scenario = await CreateControlledScenarioAsync();
        MapRoomCameraPreviewProcessor processor = new(scenario.Manager);
        byte[] jpeg = MapRoomCameraPreviewImageTest.CreateJpeg(64, 64);
        AuthProcessorContext context = scenario.Context(scenario.PlayerA);

        await processor.Process(context, new MapRoomCameraPreview(ScannerRoomScenarioFixture.CameraAId, 1, jpeg));
        Assert.IsTrue((await scenario.ControlAsync(scenario.PlayerA, ScannerRoomScenarioFixture.CameraAId, 0, false)).Granted);
        Assert.IsTrue((await scenario.ControlAsync(scenario.PlayerA, ScannerRoomScenarioFixture.CameraAId, 0, true)).Granted);
        await processor.Process(context, new MapRoomCameraPreview(ScannerRoomScenarioFixture.CameraAId, 1, jpeg));

        CollectionAssert.AreEqual(new long[] { 1, 2 }, scenario.SentToAll<MapRoomCameraPreview>().Select(packet => packet.Revision).ToArray());
    }

    [TestMethod]
    public async Task SequentialControllersPublishCanonicalRevisionsToAllClientsIncludingEachSender()
    {
        ScannerRoomScenarioFixture scenario = await CreateControlledScenarioAsync();
        MapRoomCameraPreviewProcessor processor = new(scenario.Manager);
        byte[] jpeg = MapRoomCameraPreviewImageTest.CreateJpeg(64, 64);

        await processor.Process(scenario.Context(scenario.PlayerA),
            new MapRoomCameraPreview(ScannerRoomScenarioFixture.CameraAId, 1, jpeg));
        Assert.IsTrue((await scenario.ControlAsync(scenario.PlayerA,
            ScannerRoomScenarioFixture.CameraAId, 0, false)).Granted);
        Assert.IsTrue((await scenario.ControlAsync(scenario.PlayerB,
            ScannerRoomScenarioFixture.CameraAId, 0, true)).Granted);

        await processor.Process(scenario.Context(scenario.PlayerB),
            new MapRoomCameraPreview(ScannerRoomScenarioFixture.CameraAId, 1, jpeg));

        CollectionAssert.AreEqual(new long[] { 1, 2 }, scenario.SentToAll<MapRoomCameraPreview>().Select(packet => packet.Revision).ToArray());
        Assert.AreEqual(0, scenario.SentToOthers<MapRoomCameraPreview>().Count);
    }

    [TestMethod]
    public async Task NonOwnerCannotConsumeControllersPreviewOpportunity()
    {
        ScannerRoomScenarioFixture scenario = await CreateControlledScenarioAsync();
        MapRoomCameraPreviewProcessor processor = new(scenario.Manager);
        byte[] jpeg = MapRoomCameraPreviewImageTest.CreateJpeg(64, 64);

        await processor.Process(scenario.Context(scenario.PlayerB),
            new MapRoomCameraPreview(ScannerRoomScenarioFixture.CameraAId, 1, jpeg));
        await processor.Process(scenario.Context(scenario.PlayerA),
            new MapRoomCameraPreview(ScannerRoomScenarioFixture.CameraAId, 1, jpeg));

        Assert.AreEqual(1, scenario.SentTo<MapRoomCameraPreview>(scenario.PlayerB).Count(packet => !packet.Granted));
        Assert.AreEqual(1, scenario.SentToAll<MapRoomCameraPreview>().Count);
    }

    [TestMethod]
    public async Task MalformedOwnerPayloadConsumesTheSingleOpportunity()
    {
        ScannerRoomScenarioFixture scenario = await CreateControlledScenarioAsync();
        MapRoomCameraPreviewProcessor processor = new(scenario.Manager);
        AuthProcessorContext context = scenario.Context(scenario.PlayerA);

        await processor.Process(context, new MapRoomCameraPreview(ScannerRoomScenarioFixture.CameraAId, 1, [1, 2, 3]));
        await processor.Process(context, new MapRoomCameraPreview(ScannerRoomScenarioFixture.CameraAId, 1,
            MapRoomCameraPreviewImageTest.CreateJpeg(64, 64)));

        Assert.AreEqual(0, scenario.SentToAll<MapRoomCameraPreview>().Count);
        Assert.AreEqual(2, scenario.SentTo<MapRoomCameraPreview>(scenario.PlayerA).Count(packet => !packet.Granted));
    }

    [TestMethod]
    public async Task CanonicalCleanupCannotLeaveReusablePreviewEligibility()
    {
        ScannerRoomScenarioFixture scenario = await CreateControlledScenarioAsync();
        Assert.IsTrue(scenario.Ownership.RevokeOwnerOfId(ScannerRoomScenarioFixture.CameraAId,
            out SimulationOwnershipData.PlayerLock revoked));
        Assert.IsTrue(scenario.Manager.TryCreateControlRelease(ScannerRoomScenarioFixture.CameraAId, revoked,
            "test_cleanup", out _));
        Assert.IsTrue(scenario.Ownership.TryAcquire(ScannerRoomScenarioFixture.CameraAId, scenario.PlayerA,
            SimulationLockType.EXCLUSIVE));
        MapRoomCameraPreviewProcessor processor = new(scenario.Manager);

        await processor.Process(scenario.Context(scenario.PlayerA),
            new MapRoomCameraPreview(ScannerRoomScenarioFixture.CameraAId, 1,
                MapRoomCameraPreviewImageTest.CreateJpeg(64, 64)));

        Assert.AreEqual(0, scenario.SentToAll<MapRoomCameraPreview>().Count);
        Assert.AreEqual(1, scenario.SentTo<MapRoomCameraPreview>(scenario.PlayerA).Count(packet => !packet.Granted));
    }

    [TestMethod]
    public async Task CleanupOfLegacyTransientLockClearsConsumedEligibilityBeforeReacquisition()
    {
        ScannerRoomScenarioFixture scenario = await CreateControlledScenarioAsync();
        MapRoomCameraPreviewProcessor processor = new(scenario.Manager);
        byte[] jpeg = MapRoomCameraPreviewImageTest.CreateJpeg(64, 64);
        AuthProcessorContext context = scenario.Context(scenario.PlayerA);
        await processor.Process(context,
            new MapRoomCameraPreview(ScannerRoomScenarioFixture.CameraAId, 1, jpeg));

        // Model a lock left transient by an older/background path. Cleanup emits no control-release packet
        // for it, but must still remove the consumed preview acquisition state.
        Assert.IsTrue(scenario.Ownership.TryAcquire(ScannerRoomScenarioFixture.CameraAId, scenario.PlayerA,
            SimulationLockType.TRANSIENT));
        Assert.IsTrue(scenario.Ownership.RevokeOwnerOfId(ScannerRoomScenarioFixture.CameraAId,
            out SimulationOwnershipData.PlayerLock revoked));
        Assert.AreEqual(SimulationLockType.TRANSIENT, revoked.LockType);
        Assert.IsFalse(scenario.Manager.TryCreateControlRelease(ScannerRoomScenarioFixture.CameraAId, revoked,
            "legacy_transient_cleanup", out _));

        Assert.IsTrue((await scenario.ControlAsync(scenario.PlayerA, ScannerRoomScenarioFixture.CameraAId, 0, true)).Granted);
        await processor.Process(context,
            new MapRoomCameraPreview(ScannerRoomScenarioFixture.CameraAId, 1, jpeg));

        CollectionAssert.AreEqual(new long[] { 1, 2 }, scenario.SentToAll<MapRoomCameraPreview>().Select(packet => packet.Revision).ToArray());
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
        Assert.IsTrue(scenario.IsController(
            ScannerRoomScenarioFixture.CameraAId, scenario.PlayerA.SessionId));
        Assert.IsTrue((await scenario.RequestOwnershipAsync(scenario.PlayerA,
            ScannerRoomScenarioFixture.CameraAId, SimulationLockType.EXCLUSIVE)).LockAcquired);
        MapRoomCameraPreviewProcessor processor = new(scenario.Manager);

        await processor.Process(scenario.Context(scenario.PlayerA),
            new MapRoomCameraPreview(ScannerRoomScenarioFixture.CameraAId, 1,
                MapRoomCameraPreviewImageTest.CreateJpeg(64, 64)));

        Assert.AreEqual(1, scenario.SentToAll<MapRoomCameraPreview>().Count);
        Assert.AreEqual(0, scenario.SentTo<MapRoomCameraPreview>(scenario.PlayerA).Count);
    }

    private static async Task<ScannerRoomScenarioFixture> CreateControlledScenarioAsync()
    {
        ScannerRoomScenarioFixture scenario = new();
        Assert.IsTrue((await scenario.DockAsync(scenario.PlayerA, ScannerRoomScenarioFixture.CameraAId, 0, true)).Granted);
        Assert.IsTrue((await scenario.ControlAsync(scenario.PlayerA, ScannerRoomScenarioFixture.CameraAId, 0, true)).Granted);
        return scenario;
    }

    [TestMethod]
    public async Task PreviewForAnIdThatNeverHadControlIsDeniedWithoutMarkingItAsACamera()
    {
        // Any entity id can be put in a preview packet. It must not become a "known camera", because ownership
        // requests for known ids that are not scanner cameras are denied (which would lock out e.g. a vehicle).
        ScannerRoomScenarioFixture scenario = new();
        MapRoomCameraPreviewProcessor processor = new(scenario.Manager);
        NitroxId unrelatedEntityId = new();

        await processor.Process(scenario.Context(scenario.PlayerA),
            new MapRoomCameraPreview(unrelatedEntityId, 1, MapRoomCameraPreviewImageTest.CreateJpeg(64, 64)));

        Assert.IsFalse(scenario.SentTo<MapRoomCameraPreview>(scenario.PlayerA).Single().Granted);
        Assert.AreEqual(0, scenario.SentToAll<MapRoomCameraPreview>().Count);
        Assert.IsFalse(scenario.Manager.IsScannerCamera(unrelatedEntityId));
    }
}
