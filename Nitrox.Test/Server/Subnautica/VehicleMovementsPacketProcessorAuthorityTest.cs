using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nitrox.Model.Core;
using Nitrox.Model.DataStructures;
using Nitrox.Model.DataStructures.Unity;
using Nitrox.Model.GameLogic.PlayerAnimation;
using Nitrox.Model.Packets;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Bases;
using Nitrox.Model.Subnautica.MultiplayerSession;
using Nitrox.Model.Subnautica.Packets;
using Nitrox.Server.Subnautica.Models.Packets.Core;
using Nitrox.Server.Subnautica.Models.Packets.Processors;
using Nitrox.Server.Subnautica.Models.Serialization.World;

namespace Nitrox.Test.Server.Subnautica;

[TestClass]
public sealed class VehicleMovementsPacketProcessorAuthorityTest
{
    [DataTestMethod]
    [DataRow(0, false)]
    [DataRow(1, true)]
    [DataRow(VehicleMovementsPacketProcessor.MAX_MOVEMENTS_PER_PACKET, true)]
    [DataRow(VehicleMovementsPacketProcessor.MAX_MOVEMENTS_PER_PACKET + 1, false)]
    public void MovementBatchCountIsBounded(int count, bool expected)
    {
        Assert.AreEqual(expected, VehicleMovementsPacketProcessor.IsValidMovementCount(count));
    }

    [TestMethod]
    public void MovementRealTimeMustFitNonnegativeFiniteFloatRange()
    {
        Assert.IsTrue(VehicleMovementsPacketProcessor.IsValidRealTime(0d));
        Assert.IsTrue(VehicleMovementsPacketProcessor.IsValidRealTime(float.MaxValue));
        Assert.IsFalse(VehicleMovementsPacketProcessor.IsValidRealTime(-double.Epsilon));
        Assert.IsFalse(VehicleMovementsPacketProcessor.IsValidRealTime(float.MinValue));
        Assert.IsFalse(VehicleMovementsPacketProcessor.IsValidRealTime(double.MinValue));
        Assert.IsFalse(VehicleMovementsPacketProcessor.IsValidRealTime(double.MaxValue));
        Assert.IsFalse(VehicleMovementsPacketProcessor.IsValidRealTime(double.NaN));
        Assert.IsFalse(VehicleMovementsPacketProcessor.IsValidRealTime(double.PositiveInfinity));
    }

    [TestMethod]
    public void MovementValidationRejectsNonFiniteAndDegenerateTransforms()
    {
        NitroxId id = ScannerRoomScenarioFixture.CameraAId;
        Assert.IsTrue(VehicleMovementsPacketProcessor.IsFiniteMovement(
            new SimpleMovementData(id, new NitroxVector3(1f, 2f, 3f), NitroxQuaternion.Identity)));
        Assert.IsFalse(VehicleMovementsPacketProcessor.IsFiniteMovement(
            new SimpleMovementData(id, new NitroxVector3(float.NaN, 2f, 3f), NitroxQuaternion.Identity)));
        Assert.IsFalse(VehicleMovementsPacketProcessor.IsFiniteMovement(
            new SimpleMovementData(id, new NitroxVector3(1f, 2f, 3f), new NitroxQuaternion(0f, 0f, 0f, 0f))));
        Assert.IsFalse(VehicleMovementsPacketProcessor.IsFiniteMovement(
            new ExosuitMovementData(id, new NitroxVector3(1f, 2f, 3f), NitroxQuaternion.Identity,
                new NitroxVector3(float.PositiveInfinity, 0f, 0f), NitroxVector3.Zero, 0, 0, false, true)));
    }

    [TestMethod]
    public async Task UnauthorizedCameraMovementCannotMutateOrRebroadcast()
    {
        MovementScenario test = new();
        Assert.IsTrue(test.Scenario.Ownership.TryToAcquire(ScannerRoomScenarioFixture.CameraAId, test.Scenario.PlayerA, SimulationLockType.EXCLUSIVE));
        SimpleMovementData movement = CreateMovement(ScannerRoomScenarioFixture.CameraAId, 1f);

        await test.Processor.Process(
            new AuthProcessorContext(test.Scenario.PlayerB, test.Sender),
            new VehicleMovements([movement], 10d));

        AssertCanonicalTransform(test.Entity(ScannerRoomScenarioFixture.CameraAId), NitroxVector3.Zero, NitroxQuaternion.Identity);
        Assert.AreEqual(0, test.Sender.OthersPackets);
    }

