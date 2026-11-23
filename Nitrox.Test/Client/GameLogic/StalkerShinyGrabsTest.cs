using System.Threading.Tasks;
using Nitrox.Model.Core;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.Packets;
using NitroxClient.Communication.Abstract;
using NitroxClient.Communication.Packets.Processors;
using NitroxClient.GameLogic;
using NSubstitute;

namespace Nitrox.Test.Client.GameLogic;

/// <summary>
///     What the client that simulates a Stalker does about the lock of an item the Stalker goes for, with a Stalker that
///     needs no game.
/// </summary>
[TestClass]
public sealed class StalkerShinyGrabsTest
{
    private static readonly SessionId localSession = (SessionId)(ushort)1;

    private readonly IPacketSender packetSender = Substitute.For<IPacketSender>();
    private readonly NitroxId itemId = new();
    private readonly FakeStalker stalker = new();
    private readonly SimulationOwnership simulationOwnership;
    private readonly MapRoomCameras cameras;
    private readonly StalkerCameraGrabs cameraGrabs;
    private readonly StalkerShinyGrabs grabs;

    public StalkerShinyGrabsTest()
    {
        IMultiplayerSession multiplayerSession = Substitute.For<IMultiplayerSession>();
        multiplayerSession.Reservation.Returns(new MultiplayerSessionReservation(localSession));
        simulationOwnership = new SimulationOwnership(multiplayerSession, packetSender);
        cameras = new MapRoomCameras(packetSender, multiplayerSession, simulationOwnership, new ScannerRoomClientDiagnostics());
        cameraGrabs = new StalkerCameraGrabs(simulationOwnership, cameras);
        grabs = new StalkerShinyGrabs(simulationOwnership, cameraGrabs);
    }

    [TestMethod]
    public void StalkerWaitsForTheItemsExclusiveLockAndPicksTheItemUpWhenItIsGranted()
    {
        Assert.IsFalse(grabs.MayPickUp(stalker, itemId));
        CollectionAssert.AreEqual(new[] { SimulationLockType.EXCLUSIVE }, LockRequests());
        Assert.AreEqual(0, stalker.PickedUp);

        simulationOwnership.ReceivedSimulationLockResponse(itemId, true, SimulationLockType.EXCLUSIVE);

        Assert.AreEqual(1, stalker.PickedUp);
        Assert.IsTrue(grabs.MayPickUp(stalker, itemId), "With the lock the game is not held back again.");
        Assert.AreEqual(1, LockRequests().Length);
    }

    [TestMethod]
    public void StalkerAsksForTheLockOnceWhileItWaitsNextToTheItem()
    {
        // The game tries to pick the item up every frame while the Stalker is close to it
        grabs.MayPickUp(stalker, itemId);
        grabs.MayPickUp(stalker, itemId);
        grabs.MayPickUp(stalker, itemId);

        Assert.AreEqual(1, LockRequests().Length);
    }

    [TestMethod]
    public void StalkerForgetsAnItemAnotherPlayerHolds()
    {
        grabs.MayPickUp(stalker, itemId);

        simulationOwnership.ReceivedSimulationLockResponse(itemId, false, SimulationLockType.EXCLUSIVE);

        Assert.AreEqual((0, 1), (stalker.PickedUp, stalker.Forgotten));
        grabs.MayPickUp(stalker, itemId);
        Assert.AreEqual(2, LockRequests().Length, "A refused item may be asked for again later.");
    }

    [TestMethod]
    public void LockIsGivenBackIfTheStalkerWentForSomethingElseMeanwhile()
    {
        grabs.MayPickUp(stalker, itemId);
        stalker.StillWantsTarget = false;

        simulationOwnership.ReceivedSimulationLockResponse(itemId, true, SimulationLockType.EXCLUSIVE);

        Assert.AreEqual(0, stalker.PickedUp);
        CollectionAssert.AreEqual(new[] { SimulationLockType.EXCLUSIVE, SimulationLockType.TRANSIENT }, LockRequests());
    }

    [TestMethod]
    public void DroppedItemStaysSimulatedHereWithATransientLock()
    {
        grabs.MayPickUp(stalker, itemId);
        simulationOwnership.ReceivedSimulationLockResponse(itemId, true, SimulationLockType.EXCLUSIVE);

        grabs.Dropped(itemId);
        grabs.Dropped(itemId);

        // Like a propulsion cannon's release: this client goes on sending where the item falls
        Assert.IsTrue(simulationOwnership.HasAnyLockType(itemId));
        Assert.IsFalse(simulationOwnership.HasExclusiveLock(itemId), "The next Stalker or a player's propulsion cannon can take it.");
        CollectionAssert.AreEqual(new[] { SimulationLockType.EXCLUSIVE, SimulationLockType.TRANSIENT }, LockRequests(), "Once, for the item a Stalker carried.");
    }

    [TestMethod]
    public void DroppingAnItemNoStalkerOfThisClientCarriesLeavesItsLockAlone()
    {
        // The local player holds it in a propulsion cannon, for example
        simulationOwnership.SimulateEntity(itemId, SimulationLockType.EXCLUSIVE);

        grabs.Dropped(itemId);

        Assert.IsTrue(simulationOwnership.HasExclusiveLock(itemId));
        Assert.AreEqual(0, LockRequests().Length);
    }

    [TestMethod]
    public async Task StalkerLetsGoOfTheItemWhenAnotherPlayerTakesItsLock()
    {
        grabs.MayPickUp(stalker, itemId);
        simulationOwnership.ReceivedSimulationLockResponse(itemId, true, SimulationLockType.EXCLUSIVE);
        SimulationOwnershipChangeProcessor processor = new(simulationOwnership, cameras, cameraGrabs, null!, grabs);

        await processor.Process(null!, new SimulationOwnershipChange(itemId, (SessionId)(ushort)2, SimulationLockType.EXCLUSIVE));

        Assert.AreEqual(1, stalker.Dropped);
        Assert.IsFalse(simulationOwnership.HasAnyLockType(itemId));
        Assert.AreEqual(1, LockRequests().Length, "There is no lock left to give back.");
    }

    private SimulationLockType[] LockRequests() =>
        packetSender.ReceivedCalls().Select(call => call.GetArguments()[0]).OfType<SimulationOwnershipRequest>().Select(request => request.LockType).ToArray();

    private sealed class FakeStalker : StalkerShinyGrabs.IStalker
    {
        public int PickedUp { get; private set; }
        public int Dropped { get; private set; }
        public int Forgotten { get; private set; }
        public bool StillWantsTarget { get; set; } = true;

        public void PickUp() => PickedUp++;

        public void Drop() => Dropped++;

        public void ForgetTarget() => Forgotten++;
    }
}
