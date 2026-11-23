using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nitrox.Model.Core;
using Nitrox.Model.DataStructures;
using Nitrox.Model.DataStructures.Unity;
using Nitrox.Model.GameLogic.PlayerAnimation;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities;
using Nitrox.Model.Subnautica.MultiplayerSession;
using Nitrox.Model.Subnautica.Packets;
using Nitrox.Server.Subnautica.Models.Packets.Core;
using Nitrox.Test.Server.GameLogic;
using NSubstitute;

namespace Nitrox.Server.Subnautica.Models.Packets.Processors;

[TestClass]
public sealed class VehicleMovementsPacketProcessorTest
{
    [TestMethod]
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
        Assert.IsTrue(test.Scenario.Ownership.TryAcquire(ScannerRoomScenarioFixture.CameraAId, test.Scenario.PlayerA, SimulationLockType.EXCLUSIVE));
        SimpleMovementData movement = CreateMovement(ScannerRoomScenarioFixture.CameraAId, 1f);

        await test.Processor.Process(
            test.Scenario.Context(test.Scenario.PlayerB),
            new VehicleMovements([movement], 10d));

        AssertCanonicalTransform(test.Entity(ScannerRoomScenarioFixture.CameraAId), NitroxVector3.Zero, NitroxQuaternion.Identity);
        Assert.AreEqual(0, test.Forwarded().Count);
    }

    [TestMethod]
    public async Task MixedBatchForwardsOnlyOwnedEntriesInOriginalOrder()
    {
        MovementScenario test = new();
        Assert.IsTrue(test.Scenario.Ownership.TryAcquire(ScannerRoomScenarioFixture.CameraAId, test.Scenario.PlayerA, SimulationLockType.EXCLUSIVE));
        Assert.IsTrue(test.Scenario.Ownership.TryAcquire(ScannerRoomScenarioFixture.CameraBId, test.Scenario.PlayerB, SimulationLockType.EXCLUSIVE));
        Assert.IsTrue(test.Scenario.Ownership.TryAcquire(ScannerRoomScenarioFixture.CameraCId, test.Scenario.PlayerA, SimulationLockType.TRANSIENT));
        SimpleMovementData ownedA = CreateMovement(ScannerRoomScenarioFixture.CameraAId, 1f);
        SimpleMovementData foreignB = CreateMovement(ScannerRoomScenarioFixture.CameraBId, 2f);
        SimpleMovementData ownedC = CreateMovement(ScannerRoomScenarioFixture.CameraCId, 3f);
        VehicleMovements request = new([ownedA, foreignB, ownedC], 42.5d);

        await test.Processor.Process(test.Scenario.Context(test.Scenario.PlayerA), request);

        VehicleMovements forwarded = test.Forwarded().Single();
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
        Assert.IsTrue(test.Scenario.Ownership.TryAcquire(
            ordinaryId, test.Scenario.PlayerA, SimulationLockType.TRANSIENT));
        SimpleMovementData movement = CreateMovement(ordinaryId, 3.5f);

        Task processing;
        bool completedWhileRoomLocked;
        using (test.Scenario.Manager.HoldGateAsync().GetAwaiter().GetResult())
        {
            processing = Task.Run(() => test.Processor.Process(
                test.Scenario.Context(test.Scenario.PlayerA),
                new VehicleMovements([movement], 43d)));
            completedWhileRoomLocked = processing.Wait(TimeSpan.FromMilliseconds(500));
        }
        processing.Wait(TimeSpan.FromSeconds(5));

        Assert.IsTrue(completedWhileRoomLocked,
            "An ordinary non-camera movement batch must not wait for Scanner Room state.");
        VehicleMovements forwarded = test.Forwarded().Single();
        Assert.AreSame(movement, forwarded.Data.Single());
        AssertCanonicalTransform(ordinaryEntity, movement.Position, movement.Rotation);
    }

    [TestMethod]
    public async Task MalformedEntriesCannotMutateOrRebroadcast()
    {
        MovementScenario test = new();
        foreach (NitroxId cameraId in test.Scenario.CameraIds)
        {
            Assert.IsTrue(test.Scenario.Ownership.TryAcquire(cameraId, test.Scenario.PlayerA, SimulationLockType.EXCLUSIVE));
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
            test.Scenario.Context(test.Scenario.PlayerA),
            new VehicleMovements(malformed, 1d));

        foreach (NitroxId cameraId in test.Scenario.CameraIds)
        {
            AssertCanonicalTransform(test.Entity(cameraId), NitroxVector3.Zero, NitroxQuaternion.Identity);
        }
        Assert.AreEqual(0, test.Forwarded().Count);
    }

    [TestMethod]
    public async Task EmptyOversizedAndOutOfRangeTimeBatchesAreRejectedAtomically()
    {
        MovementScenario test = new();
        Assert.IsTrue(test.Scenario.Ownership.TryAcquire(ScannerRoomScenarioFixture.CameraAId, test.Scenario.PlayerA, SimulationLockType.EXCLUSIVE));
        AuthProcessorContext context = test.Scenario.Context(test.Scenario.PlayerA);
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
        Assert.AreEqual(0, test.Forwarded().Count);
    }

    [TestMethod]
    public async Task OwnedExclusiveCameraMovementIsAppliedAndForwarded()
    {
        MovementScenario test = new();
        Assert.IsTrue(test.Scenario.Ownership.TryAcquire(ScannerRoomScenarioFixture.CameraAId, test.Scenario.PlayerA, SimulationLockType.EXCLUSIVE));
        SimpleMovementData movement = CreateMovement(ScannerRoomScenarioFixture.CameraAId, 7f);

        await test.Processor.Process(
            test.Scenario.Context(test.Scenario.PlayerA),
            new VehicleMovements([movement], 1533d));

        VehicleMovements forwarded = test.Forwarded().Single();
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
        Assert.IsTrue(test.Scenario.Ownership.TryAcquire(
            cameraId, test.Scenario.PlayerA, SimulationLockType.TRANSIENT));

        await test.Processor.Process(
            test.Scenario.Context(test.Scenario.PlayerA),
            new VehicleMovements([CreateMovement(cameraId, 7.1f)], 1533.1d));

        Assert.AreEqual(0, test.Forwarded().Count);
        AssertCanonicalTransform(test.Entity(cameraId),
            NitroxVector3.Zero, NitroxQuaternion.Identity);

        Assert.IsTrue(test.Scenario.Ownership.TryAcquire(
            cameraId, test.Scenario.PlayerA, SimulationLockType.EXCLUSIVE));
        await test.Processor.Process(
            test.Scenario.Context(test.Scenario.PlayerA),
            new VehicleMovements([CreateMovement(cameraId, 7.2f)], 1533.2d));

        Assert.AreEqual(0, test.Forwarded().Count,
            "An exclusive lock without canonical control must not move a docked camera.");
        AssertCanonicalTransform(test.Entity(cameraId),
            NitroxVector3.Zero, NitroxQuaternion.Identity);

        Assert.IsTrue((await test.Scenario.ControlAsync(
            test.Scenario.PlayerA, cameraId, true)).Granted);
        SimpleMovementData authorizedMovement = CreateMovement(cameraId, 7.3f);
        await test.Processor.Process(
            test.Scenario.Context(test.Scenario.PlayerA),
            new VehicleMovements([authorizedMovement], 1533.3d));

        Assert.AreEqual(1, test.Forwarded().Count);
        AssertCanonicalTransform(test.Entity(cameraId),
            authorizedMovement.Position, authorizedMovement.Rotation);
    }

    [TestMethod]
    public async Task OwnershipCannotBeRevokedUntilAcceptedMovementIsQueued()
    {
        MovementScenario test = new();
        NitroxId cameraId = ScannerRoomScenarioFixture.CameraAId;
        Assert.IsTrue(test.Scenario.Ownership.TryAcquire(cameraId, test.Scenario.PlayerA, SimulationLockType.EXCLUSIVE));
        using ManualResetEventSlim sendEntered = new();
        using ManualResetEventSlim continueSend = new();
        test.Scenario.PacketSender.When(sender => sender.SendPacketToOthersAsync(Arg.Any<VehicleMovements>(), Arg.Any<SessionId>())).Do(_ =>
        {
            sendEntered.Set();
            continueSend.Wait(TimeSpan.FromSeconds(5));
        });

        Task movement = Task.Run(() => test.Processor.Process(
            test.Scenario.Context(test.Scenario.PlayerA),
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
        Assert.AreEqual(1, test.Forwarded().Count);
    }

    [TestMethod]
    public async Task DrivenMovementFromLockOwnerIsRelayedBeforePilotModeIsRegistered()
    {
        // A client reports driven movement as soon as it is piloting locally, which can be a moment before the
        // server has processed its pilot-mode packet. The vehicle must keep moving for everyone else meanwhile.
        MovementScenario test = new();
        NitroxId cameraId = ScannerRoomScenarioFixture.CameraAId;
        Assert.IsTrue(test.Scenario.Ownership.TryAcquire(cameraId, test.Scenario.PlayerA, SimulationLockType.EXCLUSIVE));
        DrivenVehicleMovementData movement = new(cameraId, new NitroxVector3(8f, 9f, 10f), NitroxQuaternion.Identity, 0, 0, true);
        NitroxVector3 playerPositionBefore = test.Scenario.PlayerA.Position;

        await test.Processor.Process(
            test.Scenario.Context(test.Scenario.PlayerA),
            new VehicleMovements([movement], 1d));

        Assert.AreEqual(1, test.Forwarded().Count);
        AssertCanonicalTransform(test.Entity(cameraId), movement.Position, movement.Rotation);
        Assert.AreEqual(playerPositionBefore, test.Scenario.PlayerA.Position,
            "Only a registered pilot's own position follows the vehicle.");
    }

    [TestMethod]
    public async Task DrivenMovementFromRegisteredPilotAlsoMovesThePlayer()
    {
        MovementScenario test = new();
        NitroxId cameraId = ScannerRoomScenarioFixture.CameraAId;
        Assert.IsTrue(test.Scenario.Ownership.TryAcquire(cameraId, test.Scenario.PlayerA, SimulationLockType.EXCLUSIVE));
        DrivenVehicleMovementData movement = new(cameraId, new NitroxVector3(8f, 9f, 10f), NitroxQuaternion.Identity, 0, 0, true);
        test.Scenario.PlayerA.PlayerContext = CreatePlayerContext(test.Scenario.PlayerA, cameraId);

        await test.Processor.Process(
            test.Scenario.Context(test.Scenario.PlayerA),
            new VehicleMovements([movement], 2d));

        Assert.AreEqual(1, test.Forwarded().Count);
        AssertCanonicalTransform(test.Entity(cameraId), movement.Position, movement.Rotation);
        Assert.AreEqual(movement.Position, test.Scenario.PlayerA.Position);
        Assert.AreEqual(movement.Rotation, test.Scenario.PlayerA.Rotation);
    }

    private static SimpleMovementData CreateMovement(NitroxId id, float offset) => new(
        id,
        new NitroxVector3(offset, offset + 0.25f, offset + 0.5f),
        NitroxQuaternion.Identity);

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
            Processor = new(Scenario.EntityRegistry, Scenario.Ownership, Scenario.Manager, Substitute.For<ILogger<VehicleMovementsPacketProcessor>>());
        }

        internal WorldEntity Entity(NitroxId id)
        {
            Assert.IsTrue(Scenario.EntityRegistry.TryGetEntityById(id, out WorldEntity entity));
            return entity;
        }

        /// <summary>
        ///     The movement the processor passed on, which it does to the other players and to nobody else.
        /// </summary>
        internal List<VehicleMovements> Forwarded()
        {
            List<VehicleMovements> toOthers = Scenario.SentToOthers<VehicleMovements>();
            Assert.AreEqual(toOthers.Count, Scenario.Sent<VehicleMovements>().Count, "Movement is passed on to the other players only.");
            return toOthers;
        }
    }
}