    [TestMethod]
    public async Task MixedBatchForwardsOnlyOwnedEntriesInOriginalOrder()
    {
        MovementScenario test = new();
        Assert.IsTrue(test.Scenario.Ownership.TryToAcquire(ScannerRoomScenarioFixture.CameraAId, test.Scenario.PlayerA, SimulationLockType.EXCLUSIVE));
        Assert.IsTrue(test.Scenario.Ownership.TryToAcquire(ScannerRoomScenarioFixture.CameraBId, test.Scenario.PlayerB, SimulationLockType.EXCLUSIVE));
        Assert.IsTrue(test.Scenario.Ownership.TryToAcquire(ScannerRoomScenarioFixture.CameraCId, test.Scenario.PlayerA, SimulationLockType.TRANSIENT));
        SimpleMovementData ownedA = CreateMovement(ScannerRoomScenarioFixture.CameraAId, 1f);
        SimpleMovementData foreignB = CreateMovement(ScannerRoomScenarioFixture.CameraBId, 2f);
        SimpleMovementData ownedC = CreateMovement(ScannerRoomScenarioFixture.CameraCId, 3f);
        VehicleMovements request = new([ownedA, foreignB, ownedC], 42.5d);

        await test.Processor.Process(new AuthProcessorContext(test.Scenario.PlayerA, test.Sender), request);

        VehicleMovements forwarded = test.Sender.Single<VehicleMovements>();
        Assert.AreEqual(42.5d, forwarded.RealTime);
        CollectionAssert.AreEqual(
            new[] { ScannerRoomScenarioFixture.CameraAId, ScannerRoomScenarioFixture.CameraCId },
            forwarded.Data.Select(movement => movement.Id).ToArray());
        Assert.AreSame(ownedA, forwarded.Data[0]);
        Assert.AreSame(ownedC, forwarded.Data[1]);
        Assert.AreEqual(3, request.Data.Count, "Sanitization must not mutate the received packet list.");
        AssertCanonicalTransform(test.Entity(ScannerRoomScenarioFixture.CameraAId), ownedA.Position, ownedA.Rotation);
        AssertCanonicalTransform(test.Entity(ScannerRoomScenarioFixture.CameraBId), NitroxVector3.Zero, NitroxQuaternion.Identity);
        AssertCanonicalTransform(test.Entity(ScannerRoomScenarioFixture.CameraCId), ownedC.Position, ownedC.Rotation);
    }

    [TestMethod]
    public void OrdinaryWorldMovementDoesNotInspectScannerRoomTopology()
    {
        MovementScenario test = new();
        NitroxId ordinaryId = new("00000000-0000-0000-0000-000000000298");
        GlobalRootEntity ordinaryEntity = new(
            new NitroxTransform(),
            GlobalRootEntity.GLOBAL_ROOT_LEVEL,
            "ordinary-moving-entity",
            true,
            ordinaryId,
            new NitroxTechType("Stalker"),
            null,
            null,
            []);
        ordinaryEntity.Transform.LocalPosition = NitroxVector3.Zero;
        ordinaryEntity.Transform.LocalRotation = NitroxQuaternion.Identity;
        ordinaryEntity.Transform.LocalScale = NitroxVector3.One;
        test.Scenario.EntityRegistry.AddEntity(ordinaryEntity);
        Assert.IsTrue(test.Scenario.Ownership.TryToAcquire(
            ordinaryId, test.Scenario.PlayerA, SimulationLockType.TRANSIENT));
        SimpleMovementData movement = CreateMovement(ordinaryId, 3.5f);

        Task processing;
        bool completedWhileRoomLocked;
        lock (test.Scenario.Room)
        {
            processing = Task.Run(() => test.Processor.Process(
                new AuthProcessorContext(test.Scenario.PlayerA, test.Sender),
                new VehicleMovements([movement], 43d)));
            completedWhileRoomLocked = processing.Wait(TimeSpan.FromMilliseconds(500));
        }
        processing.Wait(TimeSpan.FromSeconds(5));

        Assert.IsTrue(completedWhileRoomLocked,
            "An ordinary non-camera movement batch must not wait for Scanner Room topology locks.");
        VehicleMovements forwarded = test.Sender.Single<VehicleMovements>();
        Assert.AreSame(movement, forwarded.Data.Single());
        AssertCanonicalTransform(ordinaryEntity, movement.Position, movement.Rotation);
    }

