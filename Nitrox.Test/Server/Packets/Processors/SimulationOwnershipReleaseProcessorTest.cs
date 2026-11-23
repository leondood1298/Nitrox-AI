using System.Threading.Tasks;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Bases;
using Nitrox.Model.Subnautica.Packets;
using Nitrox.Server.Subnautica.Models.Packets.Core;
using Nitrox.Test.Server.GameLogic;
using Nitrox.Test.Server.Helper;
using NSubstitute;

namespace Nitrox.Server.Subnautica.Models.Packets.Processors;

/// <summary>
///     A player who was given an entity it cannot simulate (the object does not exist on its side) hands it back.
/// </summary>
[TestClass]
public sealed class SimulationOwnershipReleaseProcessorTest
{
    private ScannerRoomScenarioFixture scenario;
    private ConnectedPlayers players;
    private SimulationOwnershipReleaseProcessor processor;
    private IPacketSender packetSender;
    private Player releasingPlayer;
    private ModuleEntity source;

    [TestInitialize]
    public void Init()
    {
        scenario = new ScannerRoomScenarioFixture();
        players = new ConnectedPlayers();
        processor = new SimulationOwnershipReleaseProcessor(players.PlayerManager, scenario.EntitySimulation, scenario.EntityRegistry);
        packetSender = Substitute.For<IPacketSender>();
        releasingPlayer = players.Connect("Releasing");

        source = ModuleEntity.MakeEmpty();
        source.Id = new NitroxId();
        scenario.EntityRegistry.AddEntity(source);
        Assert.IsTrue(scenario.Ownership.TryAcquire(source.Id, releasingPlayer, SimulationLockType.TRANSIENT));
    }

    [TestMethod]
    public async Task ReleasedEntityGoesToAnotherConnectedPlayer()
    {
        Player otherPlayer = players.Connect("Other");

        await processor.Process(new AuthProcessorContext(releasingPlayer, packetSender), new SimulationOwnershipRelease(source.Id));

        Assert.AreEqual(otherPlayer, scenario.Ownership.GetPlayerForLock(source.Id));
        Assert.AreEqual(0, ConnectedPlayers.SentToAll<DropSimulationOwnership>(packetSender).Count);
    }

    [TestMethod]
    public async Task ReleasedEntityWithNobodyElseIsAnnouncedAsDropped()
    {
        // The server then applies consumption from a power source itself, and the next player to join gets it.
        await processor.Process(new AuthProcessorContext(releasingPlayer, packetSender), new SimulationOwnershipRelease(source.Id));

        Assert.IsNull(scenario.Ownership.GetPlayerForLock(source.Id));
        CollectionAssert.AreEqual(new[] { source.Id }, ConnectedPlayers.SentToAll<DropSimulationOwnership>(packetSender).Single().EntityIds);
    }

    [TestMethod]
    public async Task ReleaseByAPlayerWhoDoesNotSimulateTheEntityChangesNothing()
    {
        Player otherPlayer = players.Connect("Other");

        await processor.Process(new AuthProcessorContext(otherPlayer, packetSender), new SimulationOwnershipRelease(source.Id));
        await processor.Process(new AuthProcessorContext(otherPlayer, packetSender), new SimulationOwnershipRelease(new NitroxId()));

        Assert.AreEqual(releasingPlayer, scenario.Ownership.GetPlayerForLock(source.Id));
        Assert.AreEqual(0, packetSender.ReceivedCalls().Count());
    }
}
