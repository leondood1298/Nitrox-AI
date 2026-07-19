using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nitrox.Model.Core;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.Packets;
using NitroxClient.Communication.Abstract;
using NitroxClient.Communication.Packets.Processors;
using NitroxClient.Communication.Packets.Processors.Core;
using NitroxClient.GameLogic;
using NSubstitute;

namespace Nitrox.Test.Client.GameLogic;

[TestClass]
public sealed class MapRoomCameraSelectionTest
{
    [DataTestMethod]
    [DataRow(false, false, false, false, true)]
    [DataRow(true, false, false, false, false)]
    [DataRow(true, false, false, true, true)]
    [DataRow(true, false, true, true, false)]
    [DataRow(false, false, true, false, false)]
    [DataRow(false, true, false, false, true)]
    [DataRow(false, true, true, false, true)]
    public void SelectionReflectsControllerState(bool pending, bool local, bool remote, bool active, bool expected)
    {
        Assert.AreEqual(expected, MapRoomCameras.CanSelectForControl(pending, local, remote, active));
    }

    [DataTestMethod]
    [DataRow(0f, 100f, 1f, 1f)]
    [DataRow(99.5f, 100f, 1f, 0.5f)]
    [DataRow(100f, 100f, 1f, 0f)]
    [DataRow(0f, 0f, 1f, 0f)]
    [DataRow(0f, 100f, 0f, 0f)]
    public void DockChargeIsBoundedPerSecond(float current, float capacity, float deltaTime, float expected)
    {
        Assert.AreEqual(expected, MapRoomCameras.CalculateDockCharge(current, capacity, deltaTime), 0.0001f);
    }

    [TestMethod]
    public void PreviewRevisionAcceptsOnlyStrictlyNewPositiveValues()
    {
        long revision = 0;

        Assert.IsFalse(MapRoomCameras.TryAdvancePreviewRevision(ref revision, 0));
        Assert.IsTrue(MapRoomCameras.TryAdvancePreviewRevision(ref revision, 2));
        Assert.AreEqual(2L, revision);
        Assert.IsFalse(MapRoomCameras.TryAdvancePreviewRevision(ref revision, 1));
        Assert.IsFalse(MapRoomCameras.TryAdvancePreviewRevision(ref revision, 2));
        Assert.IsTrue(MapRoomCameras.TryAdvancePreviewRevision(ref revision, 3));
        Assert.AreEqual(3L, revision);
    }

    [TestMethod]
    public async Task OwnershipDropClearsRemoteControlBookkeeping()
    {
        SessionId localSessionId = 1;
        SessionId remoteSessionId = 2;
        NitroxId cameraId = new();
        IMultiplayerSession multiplayerSession = Substitute.For<IMultiplayerSession>();
        multiplayerSession.Reservation.Returns(new MultiplayerSessionReservation(localSessionId));
        SimulationOwnership simulationOwnership = new(multiplayerSession, multiplayerSession);
        StalkerCameraLockPurposeTracker purposeTracker = new();
        MapRoomCameras cameras = new(multiplayerSession, multiplayerSession, null!, simulationOwnership,
            new ScannerRoomClientDiagnostics(), purposeTracker);
        DropSimulationOwnershipProcessor processor = new(simulationOwnership, cameras);

        cameras.ProcessControl(new MapRoomCameraControl(cameraId, Optional.Empty, -1, true, false, true, false, remoteSessionId));
        purposeTracker.RecordAcquisition(cameraId, cameraControlAlreadyGranted: false);
        Assert.IsFalse(cameras.CanSelectForControl(cameraId, false));

        await processor.Process(new ClientProcessorContext(multiplayerSession), new DropSimulationOwnership(cameraId));

        Assert.IsTrue(cameras.CanSelectForControl(cameraId, false));
        Assert.IsFalse(purposeTracker.TryConsumeForDowngrade(cameraId, true, false));
    }

    [TestMethod]
    public async Task GenericOwnershipReassignmentDoesNotEraseCameraControlBookkeeping()
    {
        SessionId localSessionId = 1;
        SessionId remoteSessionId = 2;
        NitroxId cameraId = new();
        IMultiplayerSession multiplayerSession = Substitute.For<IMultiplayerSession>();
        multiplayerSession.Reservation.Returns(new MultiplayerSessionReservation(localSessionId));
        SimulationOwnership simulationOwnership = new(multiplayerSession, multiplayerSession);
        MapRoomCameras cameras = new(multiplayerSession, multiplayerSession, null!, simulationOwnership,
            new ScannerRoomClientDiagnostics(), new StalkerCameraLockPurposeTracker());
        SimulationOwnershipChangeProcessor processor = new(simulationOwnership);

        cameras.ProcessControl(new MapRoomCameraControl(cameraId, Optional.Empty, -1, true, false, true, false, remoteSessionId));
        Assert.IsFalse(cameras.CanSelectForControl(cameraId, false));

        await processor.Process(new ClientProcessorContext(multiplayerSession),
                                new SimulationOwnershipChange(cameraId, localSessionId, SimulationLockType.TRANSIENT));

        Assert.IsFalse(cameras.CanSelectForControl(cameraId, false), "Only the canonical MapRoomCameraControl release may clear remote control state.");
    }

    [TestMethod]
    public void DeniedCameraControlCompletesDeferredStalkerReleaseWhenControllerIsRemote()
    {
        SessionId localSessionId = 1;
        SessionId remoteSessionId = 2;
        NitroxId cameraId = new();
        IMultiplayerSession multiplayerSession = Substitute.For<IMultiplayerSession>();
        multiplayerSession.Reservation.Returns(new MultiplayerSessionReservation(localSessionId));
        SimulationOwnership simulationOwnership = new(multiplayerSession, multiplayerSession);
        simulationOwnership.SimulateEntity(cameraId, SimulationLockType.EXCLUSIVE);
        StalkerCameraLockPurposeTracker purposeTracker = new();
        purposeTracker.RecordAcquisition(cameraId, cameraControlAlreadyGranted: false);
        Assert.IsFalse(purposeTracker.TryConsumeForDowngrade(cameraId, true, cameraControlProtected: true));
        MapRoomCameras cameras = new(multiplayerSession, multiplayerSession, null!, simulationOwnership,
            new ScannerRoomClientDiagnostics(), purposeTracker);

        cameras.ProcessControl(new MapRoomCameraControl(cameraId, Optional.Empty, -1, true, false,
            true, false, remoteSessionId));

        multiplayerSession.Received(1).Send(Arg.Is<SimulationOwnershipRequest>(request =>
            request.Id == cameraId && request.LockType == SimulationLockType.TRANSIENT));
        Assert.IsFalse(purposeTracker.CompleteCameraControlRequest(cameraId, false, true));
    }
}
