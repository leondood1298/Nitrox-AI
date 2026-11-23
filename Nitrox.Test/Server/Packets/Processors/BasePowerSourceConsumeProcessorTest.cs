using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Nitrox.Model.DataStructures;
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
///     Energy a player takes from a base power source it does not simulate has to reach the level that counts: the
///     simulating player's, or the stored one when nobody simulates the source.
/// </summary>
[TestClass]
public sealed class BasePowerSourceConsumeProcessorTest
{
    private ConnectedPlayers players;
    private EntityRegistry entityRegistry;
    private SimulationOwnershipData simulationOwnershipData;
    private BasePowerSourceConsumeProcessor processor;
    private IPacketSender packetSender;
    private Player simulator;
    private Player consumer;
    private ModuleEntity panel;

    [TestInitialize]
    public void Init()
    {
        players = new ConnectedPlayers();
        entityRegistry = new EntityRegistry(NullLogger<EntityRegistry>.Instance);
        simulationOwnershipData = new SimulationOwnershipData();
        BasePowerDiagnostics diagnostics = new();
        BasePowerManager basePowerManager = new(entityRegistry, simulationOwnershipData, diagnostics, new DiscardingLogger<BasePowerManager>());
        processor = new BasePowerSourceConsumeProcessor(entityRegistry, basePowerManager, diagnostics, new DiscardingLogger<BasePowerSourceConsumeProcessor>());
        packetSender = Substitute.For<IPacketSender>();
        simulator = players.Connect("Simulator");
        consumer = players.Connect("Consumer");

        panel = ModuleEntity.MakeEmpty();
        panel.Id = new NitroxId();
        panel.Metadata = new PowerSourceMetadata(60f, 0f, BasePowerSourceType.SOLAR);
        entityRegistry.AddEntity(panel);
    }

    [TestMethod]
    public async Task ConsumptionIsPassedOnlyToThePlayerSimulatingTheSource()
    {
        // The consumer docks a vehicle in a base whose solar panel another player simulates.
        Assert.IsTrue(simulationOwnershipData.TryAcquire(panel.Id, simulator, SimulationLockType.TRANSIENT));
        BasePowerSourceConsume consumption = new(panel.Id, 2.5f);

        await processor.Process(new AuthProcessorContext(consumer, packetSender), consumption);

        Assert.AreSame(consumption, ConnectedPlayers.SentTo<BasePowerSourceConsume>(packetSender, simulator).Single());
        Assert.AreEqual(1, packetSender.ReceivedCalls().Count());
        Assert.AreEqual(60f, ((PowerSourceMetadata)panel.Metadata).Power, "The simulating player applies it; the stored state follows its next update.");
    }

    [TestMethod]
    public async Task ConsumptionFollowsTheSourceToItsNewSimulatingPlayer()
    {
        // The simulating player left and the server gave the source to someone else.
        Player newSimulator = players.Connect("NewSimulator");
        Assert.IsTrue(simulationOwnershipData.TryAcquire(panel.Id, simulator, SimulationLockType.TRANSIENT));
        simulationOwnershipData.RevokeAllLocksForOwner(simulator.SessionId);
        Assert.IsTrue(simulationOwnershipData.TryAcquire(panel.Id, newSimulator, SimulationLockType.TRANSIENT));

        await processor.Process(new AuthProcessorContext(consumer, packetSender), new BasePowerSourceConsume(panel.Id, 2.5f));

        Assert.AreEqual(2.5f, ConnectedPlayers.SentTo<BasePowerSourceConsume>(packetSender, newSimulator).Single().Amount);
        Assert.AreEqual(0, ConnectedPlayers.SentTo<BasePowerSourceConsume>(packetSender, simulator).Count);
    }

    [TestMethod]
    public async Task ConsumptionFromASourceNobodySimulatesIsAppliedAndToldToEveryone()
    {
        await processor.Process(new AuthProcessorContext(consumer, packetSender), new BasePowerSourceConsume(panel.Id, 2.5f));

        Assert.AreEqual(57.5f, ((PowerSourceMetadata)panel.Metadata).Power);
        EntityMetadataUpdate update = ConnectedPlayers.SentToAll<EntityMetadataUpdate>(packetSender).Single();
        Assert.AreEqual(panel.Id, update.Id);
        Assert.AreEqual(57.5f, ((PowerSourceMetadata)update.NewValue).Power);
        Assert.AreEqual(1, packetSender.ReceivedCalls().Count());
    }

    [TestMethod]
    public async Task ConsumptionReportedByTheSimulatingPlayerItselfGoesNowhere()
    {
        Assert.IsTrue(simulationOwnershipData.TryAcquire(panel.Id, simulator, SimulationLockType.TRANSIENT));

        await processor.Process(new AuthProcessorContext(simulator, packetSender), new BasePowerSourceConsume(panel.Id, 2.5f));

        Assert.AreEqual(0, packetSender.ReceivedCalls().Count());
        Assert.AreEqual(60f, ((PowerSourceMetadata)panel.Metadata).Power);
    }

    [TestMethod]
    public async Task ConsumptionFromAnUnknownSourceGoesNowhere()
    {
        await processor.Process(new AuthProcessorContext(consumer, packetSender), new BasePowerSourceConsume(new NitroxId(), 2.5f));

        Assert.AreEqual(0, packetSender.ReceivedCalls().Count());
    }
}
