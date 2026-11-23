using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Bases;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;
using Nitrox.Model.Subnautica.Packets;
using Nitrox.Server.Subnautica.Models.GameLogic;
using Nitrox.Server.Subnautica.Models.GameLogic.Entities;
using Nitrox.Server.Subnautica.Models.Packets.Core;
using Nitrox.Test.Server.Helper;
using NSubstitute;

namespace Nitrox.Server.Subnautica.Models.Packets.Processors;

/// <summary>
///     The gate every entity metadata update passes on the server. Covered here: the arms for base power, whose state
///     only the player simulating the source decides.
/// </summary>
[TestClass]
public sealed class EntityMetadataUpdateProcessorTest
{
    private ConnectedPlayers players;
    private EntityRegistry entityRegistry;
    private SimulationOwnershipData simulationOwnershipData;
    private EntityMetadataUpdateProcessor processor;
    private IPacketSender packetSender;
    private Player simulator;
    private Player otherPlayer;
    private Player thirdPlayer;

    [TestInitialize]
    public void Init()
    {
        players = new ConnectedPlayers();
        entityRegistry = new EntityRegistry(NullLogger<EntityRegistry>.Instance);
        simulationOwnershipData = new SimulationOwnershipData();
        BasePowerManager basePowerManager = new(entityRegistry, simulationOwnershipData, new BasePowerDiagnostics(), new DiscardingLogger<BasePowerManager>());
        processor = new EntityMetadataUpdateProcessor(players.PlayerManager, entityRegistry, simulationOwnershipData,
            new ScannerRoomDiagnostics(NullLogger<ScannerRoomDiagnostics>.Instance), basePowerManager, new DiscardingLogger<EntityMetadataUpdateProcessor>());
        packetSender = Substitute.For<IPacketSender>();
        simulator = players.Connect("Simulator");
        otherPlayer = players.Connect("Other");
        thirdPlayer = players.Connect("Third");
    }

    [TestMethod]
    public async Task StateFromTheSimulatingPlayerIsStoredAndSentToTheOthers()
    {
        ModuleEntity panel = AddSource(new PowerSourceMetadata(30f, 0f, BasePowerSourceType.SOLAR), simulator);

        await Send(simulator, panel, new PowerSourceMetadata(40f, 0f, BasePowerSourceType.SOLAR));

        Assert.AreEqual(40f, ((PowerSourceMetadata)panel.Metadata).Power);
        foreach (Player receiver in new[] { otherPlayer, thirdPlayer })
        {
            EntityMetadataUpdate relayed = ConnectedPlayers.SentTo<EntityMetadataUpdate>(packetSender, receiver).Single();
            Assert.AreEqual(panel.Id, relayed.Id);
            Assert.AreEqual(40f, ((PowerSourceMetadata)relayed.NewValue).Power);
        }
        Assert.AreEqual(0, ConnectedPlayers.SentTo<EntityMetadataUpdate>(packetSender, simulator).Count,
            "The simulating player already holds this state; getting it back would undo what it simulated meanwhile.");
    }

    [TestMethod]
    public async Task StateFromAPlayerWhoDoesNotSimulateTheSourceIsAnsweredWithTheStoredState()
    {
        ModuleEntity panel = AddSource(new PowerSourceMetadata(30f, 0f, BasePowerSourceType.SOLAR), simulator);

        await Send(otherPlayer, panel, new PowerSourceMetadata(70f, 0f, BasePowerSourceType.SOLAR));

        Assert.AreEqual(30f, ((PowerSourceMetadata)panel.Metadata).Power);
        EntityMetadataUpdate correction = ConnectedPlayers.SentTo<EntityMetadataUpdate>(packetSender, otherPlayer).Single();
        Assert.AreEqual(30f, ((PowerSourceMetadata)correction.NewValue).Power);
        Assert.AreEqual(0, ConnectedPlayers.SentTo<EntityMetadataUpdate>(packetSender, simulator).Count);
        Assert.AreEqual(0, ConnectedPlayers.SentTo<EntityMetadataUpdate>(packetSender, thirdPlayer).Count);
    }

    [TestMethod]
    public async Task LateStateFromThePreviousSimulatingPlayerCannotReplaceTheNewOnes()
    {
        ModuleEntity panel = AddSource(new PowerSourceMetadata(30f, 0f, BasePowerSourceType.SOLAR), simulator);
        simulationOwnershipData.RevokeIfOwner(panel.Id, simulator);
        Assert.IsTrue(simulationOwnershipData.TryAcquire(panel.Id, otherPlayer, SimulationLockType.TRANSIENT));
        await Send(otherPlayer, panel, new PowerSourceMetadata(45f, 0f, BasePowerSourceType.SOLAR));
        packetSender.ClearReceivedCalls();

        await Send(simulator, panel, new PowerSourceMetadata(31f, 0f, BasePowerSourceType.SOLAR));

        Assert.AreEqual(45f, ((PowerSourceMetadata)panel.Metadata).Power);
        Assert.AreEqual(45f, ((PowerSourceMetadata)ConnectedPlayers.SentTo<EntityMetadataUpdate>(packetSender, simulator).Single().NewValue).Power,
            "It is told the level that counts now.");
        Assert.AreEqual(0, ConnectedPlayers.SentTo<EntityMetadataUpdate>(packetSender, thirdPlayer).Count);
    }

