using Nitrox.Model.DataStructures;
using Nitrox.Model.DataStructures.Unity;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Bases;
using Nitrox.Test.Server.GameLogic;

namespace Nitrox.Server.Subnautica.Models.GameLogic.Entities;

/// <summary>
///     Who gets an entity when its simulating player leaves or cannot simulate it. An entity nobody can simulate stays
///     frozen for everyone: a base power source neither generates nor loses what other players take from it.
///     <para>
///         An exclusive lock means a player is piloting or controlling the entity right now. The server's bookkeeping
///         (cells coming into view, reassignment, forced assignment, movement hand-off) must not weaken or remove it;
///         only the player's own explicit request or the control lifecycle may.
///     </para>
/// </summary>
[TestClass]
public sealed class EntitySimulationTest
{
    private static readonly NitroxId vehicleId = new("00000000-0000-0000-0000-000000000900");

    [TestMethod]
    public void PlayersInTheWorldComeBeforePlayersStillLoading()
    {
        ScannerRoomScenarioFixture scenario = new();
        Player loading = scenario.PlayerA;
        Player inTheWorld = scenario.PlayerB;
        inTheWorld.HasFinishedInitialSync = true;

        CollectionAssert.AreEqual(new[] { inTheWorld, loading }, EntitySimulation.PreferPlayersInTheWorld([loading, inTheWorld]));

        loading.HasFinishedInitialSync = true;
        CollectionAssert.AreEqual(new[] { loading, inTheWorld }, EntitySimulation.PreferPlayersInTheWorld([loading, inTheWorld]), "Order among equals is kept.");
    }

    [TestMethod]
    public void EntityIsNotGivenToALoadingPlayerWhileSomeoneIsInTheWorld()
    {
        ScannerRoomScenarioFixture scenario = new();
        ModuleEntity source = AddSolarPanel(scenario);
        scenario.PlayerA.HasFinishedInitialSync = false;
        scenario.PlayerB.HasFinishedInitialSync = true;

        List<Player> candidates = EntitySimulation.PreferPlayersInTheWorld([scenario.PlayerA, scenario.PlayerB]);
        Assert.IsTrue(scenario.EntitySimulation.TryAssignEntityToPlayers(candidates, source, false, out List<SimulatedEntity> assigned));

        Assert.AreEqual(scenario.PlayerB.SessionId, assigned.Single().SessionId);
        Assert.AreEqual(scenario.PlayerB, scenario.Ownership.GetPlayerForLock(source.Id));
    }

    [TestMethod]
    public void LoadingPlayerStillGetsTheEntityWhenNobodyElseIsThere()
    {
        ScannerRoomScenarioFixture scenario = new();
        ModuleEntity source = AddSolarPanel(scenario);
        scenario.PlayerA.HasFinishedInitialSync = false;

        Assert.IsTrue(scenario.EntitySimulation.TryAssignEntityToPlayers(EntitySimulation.PreferPlayersInTheWorld([scenario.PlayerA]), source, false, out _));

        Assert.AreEqual(scenario.PlayerA, scenario.Ownership.GetPlayerForLock(source.Id));
    }

    [TestMethod]
    public void PlayerWhoCannotSimulateAnEntityHandsItToAnotherPlayer()
    {
        ScannerRoomScenarioFixture scenario = new();
        ModuleEntity source = AddSolarPanel(scenario);
        scenario.PlayerB.HasFinishedInitialSync = true;
        Assert.IsTrue(scenario.Ownership.TryAcquire(source.Id, scenario.PlayerA, SimulationLockType.TRANSIENT));

        Assert.IsTrue(scenario.EntitySimulation.TryReleaseBackgroundSimulation(scenario.PlayerA, source, [scenario.PlayerB], out List<SimulatedEntity> takenOver));

        Assert.IsNotNull(takenOver);
        Assert.AreEqual(scenario.PlayerB.SessionId, takenOver.Single().SessionId);
        Assert.AreEqual(scenario.PlayerB, scenario.Ownership.GetPlayerForLock(source.Id));
    }

    [TestMethod]
    public void ReleasedEntityWithNobodyElseIsLeftWithoutASimulatingPlayer()
    {
        ScannerRoomScenarioFixture scenario = new();
        ModuleEntity source = AddSolarPanel(scenario);
        Assert.IsTrue(scenario.Ownership.TryAcquire(source.Id, scenario.PlayerA, SimulationLockType.TRANSIENT));

        Assert.IsTrue(scenario.EntitySimulation.TryReleaseBackgroundSimulation(scenario.PlayerA, source, [], out List<SimulatedEntity> takenOver));

        Assert.IsNull(takenOver);
        Assert.IsNull(scenario.Ownership.GetPlayerForLock(source.Id));
    }

    [TestMethod]
    public void OnlyTheSimulatingPlayerCanHandAnEntityBack()
    {
        ScannerRoomScenarioFixture scenario = new();
        ModuleEntity source = AddSolarPanel(scenario);
        Assert.IsTrue(scenario.Ownership.TryAcquire(source.Id, scenario.PlayerA, SimulationLockType.TRANSIENT));

        Assert.IsFalse(scenario.EntitySimulation.TryReleaseBackgroundSimulation(scenario.PlayerB, source, [scenario.PlayerA], out List<SimulatedEntity> takenOver));

        Assert.IsNull(takenOver);
        Assert.AreEqual(scenario.PlayerA, scenario.Ownership.GetPlayerForLock(source.Id));
    }

    [TestMethod]
    public void ExclusiveLockIsNotReleasedThisWay()
    {
        // Piloting or controlling something ends through its own lifecycle, not through a background release.
        ScannerRoomScenarioFixture scenario = new();
        ModuleEntity source = AddSolarPanel(scenario);
        Assert.IsTrue(scenario.Ownership.TryAcquire(source.Id, scenario.PlayerA, SimulationLockType.EXCLUSIVE));

        Assert.IsFalse(scenario.EntitySimulation.TryReleaseBackgroundSimulation(scenario.PlayerA, source, [scenario.PlayerB], out _));

        Assert.IsTrue(scenario.Ownership.TryGetLock(source.Id, out SimulationOwnershipData.PlayerLock heldLock));
        Assert.AreEqual(scenario.PlayerA, heldLock.Player);
        Assert.AreEqual(SimulationLockType.EXCLUSIVE, heldLock.LockType);
    }

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

    private static ModuleEntity AddSolarPanel(ScannerRoomScenarioFixture scenario)
    {
        ModuleEntity source = ModuleEntity.MakeEmpty();
        source.Id = new NitroxId();
        scenario.EntityRegistry.AddEntity(source);
        return source;
    }

    private static WorldEntity AddVehicle(ScannerRoomScenarioFixture scenario)
    {
        WorldEntity vehicle = new(new NitroxVector3(10f, -20f, 0f), NitroxQuaternion.Identity, NitroxVector3.One,
            new NitroxTechType("Seamoth"), 0, "seamoth-class-id", false, vehicleId, null);
        scenario.EntityRegistry.AddEntity(vehicle);
        return vehicle;
    }

    private static void AssertLock(ScannerRoomScenarioFixture scenario, Player expectedPlayer, SimulationLockType expectedType)
    {
        Assert.IsTrue(scenario.Ownership.TryGetLock(vehicleId, out SimulationOwnershipData.PlayerLock heldLock));
        Assert.AreEqual(expectedPlayer.SessionId, heldLock.Player.SessionId);
        Assert.AreEqual(expectedType, heldLock.LockType);
    }
}
