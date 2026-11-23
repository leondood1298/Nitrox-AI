using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Bases;
using Nitrox.Test.Server.GameLogic;

namespace Nitrox.Server.Subnautica.Models.GameLogic.Entities;

/// <summary>
///     Who gets an entity when its simulating player leaves or cannot simulate it. An entity nobody can simulate stays
///     frozen for everyone: a base power source neither generates nor loses what other players take from it.
/// </summary>
[TestClass]
public sealed class EntitySimulationTest
{
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

    private static ModuleEntity AddSolarPanel(ScannerRoomScenarioFixture scenario)
    {
        ModuleEntity source = ModuleEntity.MakeEmpty();
        source.Id = new NitroxId();
        scenario.EntityRegistry.AddEntity(source);
        return source;
    }
}