    [TestMethod]
    public async Task MalformedEntriesCannotMutateOrRebroadcast()
    {
        MovementScenario test = new();
        foreach (NitroxId cameraId in test.Scenario.CameraIds)
        {
            Assert.IsTrue(test.Scenario.Ownership.TryToAcquire(cameraId, test.Scenario.PlayerA, SimulationLockType.EXCLUSIVE));
        }
        List<MovementData> malformed =
        [
            new SimpleMovementData(ScannerRoomScenarioFixture.CameraAId, new NitroxVector3(float.NaN, 0f, 0f), NitroxQuaternion.Identity),
            new SimpleMovementData(ScannerRoomScenarioFixture.CameraBId, NitroxVector3.Zero, new NitroxQuaternion(0f, 0f, 0f, float.NegativeInfinity)),
            new ExosuitMovementData(ScannerRoomScenarioFixture.CameraCId, NitroxVector3.Zero, NitroxQuaternion.Identity,
                new NitroxVector3(float.PositiveInfinity, 0f, 0f), NitroxVector3.Zero, 0, 0, false, true),
            new SimpleMovementData(ScannerRoomScenarioFixture.CameraAId, NitroxVector3.Zero, new NitroxQuaternion(0f, 0f, 0f, 0f))
        ];

        await test.Processor.Process(
            new AuthProcessorContext(test.Scenario.PlayerA, test.Sender),
            new VehicleMovements(malformed, 1d));

        foreach (NitroxId cameraId in test.Scenario.CameraIds)
        {
            AssertCanonicalTransform(test.Entity(cameraId), NitroxVector3.Zero, NitroxQuaternion.Identity);
        }
        Assert.AreEqual(0, test.Sender.OthersPackets);
    }

    [TestMethod]
    public async Task EmptyOversizedAndOutOfRangeTimeBatchesAreRejectedAtomically()
    {
        MovementScenario test = new();
        Assert.IsTrue(test.Scenario.Ownership.TryToAcquire(ScannerRoomScenarioFixture.CameraAId, test.Scenario.PlayerA, SimulationLockType.EXCLUSIVE));
        AuthProcessorContext context = new(test.Scenario.PlayerA, test.Sender);
        SimpleMovementData movement = CreateMovement(ScannerRoomScenarioFixture.CameraAId, 4f);
        List<MovementData> oversized = Enumerable.Range(0, VehicleMovementsPacketProcessor.MAX_MOVEMENTS_PER_PACKET + 1)
            .Select(index => (MovementData)CreateMovement(ScannerRoomScenarioFixture.CameraAId, index + 1f))
            .ToList();

        await test.Processor.Process(context, new VehicleMovements([], 1d));
        await test.Processor.Process(context, new VehicleMovements(oversized, 1d));
        await test.Processor.Process(context, new VehicleMovements([movement], double.NaN));
        await test.Processor.Process(context, new VehicleMovements([movement], double.PositiveInfinity));
        await test.Processor.Process(context, new VehicleMovements([movement], -1d));
        await test.Processor.Process(context, new VehicleMovements([movement], double.MinValue));
        await test.Processor.Process(context, new VehicleMovements([movement], double.MaxValue));

        AssertCanonicalTransform(test.Entity(ScannerRoomScenarioFixture.CameraAId), NitroxVector3.Zero, NitroxQuaternion.Identity);
        Assert.AreEqual(0, test.Sender.OthersPackets);
    }

