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
///     What the client that simulates a Stalker does about the lock of a camera the Stalker goes for, with a Stalker
///     that needs no game.
/// </summary>
[TestClass]
public sealed class StalkerCameraGrabsTest
{
    private static readonly SessionId localSession = (SessionId)(ushort)1;

    private readonly IPacketSender packetSender = Substitute.For<IPacketSender>();
    private readonly NitroxId cameraId = new();
    private readonly FakeStalker stalker = new();
    private readonly SimulationOwnership simulationOwnership;
    private readonly MapRoomCameras cameras;
    private readonly StalkerCameraGrabs grabs;

    public StalkerCameraGrabsTest()
    {
        IMultiplayerSession multiplayerSession = Substitute.For<IMultiplayerSession>();
        multiplayerSession.Reservation.Returns(new MultiplayerSessionReservation(localSession));
        simulationOwnership = new SimulationOwnership(multiplayerSession, packetSender);
        cameras = new MapRoomCameras(packetSender, multiplayerSession, simulationOwnership, new ScannerRoomClientDiagnostics());
        grabs = new StalkerCameraGrabs(simulationOwnership, cameras);
    }

    [TestMethod]
    public void StalkerWaitsForTheCamerasLockAndPicksTheCameraUpWhenItIsGranted()
    {
        Assert.IsFalse(grabs.MayPickUp(stalker, cameraId));
        CollectionAssert.AreEqual(new[] { SimulationLockType.EXCLUSIVE }, LockRequests());
        Assert.AreEqual(0, stalker.PickedUp);

        simulationOwnership.ReceivedSimulationLockResponse(cameraId, true, SimulationLockType.EXCLUSIVE);

        Assert.AreEqual(1, stalker.PickedUp);
        Assert.IsTrue(grabs.MayPickUp(stalker, cameraId), "With the lock the game is not held back again.");
        Assert.AreEqual(1, LockRequests().Length);
    }

    [TestMethod]
    public void StalkerForgetsACameraAPlayerControls()
    {
        Assert.IsFalse(grabs.MayPickUp(stalker, cameraId));

        // The lock is refused if a player controls the camera with it, and only then.
        simulationOwnership.ReceivedSimulationLockResponse(cameraId, false, SimulationLockType.EXCLUSIVE);

        Assert.AreEqual((0, 1), (stalker.PickedUp, stalker.Forgotten));
        Assert.IsTrue(cameras.CanSelectForControl(cameraId), "A refusal says nothing about who controls the camera.");
    }

    [TestMethod]
    public void LockIsGivenBackIfTheStalkerWentForSomethingElseMeanwhile()
    {
        Assert.IsFalse(grabs.MayPickUp(stalker, cameraId));
        stalker.StillWantsTarget = false;

        simulationOwnership.ReceivedSimulationLockResponse(cameraId, true, SimulationLockType.EXCLUSIVE);

        Assert.AreEqual(0, stalker.PickedUp);
        CollectionAssert.AreEqual(new[] { SimulationLockType.EXCLUSIVE, SimulationLockType.TRANSIENT }, LockRequests());
    }

    [TestMethod]
    public void StalkerThatDropsTheCameraGivesTheLockBack()
    {
        grabs.MayPickUp(stalker, cameraId);
        simulationOwnership.ReceivedSimulationLockResponse(cameraId, true, SimulationLockType.EXCLUSIVE);

        grabs.Dropped(cameraId);
        grabs.Dropped(cameraId);

        CollectionAssert.AreEqual(new[] { SimulationLockType.EXCLUSIVE, SimulationLockType.TRANSIENT }, LockRequests(), "Once: the lock of a camera no Stalker of this client carries is not this class's to give back.");
    }

    [TestMethod]
    public async Task StalkerLetsGoOfTheCameraWhenAPlayerTakesIt()
    {
        grabs.MayPickUp(stalker, cameraId);
        simulationOwnership.ReceivedSimulationLockResponse(cameraId, true, SimulationLockType.EXCLUSIVE);
        SimulationOwnershipChangeProcessor processor = new(simulationOwnership, cameras, grabs, null!);

        // The server gave the camera to a player who asked for it, and says so like for any entity.
        await processor.Process(null!, new SimulationOwnershipChange(cameraId, (SessionId)(ushort)2, SimulationLockType.EXCLUSIVE));

        Assert.AreEqual(1, stalker.Dropped);
        Assert.IsFalse(simulationOwnership.HasAnyLockType(cameraId));
        Assert.AreEqual(1, LockRequests().Length, "There is no lock left to give back.");
    }

    [TestMethod]
    public async Task ChangeOfOwnershipOfSomethingNoStalkerCarriesIsNothingToAStalker()
    {
        simulationOwnership.SimulateEntity(cameraId, SimulationLockType.TRANSIENT);
        SimulationOwnershipChangeProcessor processor = new(simulationOwnership, cameras, grabs, null!);

        await processor.Process(null!, new SimulationOwnershipChange(cameraId, (SessionId)(ushort)2, SimulationLockType.EXCLUSIVE));

        Assert.AreEqual(0, stalker.Dropped);
    }

    private SimulationLockType[] LockRequests() =>
        packetSender.ReceivedCalls().Select(call => call.GetArguments()[0]).OfType<SimulationOwnershipRequest>().Select(request => request.LockType).ToArray();

    private sealed class FakeStalker : StalkerCameraGrabs.IStalker
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
