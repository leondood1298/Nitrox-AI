using System.Threading.Tasks;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.Packets;
using Nitrox.Server.Subnautica.Models.GameLogic;
using Nitrox.Server.Subnautica.Models.Packets.Processors;
using Packet = Nitrox.Model.Packets.Packet;
using ServerPlayer = Nitrox.Server.Subnautica.Models.Player;

namespace Nitrox.Test.Server.Subnautica;

/// <summary>
///     A camera out in the world needs a simulating player after its controller lets go: battery and health reports are
///     only accepted from that player, and a plain transient lock cannot be requested for a Scanner Room camera.
/// </summary>
[TestClass]
public sealed class MapRoomCameraReleaseSimulationTest
{
    private static readonly NitroxId cameraId = ScannerRoomScenarioFixture.CameraAId;

    [TestMethod]
    public async Task ReleasingALooseCameraLeavesItsControllerSimulatingIt()
    {
        ScannerRoomScenarioFixture scenario = new();
        Assert.IsTrue((await scenario.ControlLooseAsync(scenario.PlayerA, cameraId, true)).Granted);

        IReadOnlyList<Packet> sent = await scenario.ControlPacketsAsync(scenario.PlayerA, Release(cameraId));

        Assert.IsTrue(sent.OfType<MapRoomCameraControl>().Single().Granted);
        AssertLock(scenario, scenario.PlayerA, SimulationLockType.TRANSIENT);
        SimulatedEntity announced = sent.OfType<SimulationOwnershipChange>().Single().Entities.Single();
        Assert.AreEqual(cameraId, announced.Id);
        Assert.AreEqual(scenario.PlayerA.SessionId, announced.SessionId);
        Assert.AreEqual(SimulationLockType.TRANSIENT, announced.LockType);
        Assert.IsFalse(scenario.HasController(cameraId), "Control itself has ended.");
    }

    [TestMethod]
    public async Task ReleasedLooseCameraStillAcceptsItsSimulatorsBatteryAndHealth()
    {
        ScannerRoomScenarioFixture scenario = new();
        Assert.IsTrue((await scenario.ControlLooseAsync(scenario.PlayerA, cameraId, true)).Granted);
        await scenario.ControlPacketsAsync(scenario.PlayerA, Release(cameraId));
        MapRoomCameraComponentStateProcessor processor = new(scenario.Manager);

        await processor.Process(scenario.Context(scenario.PlayerA), new MapRoomCameraComponentState(cameraId, 60f, 250f));

        Assert.IsTrue(scenario.Sent<MapRoomCameraComponentState>().Single().Granted,
            "Someone has to be able to report a repair or damage to a camera nobody is controlling.");
    }

    [TestMethod]
    public async Task AnotherPlayerCanTakeControlOfAReleasedLooseCamera()
    {
        ScannerRoomScenarioFixture scenario = new();
        Assert.IsTrue((await scenario.ControlLooseAsync(scenario.PlayerA, cameraId, true)).Granted);
        await scenario.ControlPacketsAsync(scenario.PlayerA, Release(cameraId));

        Assert.IsTrue((await scenario.ControlLooseAsync(scenario.PlayerB, cameraId, true)).Granted);

        AssertLock(scenario, scenario.PlayerB, SimulationLockType.EXCLUSIVE);
    }

    [TestMethod]
    public async Task ReleaseFromThePlayerWhoOnlySimulatesALooseCameraKeepsThemSimulatingIt()
    {
        ScannerRoomScenarioFixture scenario = new();
        Assert.IsTrue(scenario.Ownership.TryAcquire(cameraId, scenario.PlayerA, SimulationLockType.TRANSIENT));

        IReadOnlyList<Packet> sent = await scenario.ControlPacketsAsync(scenario.PlayerA, Release(cameraId));

        Assert.IsTrue(sent.OfType<MapRoomCameraControl>().Single().Granted);
        AssertLock(scenario, scenario.PlayerA, SimulationLockType.TRANSIENT);
    }

    [TestMethod]
    public async Task ReleasingADockedCameraLeavesNoLock()
    {
        // A docked camera is a record of its room. Nobody simulates it.
        ScannerRoomScenarioFixture scenario = new();
        Assert.IsTrue((await scenario.DockAsync(scenario.PlayerA, cameraId, 0, true)).Granted);
        Assert.IsTrue((await scenario.ControlAsync(scenario.PlayerA, cameraId, 0, true)).Granted);

        IReadOnlyList<Packet> sent = await scenario.ControlPacketsAsync(scenario.PlayerA, Release(cameraId));

        Assert.IsTrue(sent.OfType<MapRoomCameraControl>().Single().Granted);
        Assert.IsNull(scenario.Ownership.GetPlayerForLock(cameraId));
        Assert.AreEqual(0, sent.OfType<SimulationOwnershipChange>().Count());
    }