    [TestMethod]
    public async Task OwnedExclusiveCameraMovementIsAppliedAndForwarded()
    {
        MovementScenario test = new();
        Assert.IsTrue(test.Scenario.Ownership.TryToAcquire(ScannerRoomScenarioFixture.CameraAId, test.Scenario.PlayerA, SimulationLockType.EXCLUSIVE));
        SimpleMovementData movement = CreateMovement(ScannerRoomScenarioFixture.CameraAId, 7f);

        await test.Processor.Process(
            new AuthProcessorContext(test.Scenario.PlayerA, test.Sender),
            new VehicleMovements([movement], 1533d));

        VehicleMovements forwarded = test.Sender.Single<VehicleMovements>();
        Assert.AreEqual(1, forwarded.Data.Count);
        Assert.AreSame(movement, forwarded.Data[0]);
        Assert.AreEqual(1533d, forwarded.RealTime);
        AssertCanonicalTransform(test.Entity(ScannerRoomScenarioFixture.CameraAId), movement.Position, movement.Rotation);
    }

    [TestMethod]
    public async Task DockedKnownWorldCameraRequiresValidatedActiveExclusiveControl()
    {
        MovementScenario test = new();
        NitroxId cameraId = ScannerRoomScenarioFixture.CameraAId;
        Assert.IsTrue((await test.Scenario.DockAsync(
            test.Scenario.PlayerA, cameraId, 0, true)).Granted);
        Assert.IsTrue(test.Scenario.Ownership.TryToAcquire(
            cameraId, test.Scenario.PlayerA, SimulationLockType.TRANSIENT));

        await test.Processor.Process(
            new AuthProcessorContext(test.Scenario.PlayerA, test.Sender),
            new VehicleMovements([CreateMovement(cameraId, 7.1f)], 1533.1d));

        Assert.AreEqual(0, test.Sender.OthersPackets);
        AssertCanonicalTransform(test.Entity(cameraId),
            NitroxVector3.Zero, NitroxQuaternion.Identity);

        Assert.IsTrue(test.Scenario.Ownership.TryToAcquire(
            cameraId, test.Scenario.PlayerA, SimulationLockType.EXCLUSIVE));
        await test.Processor.Process(
            new AuthProcessorContext(test.Scenario.PlayerA, test.Sender),
            new VehicleMovements([CreateMovement(cameraId, 7.2f)], 1533.2d));

        Assert.AreEqual(0, test.Sender.OthersPackets,
            "An exclusive lock without canonical control must not move a docked camera.");
        AssertCanonicalTransform(test.Entity(cameraId),
            NitroxVector3.Zero, NitroxQuaternion.Identity);

        Assert.IsTrue((await test.Scenario.ControlAsync(
            test.Scenario.PlayerA, cameraId, 0, true)).Granted);
        SimpleMovementData authorizedMovement = CreateMovement(cameraId, 7.3f);
        await test.Processor.Process(
            new AuthProcessorContext(test.Scenario.PlayerA, test.Sender),
            new VehicleMovements([authorizedMovement], 1533.3d));

        Assert.AreEqual(1, test.Sender.OthersPackets);
        AssertCanonicalTransform(test.Entity(cameraId),
            authorizedMovement.Position, authorizedMovement.Rotation);
    }