    [TestMethod]
    public async Task StateASourceCannotHaveIsNotStoredAndTheSenderIsCorrected()
    {
        ModuleEntity panel = AddSource(new PowerSourceMetadata(30f, 0f, BasePowerSourceType.SOLAR), simulator);

        await Send(simulator, panel, new PowerSourceMetadata(9000f, 0f, BasePowerSourceType.SOLAR));

        Assert.AreEqual(30f, ((PowerSourceMetadata)panel.Metadata).Power);
        Assert.AreEqual(30f, ((PowerSourceMetadata)ConnectedPlayers.SentTo<EntityMetadataUpdate>(packetSender, simulator).Single().NewValue).Power);
        Assert.AreEqual(0, ConnectedPlayers.SentTo<EntityMetadataUpdate>(packetSender, otherPlayer).Count);
    }

    [TestMethod]
    public async Task OtherMetadataCannotTakeThePlaceOfASourcesPowerState()
    {
        // It would erase level and fuel progress; the source would then come back full for the next player who joins.
        ModuleEntity panel = AddSource(new PowerSourceMetadata(30f, 0f, BasePowerSourceType.SOLAR), simulator);

        await Send(otherPlayer, panel, new BatteryMetadata(100f));
        await Send(simulator, panel, new BatteryMetadata(100f));

        Assert.AreEqual(30f, ((PowerSourceMetadata)panel.Metadata).Power);
        Assert.AreEqual(0, packetSender.ReceivedCalls().Count());
    }

    [TestMethod]
    public async Task TemperatureFromAnyPlayerIsMergedIntoThePowerStateAndPassedOn()
    {
        // Every player near a thermal plant measures the water; none of them needs to simulate the plant for that.
        ModuleEntity plant = AddSource(new PowerSourceMetadata(120f, 0f, BasePowerSourceType.THERMAL, 40f), simulator);

        await Send(otherPlayer, plant, new ThermalPlantMetadata(63f));

        PowerSourceMetadata stored = (PowerSourceMetadata)plant.Metadata;
        Assert.AreEqual(63f, stored.Temperature);
        Assert.AreEqual(120f, stored.Power);
        Assert.AreEqual(63f, ((ThermalPlantMetadata)ConnectedPlayers.SentTo<EntityMetadataUpdate>(packetSender, simulator).Single().NewValue).Temperature);
        Assert.AreEqual(63f, ((ThermalPlantMetadata)ConnectedPlayers.SentTo<EntityMetadataUpdate>(packetSender, thirdPlayer).Single().NewValue).Temperature);
        Assert.AreEqual(0, ConnectedPlayers.SentTo<EntityMetadataUpdate>(packetSender, otherPlayer).Count);
    }

    [TestMethod]
    public async Task TemperatureThatIsNoNewsIsNotPassedOn()
    {
        ModuleEntity plant = AddSource(new PowerSourceMetadata(120f, 0f, BasePowerSourceType.THERMAL, 40f), simulator);

        await Send(otherPlayer, plant, new ThermalPlantMetadata(40f));
        await Send(otherPlayer, plant, new ThermalPlantMetadata(float.NaN));

        Assert.AreEqual(40f, ((PowerSourceMetadata)plant.Metadata).Temperature);
        Assert.AreEqual(0, packetSender.ReceivedCalls().Count());
    }

    [TestMethod]
    public async Task ImpossibleTemperatureIsNotStoredOrPassedOnAsReported()
    {
        ModuleEntity plant = AddSource(new PowerSourceMetadata(120f, 0f, BasePowerSourceType.THERMAL, 40f), simulator);
        ModuleEntity newPlant = AddSource(null, simulator);

        await Send(otherPlayer, plant, new ThermalPlantMetadata(1e9f));
        await Send(otherPlayer, newPlant, new ThermalPlantMetadata(1e9f));

        Assert.AreEqual(BasePowerManager.MAX_THERMAL_TEMPERATURE, ((PowerSourceMetadata)plant.Metadata).Temperature);
        Assert.AreEqual(BasePowerManager.MAX_THERMAL_TEMPERATURE, ((ThermalPlantMetadata)newPlant.Metadata).Temperature,
            "A plant that has no power state yet stores the report itself.");
        foreach (EntityMetadataUpdate relayed in ConnectedPlayers.SentTo<EntityMetadataUpdate>(packetSender, thirdPlayer))
        {
            Assert.AreEqual(BasePowerManager.MAX_THERMAL_TEMPERATURE, ((ThermalPlantMetadata)relayed.NewValue).Temperature);
        }
        Assert.AreEqual(2, ConnectedPlayers.SentTo<EntityMetadataUpdate>(packetSender, thirdPlayer).Count);
    }

    [TestMethod]
    public async Task MetadataThatHasNothingToDoWithPowerStillPassesFromAnyPlayer()
    {
        ModuleEntity locker = AddSource(null, simulator);

        await Send(otherPlayer, locker, new BatteryMetadata(55f));

        Assert.AreEqual(55f, ((BatteryMetadata)locker.Metadata).Charge);
        Assert.AreEqual(1, ConnectedPlayers.SentTo<EntityMetadataUpdate>(packetSender, simulator).Count);
        Assert.AreEqual(0, ConnectedPlayers.SentTo<EntityMetadataUpdate>(packetSender, otherPlayer).Count);
    }

    private Task Send(Player sender, Entity entity, EntityMetadata metadata)
    {
        return processor.Process(new AuthProcessorContext(sender, packetSender), new EntityMetadataUpdate(entity.Id, metadata));
    }

    private ModuleEntity AddSource(EntityMetadata metadata, Player simulatedBy)
    {
        ModuleEntity source = ModuleEntity.MakeEmpty();
        source.Id = new NitroxId();
        source.Metadata = metadata;
        entityRegistry.AddEntity(source);
        Assert.IsTrue(simulationOwnershipData.TryAcquire(source.Id, simulatedBy, SimulationLockType.TRANSIENT));
        return source;
    }
}
