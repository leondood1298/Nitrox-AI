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
///     What a client knows of who controls which camera, as far as it needs no game.
/// </summary>
[TestClass]
public sealed class MapRoomCamerasTest
{
    private static readonly SessionId localSession = (SessionId)(ushort)1;
    private static readonly SessionId playerB = (SessionId)(ushort)2;
    private static readonly SessionId playerC = (SessionId)(ushort)3;

    private readonly NitroxId cameraId = new();
    private readonly SimulationOwnership simulationOwnership;
    private readonly MapRoomCameras cameras;

    public MapRoomCamerasTest()
    {
        IMultiplayerSession multiplayerSession = Substitute.For<IMultiplayerSession>();
        multiplayerSession.Reservation.Returns(new MultiplayerSessionReservation(localSession));
        simulationOwnership = new SimulationOwnership(multiplayerSession, Substitute.For<IPacketSender>());
        cameras = new MapRoomCameras(Substitute.For<IPacketSender>(), multiplayerSession, simulationOwnership, new ScannerRoomClientDiagnostics());
    }

    [TestMethod]
    public void CameraAnotherPlayerControlsIsOfferedToNobodyUntilThatPlayerLetsGo()
    {
        Assert.IsTrue(cameras.CanSelectForControl(cameraId));

        cameras.ProcessControlChanged(new MapRoomCameraControlChanged(cameraId, playerB, true));
        Assert.IsFalse(cameras.CanSelectForControl(cameraId), "This is also how a player who has just joined learns of it: the server sends the same packet.");

        cameras.ProcessControlChanged(new MapRoomCameraControlChanged(cameraId, playerB, false));
        Assert.IsTrue(cameras.CanSelectForControl(cameraId));
    }

    [TestMethod]
    public void ReleaseOnlyEndsTheControlOfThePlayerItNames()
    {
        // B flew the camera, C took it over. The word that B's control ended arrives after the word of C's control.
        cameras.ProcessControlChanged(new MapRoomCameraControlChanged(cameraId, playerB, true));
        cameras.ProcessControlChanged(new MapRoomCameraControlChanged(cameraId, playerC, true));

        cameras.ProcessControlChanged(new MapRoomCameraControlChanged(cameraId, playerB, false));

        Assert.IsFalse(cameras.CanSelectForControl(cameraId), "C still controls the camera.");
        cameras.ProcessControlChanged(new MapRoomCameraControlChanged(cameraId, playerC, false));
        Assert.IsTrue(cameras.CanSelectForControl(cameraId));
    }

    [TestMethod]
    public void WordAboutThisPlayersOwnControlNeverKeepsTheCameraFromThem()
    {
        // The server refused this player's control, or ended it.
        cameras.ProcessControlChanged(new MapRoomCameraControlChanged(cameraId, localSession, false));
        cameras.ProcessControlChanged(new MapRoomCameraControlChanged(cameraId, localSession, true));

        Assert.IsTrue(cameras.CanSelectForControl(cameraId));
        Assert.IsFalse(cameras.HasLocalControl(cameraId), "Only the game puts the player into a camera's view.");
    }

    [TestMethod]
    public async Task LockThatIsNoControlDoesNotKeepPlayersFromACamera()
    {
        // Another client holds the camera exclusively because its Stalker carries it. Nobody controls the camera, and
        // a player who asks for it gets it. Marked as controlled here, it would stay unselectable for good.
        // No lock is gained here, so no creature infection is rolled
        SimulationOwnershipChangeProcessor processor = new(simulationOwnership, cameras, new StalkerCameraGrabs(simulationOwnership, cameras), null!);

        await processor.Process(null!, new SimulationOwnershipChange(cameraId, playerB, SimulationLockType.EXCLUSIVE));

        Assert.IsTrue(cameras.CanSelectForControl(cameraId));
    }

    [TestMethod]
    public async Task CameraThatWentWithItsRoomIsNobodysAnyMore()
    {
        cameras.ProcessControlChanged(new MapRoomCameraControlChanged(cameraId, playerB, true));
        DropSimulationOwnershipProcessor processor = new(simulationOwnership, cameras);

        await processor.Process(null!, new DropSimulationOwnership([cameraId]));

        Assert.IsTrue(cameras.CanSelectForControl(cameraId));
    }
}