    [TestMethod]
    public async Task ScannerRegisteredIdWithWrongWorldTechTypeCannotMove()
    {
        MovementScenario test = new();
        NitroxId cameraId = ScannerRoomScenarioFixture.CameraAId;
        Assert.IsTrue((await test.Scenario.DockAsync(
            test.Scenario.PlayerA, cameraId, 0, true)).Granted);
        Assert.IsTrue(test.Scenario.EntityRegistry.RemoveEntity(cameraId).HasValue);
        GlobalRootEntity collision = new(
            new NitroxTransform(),
            GlobalRootEntity.GLOBAL_ROOT_LEVEL,
            "scanner-camera-id-collision",
            true,
            cameraId,
            new NitroxTechType("Titanium"),
            null,
            null,
            []);
        collision.Transform.LocalPosition = NitroxVector3.Zero;
        collision.Transform.LocalRotation = NitroxQuaternion.Identity;
        collision.Transform.LocalScale = NitroxVector3.One;
        test.Scenario.EntityRegistry.AddEntity(collision);
        Assert.IsTrue(test.Scenario.Ownership.TryToAcquire(
            cameraId, test.Scenario.PlayerA, SimulationLockType.EXCLUSIVE));

        await test.Processor.Process(
            new AuthProcessorContext(test.Scenario.PlayerA, test.Sender),
            new VehicleMovements([CreateMovement(cameraId, 7.4f)], 1533.4d));

        Assert.AreEqual(0, test.Sender.OthersPackets);
        AssertCanonicalTransform(collision,
            NitroxVector3.Zero, NitroxQuaternion.Identity);
    }

    [TestMethod]
    public void RestoredRegistrationSeedsKnownIndexBeforeAutomaticWrongTechMovement()
    {
        MovementScenario test = new();
        NitroxId cameraId = ScannerRoomScenarioFixture.CameraAId;
        lock (test.Scenario.Room)
        {
            Assert.AreEqual(1,
                test.Scenario.Room.GetOrAssignCameraNumber(cameraId, 1));
        }
        Assert.IsFalse(test.Scenario.ControlLifecycle.IsKnown(cameraId),
            "A directly restored registration must begin outside runtime dock/control paths.");

        Assert.IsTrue(test.Scenario.EntityRegistry.RemoveEntity(cameraId).HasValue);
        GlobalRootEntity collision = new(
            new NitroxTransform(),
            GlobalRootEntity.GLOBAL_ROOT_LEVEL,
            "restored-scanner-camera-id-collision",
            true,
            cameraId,
            new NitroxTechType("Titanium"),
            null,
            null,
            []);
        collision.Transform.LocalPosition = NitroxVector3.Zero;
        collision.Transform.LocalRotation = NitroxQuaternion.Identity;
        collision.Transform.LocalScale = NitroxVector3.One;
        test.Scenario.EntityRegistry.AddEntity(collision);

        Assert.AreEqual(1, WorldService.SeedLoadedMapRoomCameraLifecycles(
            test.Scenario.EntityRegistry.GetEntities<MapRoomEntity>(),
            test.Scenario.ControlLifecycle));
        Assert.IsTrue(test.Scenario.ControlLifecycle.IsKnown(cameraId));
        Assert.IsTrue(test.Scenario.Ownership.TryToAcquire(
            cameraId, test.Scenario.PlayerA, SimulationLockType.TRANSIENT));

        using ManualResetEventSlim processingStarted = new();
        Task processing;
        bool completedWhileRoomLocked;
        lock (test.Scenario.Room)
        {
            processing = Task.Run(async () =>
            {
                processingStarted.Set();
                await test.Processor.Process(
                    new AuthProcessorContext(test.Scenario.PlayerA, test.Sender),
                    new VehicleMovements([CreateMovement(cameraId, 7.45f)], 1533.45d));
            });
            Assert.IsTrue(processingStarted.Wait(TimeSpan.FromSeconds(5)));
            completedWhileRoomLocked =
                processing.Wait(TimeSpan.FromMilliseconds(250));
        }
        Assert.IsTrue(processing.Wait(TimeSpan.FromSeconds(5)));

        Assert.IsFalse(completedWhileRoomLocked,
            "A seeded restored camera ID must consult its Scanner Room topology snapshot.");
        Assert.AreEqual(0, test.Sender.OthersPackets);
        AssertCanonicalTransform(collision,
            NitroxVector3.Zero, NitroxQuaternion.Identity);
    }

