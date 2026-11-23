using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Nitrox.Model.Core;
using Nitrox.Model.DataStructures;
using Nitrox.Model.DataStructures.GameLogic;
using Nitrox.Model.DataStructures.Unity;
using Nitrox.Model.Packets;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Bases;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;
using Nitrox.Model.Subnautica.Packets;
using Nitrox.Server.Subnautica.Models.GameLogic;
using Nitrox.Server.Subnautica.Models.GameLogic.Entities;
using Nitrox.Server.Subnautica.Models.Packets.Core;
using Nitrox.Server.Subnautica.Models.Packets.Processors;
using ServerPlayer = Nitrox.Server.Subnautica.Models.Player;

namespace Nitrox.Test.Server.Subnautica;

/// <summary>
///     A base power source only generates, and only loses what other players take from it, while a player who has the
///     source in their world simulates it. These cover who gets it when that player leaves or cannot simulate it.
/// </summary>
[TestClass]
public sealed class BasePowerSourceHandoffTest
{
    [TestMethod]
    public void PlayersInTheWorldComeBeforePlayersStillLoading()
    {
        ScannerRoomScenarioFixture scenario = new();
        ServerPlayer playerC = CreatePlayer(3, "C");
        scenario.PlayerB.HasFinishedInitialSync = true;
        playerC.HasFinishedInitialSync = true;

        List<ServerPlayer> ordered = EntitySimulation.PreferPlayersInTheWorld([scenario.PlayerA, scenario.PlayerB, playerC]);

        CollectionAssert.AreEqual(new[] { scenario.PlayerB, playerC, scenario.PlayerA }, ordered, "Order among equals is kept.");
    }

    [TestMethod]
    public void SourceIsNotGivenToALoadingPlayerWhileSomeoneIsInTheWorld()
    {
        ScannerRoomScenarioFixture scenario = new();
        ModuleEntity source = AddSolarPanel(scenario);
        scenario.PlayerA.HasFinishedInitialSync = false;
        scenario.PlayerB.HasFinishedInitialSync = true;

        List<ServerPlayer> candidates = EntitySimulation.PreferPlayersInTheWorld([scenario.PlayerA, scenario.PlayerB]);
        Assert.IsTrue(scenario.EntitySimulation.TryAssignEntityToPlayers(candidates, source, false, out List<SimulatedEntity>? assigned));

        Assert.AreEqual(scenario.PlayerB.SessionId, assigned.Single().SessionId);
        Assert.AreEqual(scenario.PlayerB, scenario.Ownership.GetPlayerForLock(source.Id));
    }

    [TestMethod]
    public void LoadingPlayerStillGetsTheSourceWhenNobodyElseIsThere()
    {
        ScannerRoomScenarioFixture scenario = new();
        ModuleEntity source = AddSolarPanel(scenario);
        scenario.PlayerA.HasFinishedInitialSync = false;

        Assert.IsTrue(scenario.EntitySimulation.TryAssignEntityToPlayers(EntitySimulation.PreferPlayersInTheWorld([scenario.PlayerA]), source, false, out _));

        Assert.AreEqual(scenario.PlayerA, scenario.Ownership.GetPlayerForLock(source.Id));
    }

    [TestMethod]
    public async Task ConsumptionFollowsTheSourceToItsNewOwnerAfterTheOwnerLeft()
    {
        ScannerRoomScenarioFixture scenario = new();
        ModuleEntity source = AddSolarPanel(scenario);
        source.Metadata = new PowerSourceMetadata(60f, 75f, BasePowerSourceType.SOLAR, 5);
        ServerPlayer playerC = CreatePlayer(3, "C");
        scenario.PlayerB.HasFinishedInitialSync = true;
        playerC.HasFinishedInitialSync = true;
        Assert.IsTrue(scenario.Ownership.TryAcquire(source.Id, scenario.PlayerA, SimulationLockType.TRANSIENT));

        // Player A disconnects: its locks are revoked and offered to the players who remain.
        scenario.Ownership.RevokeAllLocksForOwner(scenario.PlayerA.SessionId);
        Assert.IsTrue(scenario.EntitySimulation.TryAssignEntityToPlayers(EntitySimulation.PreferPlayersInTheWorld([scenario.PlayerB, playerC]), source, false, out _));

        RecordingSender sender = new();
        await CreateConsumeProcessor(scenario).Process(new AuthProcessorContext(playerC, sender), new BasePowerSourceConsume(source.Id, 2.5f));

        (Packet forwarded, SessionId recipient) = sender.Direct.Single();
        Assert.AreEqual(scenario.PlayerB.SessionId, recipient);
        Assert.AreEqual(2.5f, ((BasePowerSourceConsume)forwarded).Amount);
        Assert.AreEqual(60f, ((PowerSourceMetadata)source.Metadata).Power, "The new owner applies it; the stored state follows its next update.");
    }

