using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Nitrox.Model.DataStructures;
using Nitrox.Model.DataStructures.Unity;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities;
using Nitrox.Model.Subnautica.Packets;
using Nitrox.Server.Subnautica.Models.GameLogic;
using Nitrox.Server.Subnautica.Models.GameLogic.Entities;
using Nitrox.Server.Subnautica.Models.Packets.Core;
using Nitrox.Test.Server.Helper;
using NSubstitute;
using static Nitrox.Model.Subnautica.Packets.SandSharkBurrowChanged;

namespace Nitrox.Server.Subnautica.Models.Packets.Processors;

[TestClass]
public sealed class SandSharkBurrowChangedProcessorTest
{
    private ConnectedPlayers players;
    private EntityRegistry entityRegistry;
    private SimulationOwnershipData simulationOwnershipData;
    private IPacketSender packetSender;
    private SandSharkBurrowChangedProcessor processor;
    private WorldEntity shark;
    private Player simulator;
    private Player watcher;

    [TestInitialize]
    public void Init()
    {
        players = new ConnectedPlayers();
        entityRegistry = new EntityRegistry(NullLogger<EntityRegistry>.Instance);
        simulationOwnershipData = new SimulationOwnershipData();
        packetSender = Substitute.For<IPacketSender>();
        processor = new SandSharkBurrowChangedProcessor(players.PlayerManager, entityRegistry, simulationOwnershipData);

        shark = new WorldEntity(new NitroxVector3(100f, -200f, 50f), NitroxQuaternion.Identity, NitroxVector3.One, new NitroxTechType("Sandshark"), 0, "sandshark", true,
                                new NitroxId(), null);
        entityRegistry.AddEntity(shark);
        simulator = players.Connect("Simulator");
        watcher = players.Connect("Watcher");
        simulator.AddCells([shark.AbsoluteEntityCell]);
        watcher.AddCells([shark.AbsoluteEntityCell]);
        Assert.IsTrue(simulationOwnershipData.TryAcquire(shark.Id, simulator, SimulationLockType.TRANSIENT));
    }

    [TestMethod]
    public async Task BurrowOfTheSimulatingPlayersSharkReachesThePlayersWhoSeeIt()
    {
        await Send(simulator);

        Assert.AreEqual(BurrowState.BURIED, ConnectedPlayers.SentTo<SandSharkBurrowChanged>(packetSender, watcher).Single().State);
        Assert.AreEqual(0, ConnectedPlayers.SentTo<SandSharkBurrowChanged>(packetSender, simulator).Count, "Its own game already shows it.");
    }

    [TestMethod]
    public async Task PlayerWhoDoesNotSeeTheSharkIsNotTold()
    {
        watcher.ClearVisibleCells();

        await Send(simulator);

        Assert.AreEqual(0, packetSender.ReceivedCalls().Count());
    }

    [TestMethod]
    public async Task BurrowFromAPlayerWhoDoesNotSimulateTheSharkIsDropped()
    {
        // That player's copy only replays what it is told; anything it sent would contradict the shark
        await Send(watcher);

        Assert.AreEqual(0, packetSender.ReceivedCalls().Count());
    }

    [TestMethod]
    public async Task BurrowOfASharkNobodySimulatesIsDropped()
    {
        simulationOwnershipData.RevokeIfOwner(shark.Id, simulator);

        await Send(simulator);

        Assert.AreEqual(0, packetSender.ReceivedCalls().Count());
    }

    private Task Send(Player sender)
    {
        return processor.Process(new AuthProcessorContext(sender, packetSender), new SandSharkBurrowChanged(shark.Id, BurrowState.BURIED, shark.Transform.Position, NitroxQuaternion.Identity));
    }
}