    [TestMethod]
    public async Task OwnedUniquelyRegisteredCameraWithoutWorldEntityIsForwardedWithoutCreatingEntity()
    {
        MovementScenario test = new();
        NitroxId cameraId = ScannerRoomScenarioFixture.CameraAId;
        RegisterCameraWithoutWorldEntity(test, test.Scenario.Room, cameraId, 1, dockingSlot: 0);
        MapRoomCameraControl control = await test.Scenario.ControlAsync(
            test.Scenario.PlayerA, cameraId, 0, true);
        Assert.IsTrue(control.Granted);
        Assert.IsTrue(test.Scenario.ControlLifecycle.IsActiveController(
            cameraId, test.Scenario.PlayerA.SessionId));
        SimpleMovementData movement = CreateMovement(cameraId, 7.5f);

        await test.Processor.Process(
            new AuthProcessorContext(test.Scenario.PlayerA, test.Sender),
            new VehicleMovements([movement], 1534d));

        VehicleMovements forwarded = test.Sender.Single<VehicleMovements>();
        Assert.AreEqual(1, forwarded.Data.Count);
        Assert.AreSame(movement, forwarded.Data[0]);
        Assert.AreEqual(1534d, forwarded.RealTime);
        Assert.IsFalse(test.Scenario.EntityRegistry.GetEntityById(cameraId).HasValue,
            "Relaying bootstrap movement must not create or persist a phantom camera entity.");
        lock (test.Scenario.Room)
        {
            Assert.AreEqual(cameraId, test.Scenario.Room.GetCameraRecord(cameraId)?.CameraId);
        }
    }

    [TestMethod]
    public async Task RegisteredCameraWithoutWorldEntityRequiresValidatedActiveController()
    {
        MovementScenario test = new();
        NitroxId cameraId = ScannerRoomScenarioFixture.CameraAId;
        RegisterCameraWithoutWorldEntity(test, test.Scenario.Room, cameraId, 1, dockingSlot: 0);
        Assert.IsTrue(test.Scenario.Ownership.TryToAcquire(
            cameraId, test.Scenario.PlayerA, SimulationLockType.EXCLUSIVE));
        Assert.IsFalse(test.Scenario.ControlLifecycle.IsActiveController(
            cameraId, test.Scenario.PlayerA.SessionId));

        await test.Processor.Process(
            new AuthProcessorContext(test.Scenario.PlayerA, test.Sender),
            new VehicleMovements([CreateMovement(cameraId, 8f)], 1535d));

        Assert.AreEqual(0, test.Sender.OthersPackets);
        Assert.IsFalse(test.Scenario.EntityRegistry.GetEntityById(cameraId).HasValue);
    }

    [TestMethod]
    public async Task MissingWorldEntityFallbackRejectsUnknownAndAmbiguousCameraIds()
    {
        MovementScenario test = new();
        NitroxId unknownId = new("00000000-0000-0000-0000-000000000299");
        NitroxId ambiguousId = ScannerRoomScenarioFixture.CameraAId;
        RegisterCameraWithoutWorldEntity(test, test.Scenario.Room, ambiguousId, 1, dockingSlot: 0);
        MapRoomEntity duplicateRoom = new(
            new NitroxId("00000000-0000-0000-0000-000000000102"),
            ScannerRoomScenarioFixture.RoomParentId,
            new NitroxInt3(5, -2, 9));
        duplicateRoom.GetOrAssignCameraNumber(ambiguousId, 1);
        test.Scenario.EntityRegistry.AddEntity(duplicateRoom);
        Assert.IsTrue(test.Scenario.Ownership.TryToAcquire(unknownId, test.Scenario.PlayerA, SimulationLockType.EXCLUSIVE));
        Assert.IsTrue((await test.Scenario.ControlAsync(
            test.Scenario.PlayerA, ambiguousId, 0, true)).Granted);

        AuthProcessorContext context = new(test.Scenario.PlayerA, test.Sender);
        await test.Processor.Process(context, new VehicleMovements([CreateMovement(unknownId, 8.5f)], 1536d));
        await test.Processor.Process(context, new VehicleMovements([CreateMovement(ambiguousId, 9f)], 1537d));

        Assert.AreEqual(0, test.Sender.OthersPackets);
        Assert.IsFalse(test.Scenario.EntityRegistry.GetEntityById(unknownId).HasValue);
        Assert.IsFalse(test.Scenario.EntityRegistry.GetEntityById(ambiguousId).HasValue);
    }

