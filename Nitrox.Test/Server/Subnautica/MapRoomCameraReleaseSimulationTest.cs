using System.Threading.Tasks;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;
using Nitrox.Model.Subnautica.Packets;
using Nitrox.Server.Subnautica.Models.GameLogic;
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

        IReadOnlyList<Packet> sent = await scenario.ReportCameraAsync(scenario.PlayerA, cameraId, energy: 60f, health: 250f);

        Assert.AreEqual((60f, 250f), (scenario.CameraState(cameraId).Energy, scenario.CameraState(cameraId).Health),
            "Someone has to be able to report a repair or damage to a camera nobody is controlling.");
        Assert.AreEqual(0, sent.Count, "They are not sent back what they reported.");
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
    public async Task ReleasingADockedCameraLeavesItsControllerSimulatingIt()
    {
        // A docked camera is an entity like a loose one. Its simulating player recharges it.
        ScannerRoomScenarioFixture scenario = new();
        Assert.IsTrue((await scenario.DockAsync(scenario.PlayerA, cameraId, 0, true)).Granted);
        Assert.IsTrue((await scenario.ControlAsync(scenario.PlayerB, cameraId, 0, true)).Granted);

        IReadOnlyList<Packet> sent = await scenario.ControlPacketsAsync(scenario.PlayerB, new MapRoomCameraControl(cameraId, ScannerRoomScenarioFixture.RoomId, 0, false, false));

        Assert.IsTrue(sent.OfType<MapRoomCameraControl>().Single().Granted);
        AssertLock(scenario, scenario.PlayerB, SimulationLockType.TRANSIENT);
        SimulatedEntity announced = sent.OfType<SimulationOwnershipChange>().Single().Entities.Single();
        Assert.AreEqual((cameraId, scenario.PlayerB.SessionId, SimulationLockType.TRANSIENT), (announced.Id, announced.SessionId, announced.LockType));
    }

    [TestMethod]
    public async Task DeconstructingARoomLeavesTheControllerOfACameraThatIsOutInTheWorldAlone()
    {
        // Any screen flies any camera of the world. A camera that left its dock is no part of the room it came from.
        ScannerRoomScenarioFixture scenario = new();
        Assert.IsTrue((await scenario.DockAsync(scenario.PlayerA, cameraId, 0, true)).Granted);
        Assert.IsTrue((await scenario.DockAsync(scenario.PlayerA, cameraId, 0, false)).Granted);
        Assert.IsTrue((await scenario.ControlLooseAsync(scenario.PlayerB, cameraId, true)).Granted);
        Assert.IsTrue(scenario.EntityRegistry.RemoveEntity(ScannerRoomScenarioFixture.RoomId).HasValue);

        IReadOnlyList<Packet> sent = await scenario.CleanupAsync(scenario.Room);

        Assert.AreEqual(0, sent.OfType<MapRoomCameraControl>().Count());
        AssertLock(scenario, scenario.PlayerB, SimulationLockType.EXCLUSIVE);
        Assert.IsTrue(scenario.IsController(cameraId, scenario.PlayerB.SessionId));
        Assert.IsFalse(sent.OfType<DropSimulationOwnership>().Any(drop => drop.EntityIds.Contains(cameraId)),
            "Players must not be told to forget a lock that is still held.");
    }

    [TestMethod]
    public async Task DockedCameraKeepsItsSimulatingPlayer()
    {
        // A camera in a dock is recharged by the player who simulates it, like a docked vehicle is repaired.
        ScannerRoomScenarioFixture scenario = new();
        Assert.IsTrue((await scenario.ControlLooseAsync(scenario.PlayerA, cameraId, true)).Granted);
        await scenario.ControlPacketsAsync(scenario.PlayerA, Release(cameraId));
        AssertLock(scenario, scenario.PlayerA, SimulationLockType.TRANSIENT);

        ScannerRoomScenarioFixture.DockResult docked = await scenario.DockAsync(scenario.PlayerA, cameraId, 0, true);

        Assert.IsTrue(docked.Granted);
        AssertLock(scenario, scenario.PlayerA, SimulationLockType.TRANSIENT);
        Assert.AreEqual(0, docked.Sent.OfType<DropSimulationOwnership>().Count());
        Assert.AreEqual(0, docked.Sent.OfType<MapRoomCameraControl>().Count(), "Nobody was controlling it.");
    }

    [TestMethod]
    public async Task DockReportedByAPlayerWhoDoesNotSimulateTheCameraChangesNothing()
    {
        // Every client whose game sees a camera reach a dock docks it. Only the camera's simulating player counts.
        ScannerRoomScenarioFixture scenario = new();
        Assert.IsTrue(scenario.Ownership.TryAcquire(cameraId, scenario.PlayerA, SimulationLockType.TRANSIENT));
        Assert.IsTrue(scenario.Ownership.TryAcquire(scenario.Room.Id, scenario.PlayerB, SimulationLockType.TRANSIENT));

        ScannerRoomScenarioFixture.DockResult refused = await scenario.DockAsync(scenario.PlayerB, cameraId, 0, true, establishAuthority: false);

        Assert.IsFalse(refused.Granted);
        Assert.IsFalse(scenario.IsDocked(cameraId));
        AssertLock(scenario, scenario.PlayerA, SimulationLockType.TRANSIENT);
        EntityMetadataUpdate correction = (EntityMetadataUpdate)refused.Sent.Single();
        Assert.AreEqual(cameraId, correction.Id);
        Assert.IsFalse(((MapRoomCameraMetadata)correction.NewValue).IsDocked, "With the camera's real state the refused player takes it out of the dock again.");
        Assert.AreEqual(0, scenario.SentTo<EntityMetadataUpdate>(scenario.PlayerA).Count);
    }

    private static MapRoomCameraControl Release(NitroxId camera) => new(camera, Optional.Empty, -1, false, false);

    private static void AssertLock(ScannerRoomScenarioFixture scenario, ServerPlayer expectedPlayer, SimulationLockType expectedType)
    {
        Assert.IsTrue(scenario.Ownership.TryGetLock(cameraId, out SimulationOwnershipData.PlayerLock heldLock));
        Assert.AreSame(expectedPlayer, heldLock.Player);
        Assert.AreEqual(expectedType, heldLock.LockType);
    }
}
