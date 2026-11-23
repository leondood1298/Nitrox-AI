using System.Threading.Tasks;
using Nitrox.Model.DataStructures;
using Nitrox.Model.DataStructures.Unity;
using Nitrox.Model.Subnautica.DataStructures;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities;
using Nitrox.Model.Subnautica.Packets;
using Nitrox.Server.Subnautica.Models.GameLogic;

namespace Nitrox.Test.Server.Subnautica;

/// <summary>
///     An exclusive lock means a player is piloting or controlling the entity right now. The server's bookkeeping paths
///     (cells coming into view, reassignment, forced assignment, movement hand-off) must not weaken or remove it; only
///     the player's own explicit request or the control lifecycle may.
/// </summary>
[TestClass]
public sealed class EntitySimulationLockProtectionTest
{
    private static readonly NitroxId vehicleId = new("00000000-0000-0000-0000-000000000900");

    [TestMethod]
    public void RefreshKeepsThePlayersOwnExclusiveLock()
    {
        ScannerRoomScenarioFixture scenario = new();
        WorldEntity vehicle = AddVehicle(scenario);
        Assert.IsTrue(scenario.Ownership.TryAcquire(vehicleId, scenario.PlayerA, SimulationLockType.EXCLUSIVE));

        SimulatedEntity refreshed = scenario.EntitySimulation.TryAcquireEntity(scenario.PlayerA, vehicle, propagate: true).Single();

        Assert.AreEqual(SimulationLockType.EXCLUSIVE, refreshed.LockType, "Players are told the lock that is actually held.");
        AssertLock(scenario, scenario.PlayerA, SimulationLockType.EXCLUSIVE);
    }

    [TestMethod]
    public void RefreshForAnotherPlayerDoesNotTakeAnExclusiveLock()
    {
        ScannerRoomScenarioFixture scenario = new();
        WorldEntity vehicle = AddVehicle(scenario);
        Assert.IsTrue(scenario.Ownership.TryAcquire(vehicleId, scenario.PlayerA, SimulationLockType.EXCLUSIVE));

        Assert.AreEqual(0, scenario.EntitySimulation.TryAcquireEntity(scenario.PlayerB, vehicle, propagate: true).Count());

        AssertLock(scenario, scenario.PlayerA, SimulationLockType.EXCLUSIVE);
    }

    [TestMethod]
    public void ExplicitRequestByTheHolderIsAppliedExactly()
    {
        // Leaving a vehicle asks for a transient lock on it; that request has to take effect.
        ScannerRoomScenarioFixture scenario = new();
        WorldEntity vehicle = AddVehicle(scenario);
        Assert.IsTrue(scenario.Ownership.TryAcquire(vehicleId, scenario.PlayerA, SimulationLockType.EXCLUSIVE));

        SimulatedEntity changed = scenario.EntitySimulation.TryAcquireEntity(scenario.PlayerA, vehicle, propagate: true,
            SimulationLockType.TRANSIENT, explicitRequest: true).Single();

        Assert.AreEqual(SimulationLockType.TRANSIENT, changed.LockType);
        AssertLock(scenario, scenario.PlayerA, SimulationLockType.TRANSIENT);
    }

    [TestMethod]
    public void ForcedAssignmentDoesNotTakeAnExclusiveLockFromAnotherPlayer()
    {
        ScannerRoomScenarioFixture scenario = new();
        WorldEntity vehicle = AddVehicle(scenario);
        Assert.IsTrue(scenario.Ownership.TryAcquire(vehicleId, scenario.PlayerA, SimulationLockType.EXCLUSIVE));

        Assert.AreEqual(0, scenario.EntitySimulation.AssignEntityToPlayer(vehicle, scenario.PlayerB, propagate: true).Count());

        AssertLock(scenario, scenario.PlayerA, SimulationLockType.EXCLUSIVE);
    }

    [TestMethod]
    public void ForcedAssignmentTakesOverATransientLock()
    {
        // A player dropping an item becomes its simulating player even if someone else simulated it in the background.
        ScannerRoomScenarioFixture scenario = new();
        WorldEntity vehicle = AddVehicle(scenario);
        Assert.IsTrue(scenario.Ownership.TryAcquire(vehicleId, scenario.PlayerA, SimulationLockType.TRANSIENT));

        SimulatedEntity assigned = scenario.EntitySimulation.AssignEntityToPlayer(vehicle, scenario.PlayerB, propagate: true).Single();

        Assert.AreEqual(scenario.PlayerB.SessionId, assigned.SessionId);
        AssertLock(scenario, scenario.PlayerB, SimulationLockType.TRANSIENT);
    }

    [TestMethod]
    public void ForcedAssignmentToTheHolderKeepsTheirExclusiveLock()
    {
        ScannerRoomScenarioFixture scenario = new();
        WorldEntity vehicle = AddVehicle(scenario);
        Assert.IsTrue(scenario.Ownership.TryAcquire(vehicleId, scenario.PlayerA, SimulationLockType.EXCLUSIVE));

        SimulatedEntity assigned = scenario.EntitySimulation.AssignEntityToPlayer(vehicle, scenario.PlayerA, propagate: true).Single();

        Assert.AreEqual(SimulationLockType.EXCLUSIVE, assigned.LockType);
        AssertLock(scenario, scenario.PlayerA, SimulationLockType.EXCLUSIVE);
    }

    [TestMethod]
    public async Task ControlledScannerCameraIsRecognisedOnlyForItsController()
    {
        ScannerRoomScenarioFixture scenario = new();
        Assert.IsTrue((await scenario.DockAsync(scenario.PlayerA, ScannerRoomScenarioFixture.CameraAId, 0, true)).Granted);
        Assert.IsTrue((await scenario.ControlAsync(scenario.PlayerA, ScannerRoomScenarioFixture.CameraAId, 0, true)).Granted);

        Assert.IsTrue(scenario.EntitySimulation.IsControlledScannerCamera(ScannerRoomScenarioFixture.CameraAId, scenario.PlayerA));
        Assert.IsFalse(scenario.EntitySimulation.IsControlledScannerCamera(ScannerRoomScenarioFixture.CameraAId, scenario.PlayerB));
        Assert.IsFalse(scenario.EntitySimulation.IsControlledScannerCamera(vehicleId, scenario.PlayerA), "Only Scanner Room cameras are covered.");
    }

    private static WorldEntity AddVehicle(ScannerRoomScenarioFixture scenario)
    {
        WorldEntity vehicle = new(new NitroxVector3(10f, -20f, 0f), NitroxQuaternion.Identity, NitroxVector3.One,
            new NitroxTechType("Seamoth"), 0, "seamoth-class-id", false, vehicleId, null);
        scenario.EntityRegistry.AddEntity(vehicle);
        return vehicle;
    }

    private static void AssertLock(ScannerRoomScenarioFixture scenario, Nitrox.Server.Subnautica.Models.Player expectedPlayer, SimulationLockType expectedType)
    {
        Assert.IsTrue(scenario.Ownership.TryGetLock(vehicleId, out SimulationOwnershipData.PlayerLock heldLock));
        Assert.AreEqual(expectedPlayer.SessionId, heldLock.Player.SessionId);
        Assert.AreEqual(expectedType, heldLock.LockType);
    }
}