    [TestMethod]
    public void PlayerWhoCannotSimulateASourceHandsItToAnotherPlayer()
    {
        ScannerRoomScenarioFixture scenario = new();
        ModuleEntity source = AddSolarPanel(scenario);
        scenario.PlayerB.HasFinishedInitialSync = true;
        Assert.IsTrue(scenario.Ownership.TryAcquire(source.Id, scenario.PlayerA, SimulationLockType.TRANSIENT));

        Assert.IsTrue(scenario.EntitySimulation.TryReleaseBackgroundSimulation(scenario.PlayerA, source, [scenario.PlayerB], out List<SimulatedEntity>? takenOver));

        Assert.IsNotNull(takenOver);
        Assert.AreEqual(scenario.PlayerB.SessionId, takenOver.Single().SessionId);
        Assert.AreEqual(scenario.PlayerB, scenario.Ownership.GetPlayerForLock(source.Id));
    }

    [TestMethod]
    public async Task HandedBackSourceWithNobodyElseIsDroppedAndTheServerAppliesConsumption()
    {
        ScannerRoomScenarioFixture scenario = new();
        ModuleEntity source = AddSolarPanel(scenario);
        source.Metadata = new PowerSourceMetadata(60f, 75f, BasePowerSourceType.SOLAR, 5);
        Assert.IsTrue(scenario.Ownership.TryAcquire(source.Id, scenario.PlayerA, SimulationLockType.TRANSIENT));
        RecordingSender sender = new();
        SimulationOwnershipReleaseProcessor releaseProcessor = new(new PlayerManager(null!, null!, null!, NullLogger<PlayerManager>.Instance),
            scenario.EntitySimulation, scenario.EntityRegistry);

        await releaseProcessor.Process(new AuthProcessorContext(scenario.PlayerA, sender), new SimulationOwnershipRelease(source.Id));

        DropSimulationOwnership drop = (DropSimulationOwnership)sender.ToAll.Single();
        CollectionAssert.AreEqual(new[] { source.Id }, drop.EntityIds);
        Assert.IsNull(scenario.Ownership.GetPlayerForLock(source.Id));

        // The draw the player could not apply comes back right after the release.
        await CreateConsumeProcessor(scenario).Process(new AuthProcessorContext(scenario.PlayerA, sender), new BasePowerSourceConsume(source.Id, 2.5f));

        Assert.AreEqual(57.5f, ((PowerSourceMetadata)source.Metadata).Power);
        Assert.AreEqual(57.5f, sender.ToAll.OfType<BasePowerSourceUpdate>().Single().Power);
    }

    [TestMethod]
    public void OnlyTheSimulatingPlayerCanHandASourceBack()
    {
        ScannerRoomScenarioFixture scenario = new();
        ModuleEntity source = AddSolarPanel(scenario);
        Assert.IsTrue(scenario.Ownership.TryAcquire(source.Id, scenario.PlayerA, SimulationLockType.TRANSIENT));

        Assert.IsFalse(scenario.EntitySimulation.TryReleaseBackgroundSimulation(scenario.PlayerB, source, [scenario.PlayerA], out List<SimulatedEntity>? takenOver));

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

    private static BasePowerSourceConsumeProcessor CreateConsumeProcessor(ScannerRoomScenarioFixture scenario) => new(scenario.EntityRegistry,
        scenario.Ownership, new BasePowerSourceAuthority(), new BasePowerDiagnostics(), NullLogger<BasePowerSourceConsumeProcessor>.Instance);

    private static ModuleEntity AddSolarPanel(ScannerRoomScenarioFixture scenario)
    {
        ModuleEntity source = ModuleEntity.MakeEmpty();
        source.Id = new NitroxId();
        scenario.EntityRegistry.AddEntity(source);
        return source;
    }

    private static ServerPlayer CreatePlayer(ushort sessionId, string name) => new(
        (PeerId)(uint)sessionId,
        (SessionId)sessionId,
        name,
        false,
        null,
        NitroxVector3.Zero,
        NitroxQuaternion.Identity,
        new NitroxId($"00000000-0000-0000-0000-{sessionId:D12}"),
        Optional.Empty,
        Perms.PLAYER,
        new PlayerStatsData(45f, 45f, 100f, 100f, 100f, 0f),
        SubnauticaGameMode.SURVIVAL,
        Array.Empty<NitroxTechType>(),
        Array.Empty<Optional<NitroxId>>(),
        new Dictionary<string, NitroxId>(),
        new Dictionary<string, float>(),
        new Dictionary<string, PingInstancePreference>(),
        new List<int>(),
        false,
        false);

    private sealed class RecordingSender : IPacketSender
    {
        internal List<(Packet Packet, SessionId Recipient)> Direct { get; } = [];
        internal List<Packet> ToAll { get; } = [];

        public ValueTask SendPacketAsync<T>(T packet, SessionId sessionId) where T : Packet
        {
            Direct.Add((packet, sessionId));
            return ValueTask.CompletedTask;
        }

        public ValueTask SendPacketToAllAsync<T>(T packet) where T : Packet
        {
            ToAll.Add(packet);
            return ValueTask.CompletedTask;
        }

        public ValueTask SendPacketToOthersAsync<T>(T packet, SessionId excludedSessionId) where T : Packet
        {
            ToAll.Add(packet);
            return ValueTask.CompletedTask;
        }
    }
}