    [TestMethod]
    public async Task ControllerKeepsSimulatingALooseCameraWhoseRoomIsDeconstructed()
    {
        ScannerRoomScenarioFixture scenario = new();
        Assert.IsTrue((await scenario.DockAsync(scenario.PlayerA, cameraId, 0, true)).Granted);
        Assert.IsTrue((await scenario.DockAsync(scenario.PlayerA, cameraId, 0, false)).Granted);
        Assert.IsTrue((await scenario.ControlLooseAsync(scenario.PlayerB, cameraId, true)).Granted);
        Assert.IsTrue(scenario.EntityRegistry.RemoveEntity(ScannerRoomScenarioFixture.RoomId).HasValue);

        IReadOnlyList<Packet> sent = await scenario.CleanupAsync(scenario.Room);

        Assert.IsFalse(sent.OfType<MapRoomCameraControl>().Single(packet => packet.CameraId == cameraId).IsControlling,
            "Deconstructing the room still ends control.");
        AssertLock(scenario, scenario.PlayerB, SimulationLockType.TRANSIENT);
        Assert.IsTrue(sent.OfType<SimulationOwnershipChange>().Any(change => change.Entities.Any(entity =>
            entity.Id == cameraId && entity.SessionId == scenario.PlayerB.SessionId && entity.LockType == SimulationLockType.TRANSIENT)));
        Assert.IsFalse(sent.OfType<DropSimulationOwnership>().Any(drop => drop.EntityIds.Contains(cameraId)),
            "Players must not be told to forget a lock that is still held.");
    }

    [TestMethod]
    public async Task DockingAReleasedCameraTellsItsSimulatingPlayerThatTheLockIsGone()
    {
        // A docked camera is simulated by nobody.
        ScannerRoomScenarioFixture scenario = new();
        Assert.IsTrue((await scenario.ControlLooseAsync(scenario.PlayerA, cameraId, true)).Granted);
        await scenario.ControlPacketsAsync(scenario.PlayerA, Release(cameraId));
        AssertLock(scenario, scenario.PlayerA, SimulationLockType.TRANSIENT);
        Assert.IsTrue(scenario.Ownership.TryAcquire(scenario.Room.Id, scenario.PlayerB, SimulationLockType.TRANSIENT));

        IReadOnlyList<Packet> sent = await scenario.DockPacketsAsync(scenario.PlayerB, new MapRoomCameraDock(cameraId, ScannerRoomScenarioFixture.RoomId, 0));

        Assert.IsTrue(sent.OfType<MapRoomCameraDock>().Single().Granted);
        Assert.IsNull(scenario.Ownership.GetPlayerForLock(cameraId));
        Assert.IsTrue(sent.OfType<DropSimulationOwnership>().Single().EntityIds.Contains(cameraId));
        Assert.AreEqual(0, sent.OfType<MapRoomCameraControl>().Count(), "Nobody was controlling it.");
    }

    [TestMethod]
    public async Task DockingACameraNobodySimulatedAnnouncesNoDroppedLock()
    {
        ScannerRoomScenarioFixture scenario = new();
        Assert.IsTrue(scenario.Ownership.TryAcquire(scenario.Room.Id, scenario.PlayerB, SimulationLockType.TRANSIENT));

        IReadOnlyList<Packet> sent = await scenario.DockPacketsAsync(scenario.PlayerB, new MapRoomCameraDock(cameraId, ScannerRoomScenarioFixture.RoomId, 0));

        Assert.IsTrue(sent.OfType<MapRoomCameraDock>().Single().Granted);
        Assert.AreEqual(0, sent.OfType<DropSimulationOwnership>().Count());
    }

    private static MapRoomCameraControl Release(NitroxId camera) => new(camera, Optional.Empty, -1, false, false);

    private static void AssertLock(ScannerRoomScenarioFixture scenario, ServerPlayer expectedPlayer, SimulationLockType expectedType)
    {
        Assert.IsTrue(scenario.Ownership.TryGetLock(cameraId, out SimulationOwnershipData.PlayerLock heldLock));
        Assert.AreSame(expectedPlayer, heldLock.Player);
        Assert.AreEqual(expectedType, heldLock.LockType);
    }
}
