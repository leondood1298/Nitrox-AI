using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;
using Nitrox.Model.Subnautica.Packets;
using NitroxClient.Communication.Abstract;
using NitroxClient.GameLogic;
using NSubstitute;

namespace Nitrox.Test.Client.GameLogic;

/// <summary>
///     Only the player simulating a water filtration machine runs its timers and makes its items; that player sends
///     the timers when they start, stop or start over, and otherwise every few seconds while they run.
/// </summary>
[TestClass]
public sealed class FiltrationMachinesTest
{
    private const float IDLE = FiltrationMachineMetadata.IDLE;

    private readonly FiltrationMachines machines;

    public FiltrationMachinesTest()
    {
        IPacketSender packetSender = Substitute.For<IPacketSender>();
        machines = new FiltrationMachines(packetSender, new SimulationOwnership(Substitute.For<IMultiplayerSession>(), packetSender), null!);
    }

    [TestMethod]
    public void OnlyTheSimulatingPlayerFilters()
    {
        Assert.IsTrue(FiltrationMachines.FiltersHere(hasId: true, simulatedLocally: true, rebuildingBases: false, finishedLoading: true));
        Assert.IsFalse(FiltrationMachines.FiltersHere(hasId: true, simulatedLocally: false, rebuildingBases: false, finishedLoading: true));
    }

    [TestMethod]
    public void MachineWithoutIdFiltersAsInTheGame()
    {
        Assert.IsTrue(FiltrationMachines.FiltersHere(hasId: false, simulatedLocally: false, rebuildingBases: false, finishedLoading: false));
    }

    [TestMethod]
    public void SimulatingPlayerDoesNotFilterWhileItsItemsAreStillSpawning()
    {
        // The stored items arrive after the machine; filtering then would fill it up on top of them.
        Assert.IsFalse(FiltrationMachines.FiltersHere(hasId: true, simulatedLocally: true, rebuildingBases: false, finishedLoading: false));
        Assert.IsFalse(FiltrationMachines.FiltersHere(hasId: true, simulatedLocally: true, rebuildingBases: true, finishedLoading: true));
    }

    [TestMethod]
    public void FirstStateIsSentAtOnce()
    {
        Assert.IsTrue(FiltrationMachines.ShouldBroadcast(null, 0f, 600f, IDLE));
    }

    [TestMethod]
    public void UnchangedTimersAreNotSent()
    {
        // A full or unpowered machine does not count down
        FiltrationMachines.Broadcast last = new(0f, 600f, IDLE);

        Assert.IsFalse(FiltrationMachines.ShouldBroadcast(last, 120f, 600f, IDLE));
    }

    [TestMethod]
    public void RunningTimersAreSentEveryFewSeconds()
    {
        FiltrationMachines.Broadcast last = new(0f, 600f, 300f);

        Assert.IsFalse(FiltrationMachines.ShouldBroadcast(last, 1f, 599f, 299f));
        Assert.IsFalse(FiltrationMachines.ShouldBroadcast(last, 4f, 596f, 296f));
        Assert.IsTrue(FiltrationMachines.ShouldBroadcast(last, 5f, 595f, 295f));
    }

    [TestMethod]
    public void TimerThatStopsIsSentAtOnce()
    {
        // The machine filled up: the display says it is done
        FiltrationMachines.Broadcast last = new(0f, 1f, 300f);

        Assert.IsTrue(FiltrationMachines.ShouldBroadcast(last, 1f, IDLE, 299f));
    }

    [TestMethod]
    public void TimerThatStartsIsSentAtOnce()
    {
        // Someone took the salt out of a full machine
        FiltrationMachines.Broadcast last = new(0f, 600f, IDLE);

        Assert.IsTrue(FiltrationMachines.ShouldBroadcast(last, 1f, 599f, FiltrationMachineMetadata.SALT_SECONDS));
    }

    [TestMethod]
    public void TimerThatStartsOverIsSentAtOnce()
    {
        // A bottle was made and the next one started in the same update
        FiltrationMachines.Broadcast last = new(0f, 1f, 300f);

        Assert.IsTrue(FiltrationMachines.ShouldBroadcast(last, 1f, FiltrationMachineMetadata.WATER_SECONDS, 299f));
    }

    [TestMethod]
    public void UpdateCarriesTheTimersAndIsRememberedAsSent()
    {
        NitroxId machineId = new();
        FiltrationMachineMetadata state = new(600f, IDLE);

        Assert.IsTrue(machines.TryCreateUpdate(machineId, state, 0f, out EntityMetadataUpdate update));
        Assert.AreEqual(machineId, update.Id);
        Assert.AreSame(state, update.NewValue);

        Assert.IsFalse(machines.TryCreateUpdate(machineId, new FiltrationMachineMetadata(599f, IDLE), 1f, out _), "Progress waits for the next interval.");
        Assert.IsTrue(machines.TryCreateUpdate(new NitroxId(), new FiltrationMachineMetadata(599f, IDLE), 1f, out _), "Each machine is counted on its own.");
    }
}