    [TestMethod]
    public async Task MissingWorldEntityFallbackAcceptsOnlyFiniteSimpleMovement()
    {
        MovementScenario test = new();
        NitroxId cameraId = ScannerRoomScenarioFixture.CameraAId;
        RegisterCameraWithoutWorldEntity(test, test.Scenario.Room, cameraId, 1, dockingSlot: 0);
        Assert.IsTrue((await test.Scenario.ControlAsync(
            test.Scenario.PlayerA, cameraId, 0, true)).Granted);
        test.Scenario.PlayerA.PlayerContext = CreatePlayerContext(test.Scenario.PlayerA, cameraId);
        AuthProcessorContext context = new(test.Scenario.PlayerA, test.Sender);
        SimpleMovementData nonFinite = new(cameraId,
            new NitroxVector3(float.NaN, 0f, 0f), NitroxQuaternion.Identity);
        DrivenVehicleMovementData driven = new(cameraId,
            new NitroxVector3(8f, 9f, 10f), NitroxQuaternion.Identity, 0, 0, true);

        await test.Processor.Process(context, new VehicleMovements([nonFinite], 1538d));
        await test.Processor.Process(context, new VehicleMovements([driven], 1539d));

        Assert.AreEqual(0, test.Sender.OthersPackets);
        Assert.IsFalse(test.Scenario.EntityRegistry.GetEntityById(cameraId).HasValue);
    }

