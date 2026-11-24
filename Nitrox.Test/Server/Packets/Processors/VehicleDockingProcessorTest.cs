using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Nitrox.Model.Core;
using Nitrox.Model.DataStructures;
using Nitrox.Model.DataStructures.Unity;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Bases;
using Nitrox.Model.Subnautica.Packets;
using Nitrox.Server.Subnautica.Models.GameLogic;
using Nitrox.Server.Subnautica.Models.GameLogic.Entities;
using Nitrox.Server.Subnautica.Models.Packets.Core;
using Nitrox.Test.Server.Helper;
using NSubstitute;

namespace Nitrox.Server.Subnautica.Models.Packets.Processors;

[TestClass]
public sealed class VehicleDockingProcessorTest
{
    private ConnectedPlayers players;
    private EntityRegistry entityRegistry;
    private SimulationOwnershipData simulationOwnershipData;
    private IPacketSender packetSender;
    private VehicleDockingProcessor processor;
    private Player playerA;
    private Player playerB;

    [TestInitialize]
    public void Init()
    {
        players = new ConnectedPlayers();
        entityRegistry = new EntityRegistry(NullLogger<EntityRegistry>.Instance);
        simulationOwnershipData = new SimulationOwnershipData();
        packetSender = Substitute.For<IPacketSender>();
        processor = new VehicleDockingProcessor(entityRegistry, simulationOwnershipData, NullLogger<VehicleDockingProcessor>.Instance);
        playerA = players.Connect("PlayerA");
        playerB = players.Connect("PlayerB");
    }

    [TestMethod]
    public async Task DockingIsStoredAndToldToTheOthersInTheSendersName()
    {
        MoonpoolEntity bay = AddBay();
        VehicleEntity seamoth = AddVehicle(playerA);

        // The packet names another player's session
        await Send(playerA, new VehicleDocking(seamoth.Id, bay.Id, playerB.SessionId));

        Assert.AreEqual(bay.Id, seamoth.ParentId);
        VehicleDocking relayed = packetSender.ReceivedCalls()
                                             .Where(call => call.GetMethodInfo().Name == nameof(IPacketSender.SendPacketToOthersAsync))
                                             .Select(call => call.GetArguments()[0])
                                             .OfType<VehicleDocking>()
                                             .Single();
        Assert.AreEqual(playerA.SessionId, relayed.SessionId);
    }

    [TestMethod]
    public async Task SecondVehicleDockedIntoTheSameBayIsSentOutOfItsPlayersGame()
    {
        MoonpoolEntity bay = AddBay();
        VehicleEntity seamoth = AddVehicle(playerA);
        VehicleEntity prawn = AddVehicle(playerB);
        await Send(playerA, new VehicleDocking(seamoth.Id, bay.Id, playerA.SessionId));

        await Send(playerB, new VehicleDocking(prawn.Id, bay.Id, playerB.SessionId));

        Assert.IsNull(prawn.ParentId);
        Assert.AreEqual(bay.Id, seamoth.ParentId);
        VehicleUndocking sendOut = ConnectedPlayers.SentTo<VehicleUndocking>(packetSender, playerB).Single();
        Assert.AreEqual(prawn.Id, sendOut.VehicleId);
        Assert.IsFalse(sendOut.UndockingStart, "The finishing half frees the vehicle in its player's game.");
    }

    [TestMethod]
    public async Task DockingOfAVehicleTheSenderDoesNotSimulateIsIgnored()
    {
        MoonpoolEntity bay = AddBay();
        VehicleEntity seamoth = AddVehicle(playerA);

        await Send(playerB, new VehicleDocking(seamoth.Id, bay.Id, playerB.SessionId));

        Assert.IsNull(seamoth.ParentId);
        Assert.AreEqual(0, packetSender.ReceivedCalls().Count());
    }

    private MoonpoolEntity AddBay()
    {
        MoonpoolEntity bay = new(new NitroxId(), new NitroxId(), new NitroxInt3(0, 0, 0));
        entityRegistry.AddEntity(bay);
        return bay;
    }

    private VehicleEntity AddVehicle(Player simulatedBy)
    {
        VehicleEntity vehicle = new(null, 0f, new NitroxTransform(), "", false, new NitroxId(), new NitroxTechType("Seamoth"), null);
        entityRegistry.AddEntity(vehicle);
        Assert.IsTrue(simulationOwnershipData.TryAcquire(vehicle.Id, simulatedBy, SimulationLockType.EXCLUSIVE));
        return vehicle;
    }

    private Task Send(Player sender, VehicleDocking packet)
    {
        return processor.Process(new AuthProcessorContext(sender, packetSender), packet);
    }
}
