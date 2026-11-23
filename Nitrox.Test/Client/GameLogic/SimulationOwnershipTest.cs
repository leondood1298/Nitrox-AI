using Nitrox.Model.Core;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.Packets;
using NitroxClient.Communication.Abstract;
using NitroxClient.GameLogic;
using NSubstitute;

namespace Nitrox.Test.Client.GameLogic;

/// <summary>
///     The list of simulating players in the initial sync is made when a player begins to join. What the server
///     changes while that player loads is newer than the list.
/// </summary>
[TestClass]
public sealed class SimulationOwnershipTest
{
    private static readonly SessionId localSession = (SessionId)(ushort)1;
    private static readonly SessionId otherSession = (SessionId)(ushort)2;

    private readonly NitroxId sourceId = new();
    private readonly SimulationOwnership simulationOwnership;

    public SimulationOwnershipTest()
    {
        IMultiplayerSession multiplayerSession = Substitute.For<IMultiplayerSession>();
        multiplayerSession.Reservation.Returns(new MultiplayerSessionReservation(localSession));
        simulationOwnership = new SimulationOwnership(multiplayerSession, Substitute.For<IPacketSender>());
    }

    [TestMethod]
    public void InitialListGivesThisClientWhatItSimulates()
    {
        simulationOwnership.TreatInitialSimulatedEntity(Simulated(localSession));
        simulationOwnership.InitialListApplied();

        Assert.IsTrue(simulationOwnership.HasAnyLockType(sourceId));
    }

    [TestMethod]
    public void EntityGivenToThisClientWhileLoadingIsNotDroppedByTheOlderInitialList()
    {
        // The only player in the world left while this one was loading, so the server handed everything over.
        simulationOwnership.TreatSimulatedEntity(Simulated(localSession));

        simulationOwnership.TreatInitialSimulatedEntity(Simulated(otherSession));
        simulationOwnership.InitialListApplied();

        Assert.IsTrue(simulationOwnership.HasAnyLockType(sourceId));
    }

    [TestMethod]
    public void EntityTakenFromThisClientWhileLoadingIsNotGivenBackByTheOlderInitialList()
    {
        simulationOwnership.TreatSimulatedEntity(Simulated(otherSession));

        simulationOwnership.TreatInitialSimulatedEntity(Simulated(localSession));
        simulationOwnership.InitialListApplied();

        Assert.IsFalse(simulationOwnership.HasAnyLockType(sourceId));
    }

    [TestMethod]
    public void EntityDroppedWhileLoadingIsNotGivenBackByTheOlderInitialList()
    {
        simulationOwnership.DropSimulationFrom(sourceId);

        simulationOwnership.TreatInitialSimulatedEntity(Simulated(localSession));
        simulationOwnership.InitialListApplied();

        Assert.IsFalse(simulationOwnership.HasAnyLockType(sourceId));
    }

    [TestMethod]
    public void ChangesAfterTheInitialListAreAppliedAsBefore()
    {
        simulationOwnership.TreatInitialSimulatedEntity(Simulated(otherSession));
        simulationOwnership.InitialListApplied();

        simulationOwnership.TreatSimulatedEntity(Simulated(localSession));
        Assert.IsTrue(simulationOwnership.HasAnyLockType(sourceId));

        simulationOwnership.TreatSimulatedEntity(Simulated(otherSession));
        Assert.IsFalse(simulationOwnership.HasAnyLockType(sourceId));
    }

    private SimulatedEntity Simulated(SessionId by) => new(sourceId, by, false, SimulationLockType.TRANSIENT);
}