    [TestMethod]
    public async Task OwnershipCannotBeRevokedUntilAcceptedMovementIsQueued()
    {
        MovementScenario test = new();
        NitroxId cameraId = ScannerRoomScenarioFixture.CameraAId;
        Assert.IsTrue(test.Scenario.Ownership.TryToAcquire(cameraId, test.Scenario.PlayerA, SimulationLockType.EXCLUSIVE));
        using ManualResetEventSlim sendEntered = new();
        using ManualResetEventSlim continueSend = new();
        test.Sender.SendEntered = sendEntered;
        test.Sender.ContinueSend = continueSend;

        Task movement = Task.Run(() => test.Processor.Process(
            new AuthProcessorContext(test.Scenario.PlayerA, test.Sender),
            new VehicleMovements([CreateMovement(cameraId, 9f)], 10d)));
        Assert.IsTrue(sendEntered.Wait(TimeSpan.FromSeconds(5)), "Movement did not reach the packet enqueue boundary.");

        Task<bool> revoke = Task.Run(() => test.Scenario.Ownership.RevokeOwnerOfId(cameraId));
        try
        {
            Task first = await Task.WhenAny(revoke, Task.Delay(100));
            Assert.AreNotSame(revoke, first, "Revocation overtook an accepted movement that was not yet queued.");
        }
        finally
        {
            continueSend.Set();
        }

        await movement.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.IsTrue(await revoke.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.AreEqual(1, test.Sender.OthersPackets);
    }

    [TestMethod]
    public async Task DrivenMovementRequiresCanonicalVehicleAndRetainsPlayerPositionBehavior()
    {
        MovementScenario test = new();
        NitroxId cameraId = ScannerRoomScenarioFixture.CameraAId;
        Assert.IsTrue(test.Scenario.Ownership.TryToAcquire(cameraId, test.Scenario.PlayerA, SimulationLockType.EXCLUSIVE));
        DrivenVehicleMovementData movement = new(cameraId, new NitroxVector3(8f, 9f, 10f), NitroxQuaternion.Identity, 0, 0, true);

        await test.Processor.Process(
            new AuthProcessorContext(test.Scenario.PlayerA, test.Sender),
            new VehicleMovements([movement], 1d));
        Assert.AreEqual(0, test.Sender.OthersPackets);
        AssertCanonicalTransform(test.Entity(cameraId), NitroxVector3.Zero, NitroxQuaternion.Identity);

        test.Scenario.PlayerA.PlayerContext = CreatePlayerContext(test.Scenario.PlayerA, cameraId);
        await test.Processor.Process(
            new AuthProcessorContext(test.Scenario.PlayerA, test.Sender),
            new VehicleMovements([movement], 2d));

        Assert.AreEqual(1, test.Sender.OthersPackets);
        AssertCanonicalTransform(test.Entity(cameraId), movement.Position, movement.Rotation);
        Assert.AreEqual(movement.Position, test.Scenario.PlayerA.Position);
        Assert.AreEqual(movement.Rotation, test.Scenario.PlayerA.Rotation);
    }

    private static SimpleMovementData CreateMovement(NitroxId id, float offset) => new(
        id,
        new NitroxVector3(offset, offset + 0.25f, offset + 0.5f),
        NitroxQuaternion.Identity);

    private static void RegisterCameraWithoutWorldEntity(MovementScenario test, MapRoomEntity room,
        NitroxId cameraId, int cameraNumber, int? dockingSlot = null)
    {
        Assert.IsTrue(test.Scenario.EntityRegistry.RemoveEntity(cameraId).HasValue);
        lock (room)
        {
            Assert.AreEqual(cameraNumber, room.GetOrAssignCameraNumber(cameraId, cameraNumber));
            if (dockingSlot.HasValue)
            {
                room.SetDockedCamera(dockingSlot.Value, cameraId);
            }
        }
        Assert.IsFalse(test.Scenario.EntityRegistry.GetEntityById(cameraId).HasValue);
    }

    private static PlayerContext CreatePlayerContext(Nitrox.Server.Subnautica.Models.Player player, NitroxId drivingVehicle) => new(
        player.Name,
        player.SessionId,
        player.GameObjectId,
        false,
        new PlayerSettings(new NitroxColor(0.1f, 0.2f, 0.3f)),
        false,
        player.GameMode,
        drivingVehicle,
        IntroCinematicMode.NONE,
        new PlayerAnimation(AnimChangeType.UNDERWATER, AnimChangeState.OFF));

    private static void AssertCanonicalTransform(WorldEntity entity, NitroxVector3 position, NitroxQuaternion rotation)
    {
        Assert.AreEqual(position, entity.Transform.Position);
        Assert.AreEqual(rotation, entity.Transform.Rotation);
    }

    private sealed class MovementScenario
    {
        internal ScannerRoomScenarioFixture Scenario { get; } = new();
        internal RecordingPacketSender Sender { get; } = new();
        internal VehicleMovementsPacketProcessor Processor { get; }

        internal MovementScenario()
        {
            foreach (NitroxId cameraId in Scenario.CameraIds)
            {
                WorldEntity entity = Entity(cameraId);
                entity.Transform.LocalPosition = NitroxVector3.Zero;
                entity.Transform.LocalRotation = NitroxQuaternion.Identity;
                entity.Transform.LocalScale = NitroxVector3.One;
            }
            Processor = new(Scenario.EntityRegistry, Scenario.Ownership, Scenario.ControlLifecycle,
                new DiscardingLogger<VehicleMovementsPacketProcessor>());
        }

        internal WorldEntity Entity(NitroxId id)
        {
            Assert.IsTrue(Scenario.EntityRegistry.TryGetEntityById(id, out WorldEntity entity));
            return entity;
        }
    }

    private sealed class RecordingPacketSender : IPacketSender
    {
        private readonly List<Packet> packets = [];

        internal int OthersPackets { get; private set; }
        internal ManualResetEventSlim? SendEntered { get; set; }
        internal ManualResetEventSlim? ContinueSend { get; set; }

        public ValueTask SendPacketAsync<T>(T packet, SessionId sessionId) where T : Packet => throw new NotSupportedException();
        public ValueTask SendPacketToAllAsync<T>(T packet) where T : Packet => throw new NotSupportedException();

        public ValueTask SendPacketToOthersAsync<T>(T packet, SessionId excludedSessionId) where T : Packet
        {
            SendEntered?.Set();
            ContinueSend?.Wait(TimeSpan.FromSeconds(5));
            packets.Add(packet);
            OthersPackets++;
            return ValueTask.CompletedTask;
        }

        internal T Single<T>() where T : Packet => packets.OfType<T>().Single();
    }

    private sealed class DiscardingLogger<T> : ILogger<T>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
        }
    }
}
