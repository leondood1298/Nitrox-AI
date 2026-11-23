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
///     A client takes a generic exclusive lock on a loose camera while its Stalker carries it. That lock is given to any
///     client that asks and only given back when that client says so, so it must never keep players away from the
///     camera for good.
/// </summary>
[TestClass]
public sealed class MapRoomCameraStalkerLockTest
{
    private static readonly NitroxId cameraId = ScannerRoomScenarioFixture.CameraAId;

    [TestMethod]
    public async Task CameraControlTakesOverALockThatIsNotCameraControl()
    {
        // The Stalker unloaded mid-carry, or its simulating player never gave the lock back.
        ScannerRoomScenarioFixture scenario = new();
        Assert.IsTrue((await scenario.RequestOwnershipAsync(scenario.PlayerB, cameraId, SimulationLockType.EXCLUSIVE)).LockAcquired);

        IReadOnlyList<Packet> sent = await scenario.ControlPacketsAsync(scenario.PlayerA, Acquire());

        MapRoomCameraControl response = sent.OfType<MapRoomCameraControl>().Single();
        Assert.IsTrue(response.Granted, "A camera a Stalker carries can be controlled, as in the game.");
        Assert.AreEqual(scenario.PlayerA.SessionId, response.ControllerSessionId);
        AssertLock(scenario, scenario.PlayerA, SimulationLockType.EXCLUSIVE);
        Assert.IsTrue(scenario.ControlLifecycle.IsActiveController(cameraId, scenario.PlayerA.SessionId));
        SimulatedEntity announced = sent.OfType<SimulationOwnershipChange>().Single().Entities.Single();
        Assert.AreEqual(cameraId, announced.Id);
        Assert.AreEqual(scenario.PlayerA.SessionId, announced.SessionId, "The previous holder has to learn that its lock is gone.");
    }

    [TestMethod]
    public async Task CameraControlDoesNotTakeOverAnotherPlayersCameraControl()
    {
        ScannerRoomScenarioFixture scenario = new();
        Assert.IsTrue((await scenario.ControlLooseAsync(scenario.PlayerB, cameraId, true)).Granted);

        MapRoomCameraControl response = await scenario.ControlLooseAsync(scenario.PlayerA, cameraId, true);

        Assert.IsFalse(response.Granted);
        Assert.AreEqual(scenario.PlayerB.SessionId, response.ControllerSessionId);
        AssertLock(scenario, scenario.PlayerB, SimulationLockType.EXCLUSIVE);
    }

    [TestMethod]
    public async Task PlayerWhoOnlySimulatedTheCameraIsToldWhenAnotherPlayerTakesControl()
    {
        ScannerRoomScenarioFixture scenario = new();
        Assert.IsTrue(scenario.Ownership.TryAcquire(cameraId, scenario.PlayerB, SimulationLockType.TRANSIENT));

        IReadOnlyList<Packet> sent = await scenario.ControlPacketsAsync(scenario.PlayerA, Acquire());

        Assert.IsTrue(sent.OfType<MapRoomCameraControl>().Single().Granted);
        Assert.AreEqual(scenario.PlayerA.SessionId, sent.OfType<SimulationOwnershipChange>().Single().Entities.Single().SessionId);
    }

    [TestMethod]
    public async Task TakingControlOfACameraNobodyHeldAnnouncesNoOrdinaryOwnershipChange()
    {
        ScannerRoomScenarioFixture scenario = new();

        IReadOnlyList<Packet> sent = await scenario.ControlPacketsAsync(scenario.PlayerA, Acquire());

        Assert.IsTrue(sent.OfType<MapRoomCameraControl>().Single().Granted);
        Assert.AreEqual(0, sent.OfType<SimulationOwnershipChange>().Count());
    }

    [TestMethod]
    public async Task DisconnectGivesTheLockBack()
    {
        ScannerRoomScenarioFixture scenario = new();
        Assert.IsTrue((await scenario.RequestOwnershipAsync(scenario.PlayerB, cameraId, SimulationLockType.EXCLUSIVE)).LockAcquired);

        await scenario.DisconnectAsync(scenario.PlayerB);

        Assert.IsNull(scenario.Ownership.GetPlayerForLock(cameraId));
        Assert.IsTrue((await scenario.RequestOwnershipAsync(scenario.PlayerA, cameraId, SimulationLockType.EXCLUSIVE)).LockAcquired,
            "Another player's Stalker can pick the camera up afterwards.");
    }

    [TestMethod]
    public async Task HolderGivesTheLockBackByAskingForAnOrdinaryOne()
    {
        ScannerRoomScenarioFixture scenario = new();
        Assert.IsTrue((await scenario.RequestOwnershipAsync(scenario.PlayerB, cameraId, SimulationLockType.EXCLUSIVE)).LockAcquired);

        Assert.IsTrue((await scenario.RequestOwnershipAsync(scenario.PlayerB, cameraId, SimulationLockType.TRANSIENT)).LockAcquired);

        AssertLock(scenario, scenario.PlayerB, SimulationLockType.TRANSIENT);
    }

    [TestMethod]
    [DataRow(SimulationLockType.EXCLUSIVE, false, true)]
    [DataRow(SimulationLockType.EXCLUSIVE, true, false)]
    [DataRow(SimulationLockType.TRANSIENT, false, false)]
    public void OnlyAnExclusiveLockWithoutCameraControlIsTakenOver(SimulationLockType heldLockType, bool holderIsActiveController, bool expected)
    {
        // A transient lock is taken over by the ordinary acquire rules, not by this one.
        Assert.AreEqual(expected, MapRoomCameraControlProcessor.CanPreempt(heldLockType, holderIsActiveController));
    }

    private static MapRoomCameraControl Acquire() => new(cameraId, Optional.Empty, -1, true, false);

    private static void AssertLock(ScannerRoomScenarioFixture scenario, ServerPlayer expectedPlayer, SimulationLockType expectedType)
    {
        Assert.IsTrue(scenario.Ownership.TryGetLock(cameraId, out SimulationOwnershipData.PlayerLock heldLock));
        Assert.AreSame(expectedPlayer, heldLock.Player);
        Assert.AreEqual(expectedType, heldLock.LockType);
    }
}
