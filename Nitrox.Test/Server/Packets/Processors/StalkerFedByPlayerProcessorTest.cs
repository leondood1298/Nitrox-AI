using System.Threading.Tasks;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.Packets;
using Nitrox.Server.Subnautica.Models.GameLogic;
using Nitrox.Server.Subnautica.Models.Packets.Core;
using Nitrox.Test.Server.Helper;
using NSubstitute;

namespace Nitrox.Server.Subnautica.Models.Packets.Processors;

[TestClass]
public sealed class StalkerFedByPlayerProcessorTest
{
    private readonly NitroxId stalkerId = new();
    private ConnectedPlayers players;
    private SimulationOwnershipData simulationOwnershipData;
    private IPacketSender packetSender;
    private StalkerFedByPlayerProcessor processor;
    private Player simulator;
    private Player feeder;
    private Player bystander;

    [TestInitialize]
    public void Init()
    {
        players = new ConnectedPlayers();
        simulationOwnershipData = new SimulationOwnershipData();
        packetSender = Substitute.For<IPacketSender>();
        processor = new StalkerFedByPlayerProcessor(simulationOwnershipData);
        simulator = players.Connect("Simulator");
        feeder = players.Connect("Feeder");
        bystander = players.Connect("Bystander");
        Assert.IsTrue(simulationOwnershipData.TryAcquire(stalkerId, simulator, SimulationLockType.TRANSIENT));
    }

    [TestMethod]
    public async Task FeedingReachesThePlayerWhoSimulatesTheStalkerOnly()
    {
        await Send(feeder, new StalkerFedByPlayer(stalkerId, feeder.GameObjectId, 1.5f));

        Assert.AreEqual(1.5f, ConnectedPlayers.SentTo<StalkerFedByPlayer>(packetSender, simulator).Single().FoodValue);
        Assert.AreEqual(0, ConnectedPlayers.SentTo<StalkerFedByPlayer>(packetSender, bystander).Count);
    }

    [TestMethod]
    public async Task PlayerCannotFeedInSomebodyElsesName()
    {
        // Or they could make any Stalker befriend anybody
        await Send(feeder, new StalkerFedByPlayer(stalkerId, bystander.GameObjectId, 1f));

        Assert.AreEqual(0, packetSender.ReceivedCalls().Count());
    }

    [TestMethod]
    [DataRow(0f)]
    [DataRow(-1f)]
    [DataRow(3.5f)]
    [DataRow(float.NaN)]
    public void FoodValueOutsideWhatAFishFeedsIsRefused(float foodValue)
    {
        Assert.IsFalse(StalkerFedByPlayerProcessor.IsValid(new StalkerFedByPlayer(stalkerId, feeder.GameObjectId, foodValue), simulator, feeder));
    }

    [TestMethod]
    public async Task FeedingAStalkerNobodySimulatesGoesNowhere()
    {
        simulationOwnershipData.RevokeIfOwner(stalkerId, simulator);

        await Send(feeder, new StalkerFedByPlayer(stalkerId, feeder.GameObjectId, 1f));

        Assert.AreEqual(0, packetSender.ReceivedCalls().Count());
    }

    private Task Send(Player sender, StalkerFedByPlayer packet) => processor.Process(new AuthProcessorContext(sender, packetSender), packet);
}
