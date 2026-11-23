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

namespace Nitrox.Server.Subnautica.Models.Packets.Processors;

[TestClass]
public sealed class CreatureBitePlayedProcessorTest
{
    private ConnectedPlayers players;
    private SimulationOwnershipData simulationOwnershipData;
    private IPacketSender packetSender;
    private CreatureBitePlayedProcessor processor;
    private WorldEntity stalker;
    private Player simulator;
    private Player bitten;
    private Player third;

    [TestInitialize]
    public void Init()
    {
        players = new ConnectedPlayers();
        EntityRegistry entityRegistry = new(NullLogger<EntityRegistry>.Instance);
        simulationOwnershipData = new SimulationOwnershipData();
        packetSender = Substitute.For<IPacketSender>();
        processor = new CreatureBitePlayedProcessor(players.PlayerManager, entityRegistry, simulationOwnershipData);

        stalker = new WorldEntity(new NitroxVector3(-30f, -60f, 200f), NitroxQuaternion.Identity, NitroxVector3.One, new NitroxTechType("Stalker"), 0, "stalker", true,
                                  new NitroxId(), null);
        entityRegistry.AddEntity(stalker);
        simulator = players.Connect("Simulator");
        bitten = players.Connect("Bitten");
        third = players.Connect("Third");
        foreach (Player player in new[] { simulator, bitten, third })
        {
            player.AddCells([stalker.AbsoluteEntityCell]);
        }
        Assert.IsTrue(simulationOwnershipData.TryAcquire(stalker.Id, simulator, SimulationLockType.TRANSIENT));
    }

    [TestMethod]
    public async Task BiteReachesEveryOtherPlayerWhoSeesTheStalker()
    {
        await processor.Process(new AuthProcessorContext(simulator, packetSender), new CreatureBitePlayed(stalker.Id));

        Assert.AreEqual(1, ConnectedPlayers.SentTo<CreatureBitePlayed>(packetSender, bitten).Count);
        Assert.AreEqual(1, ConnectedPlayers.SentTo<CreatureBitePlayed>(packetSender, third).Count);
        Assert.AreEqual(0, ConnectedPlayers.SentTo<CreatureBitePlayed>(packetSender, simulator).Count);
    }

    [TestMethod]
    public async Task BiteFromAPlayerWhoDoesNotSimulateTheStalkerIsDropped()
    {
        await processor.Process(new AuthProcessorContext(third, packetSender), new CreatureBitePlayed(stalker.Id));

        Assert.AreEqual(0, packetSender.ReceivedCalls().Count());
    }
}
