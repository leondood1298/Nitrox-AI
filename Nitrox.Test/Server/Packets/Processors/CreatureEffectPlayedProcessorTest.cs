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
public sealed class CreatureEffectPlayedProcessorTest
{
    private ConnectedPlayers players;
    private IPacketSender packetSender;
    private CreatureEffectPlayedProcessor processor;
    private WorldEntity gasopod;
    private Player simulator;
    private Player onlooker;

    [TestInitialize]
    public void Init()
    {
        players = new ConnectedPlayers();
        EntityRegistry entityRegistry = new(NullLogger<EntityRegistry>.Instance);
        SimulationOwnershipData simulationOwnershipData = new();
        packetSender = Substitute.For<IPacketSender>();
        processor = new CreatureEffectPlayedProcessor(players.PlayerManager, entityRegistry, simulationOwnershipData);

        gasopod = new WorldEntity(new NitroxVector3(10f, -40f, 90f), NitroxQuaternion.Identity, NitroxVector3.One, new NitroxTechType("Gasopod"), 0, "gasopod", true, new NitroxId(), null);
        entityRegistry.AddEntity(gasopod);
        simulator = players.Connect("Simulator");
        onlooker = players.Connect("Onlooker");
        foreach (Player player in new[] { simulator, onlooker })
        {
            player.AddCells([gasopod.AbsoluteEntityCell]);
        }
        Assert.IsTrue(simulationOwnershipData.TryAcquire(gasopod.Id, simulator, SimulationLockType.TRANSIENT));
    }

    [TestMethod]
    public async Task EffectReachesTheOthersWhoSeeTheCreature()
    {
        await processor.Process(new AuthProcessorContext(simulator, packetSender), Effect());

        Assert.AreEqual(1, ConnectedPlayers.SentTo<CreatureEffectPlayed>(packetSender, onlooker).Count);
        Assert.AreEqual(0, ConnectedPlayers.SentTo<CreatureEffectPlayed>(packetSender, simulator).Count);
    }

    [TestMethod]
    public async Task EffectFromAPlayerWhoDoesNotSimulateTheCreatureIsDropped()
    {
        await processor.Process(new AuthProcessorContext(onlooker, packetSender), Effect());

        Assert.AreEqual(0, packetSender.ReceivedCalls().Count());
    }

    private CreatureEffectPlayed Effect() => new(gasopod.Id, CreatureEffectPlayed.CreatureEffect.GAS_PODS_RELEASED, 1234, Optional.Empty);
}
