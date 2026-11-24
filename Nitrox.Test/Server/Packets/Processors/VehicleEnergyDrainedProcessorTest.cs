using System.Threading.Tasks;
using Nitrox.Model.Core;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.Packets;
using Nitrox.Server.Subnautica.Models.GameLogic;
using Nitrox.Server.Subnautica.Models.Packets.Core;
using Nitrox.Test.Server.Helper;
using NSubstitute;

namespace Nitrox.Server.Subnautica.Models.Packets.Processors;

[TestClass]
public sealed class VehicleEnergyDrainedProcessorTest
{
    private ConnectedPlayers players;
    private SimulationOwnershipData simulationOwnershipData;
    private IPacketSender packetSender;
    private VehicleEnergyDrainedProcessor processor;
    private Player larvaSimulator;
    private Player vehicleSimulator;
    private Player bystander;

    [TestInitialize]
    public void Init()
    {
        players = new ConnectedPlayers();
        simulationOwnershipData = new SimulationOwnershipData();
        packetSender = Substitute.For<IPacketSender>();
        processor = new VehicleEnergyDrainedProcessor(simulationOwnershipData);
        larvaSimulator = players.Connect("LarvaSimulator");
        vehicleSimulator = players.Connect("VehicleSimulator");
        bystander = players.Connect("Bystander");
    }

    [TestMethod]
    public async Task DrainGoesToThePlayerSimulatingTheVehicleOnly()
    {
        NitroxId seamothId = new();
        Assert.IsTrue(simulationOwnershipData.TryAcquire(seamothId, vehicleSimulator, SimulationLockType.EXCLUSIVE));

        await Send(larvaSimulator, new VehicleEnergyDrained(seamothId, 2.5f));

        Assert.AreEqual(2.5f, ConnectedPlayers.SentTo<VehicleEnergyDrained>(packetSender, vehicleSimulator).Single().Amount);
        Assert.AreEqual(0, ConnectedPlayers.SentTo<VehicleEnergyDrained>(packetSender, bystander).Count, "Taken twice, the drain would be doubled.");
        Assert.AreEqual(0, ConnectedPlayers.SentTo<VehicleEnergyDrained>(packetSender, larvaSimulator).Count);
    }

    [TestMethod]
    public async Task DrainOfAVehicleNobodySimulatesOrTheSenderSimulatesGoesNowhere()
    {
        NitroxId unsimulated = new();
        NitroxId ownVehicle = new();
        Assert.IsTrue(simulationOwnershipData.TryAcquire(ownVehicle, larvaSimulator, SimulationLockType.TRANSIENT));

        await Send(larvaSimulator, new VehicleEnergyDrained(unsimulated, 1f));
        await Send(larvaSimulator, new VehicleEnergyDrained(ownVehicle, 1f));
        await Send(larvaSimulator, new VehicleEnergyDrained(ownVehicle, -5f));

        Assert.AreEqual(0, packetSender.ReceivedCalls().Count());
    }

    private Task Send(Player sender, VehicleEnergyDrained packet)
    {
        return processor.Process(new AuthProcessorContext(sender, packetSender), packet);
    }
}
