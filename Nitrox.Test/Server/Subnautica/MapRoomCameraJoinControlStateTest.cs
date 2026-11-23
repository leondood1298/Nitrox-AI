using System.Threading.Tasks;
using Nitrox.Model.Core;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;
using Nitrox.Model.Subnautica.Packets;

namespace Nitrox.Test.Server.Subnautica;

/// <summary>
///     Camera control is announced to everyone when it is granted. A player who joins afterwards missed that, so once
///     their initial sync has finished they are sent the grants that are in effect: otherwise a camera somebody is
///     flying stays frozen for them, and their Scanner Room screen offers it.
/// </summary>
[TestClass]
public sealed class MapRoomCameraJoinControlStateTest
{
    private static readonly SessionId joinedSessionId = (SessionId)(ushort)9;

    [TestMethod]
    public async Task JoinedPlayerIsToldWhoControlsADockedAndALooseCamera()
    {
        ScannerRoomScenarioFixture scenario = new();
        Assert.IsTrue((await scenario.DockAsync(scenario.PlayerA, ScannerRoomScenarioFixture.CameraAId, 1, true)).Granted);
        scenario.Room.GetCameraRecord(ScannerRoomScenarioFixture.CameraAId)!.LightOn = true;
        Assert.IsTrue((await scenario.ControlAsync(scenario.PlayerA, ScannerRoomScenarioFixture.CameraAId, 1, true)).Granted);
        Assert.IsTrue(scenario.EntityRegistry.TryGetEntityById(ScannerRoomScenarioFixture.CameraBId, out WorldEntity looseCamera));
        looseCamera.Metadata = new MapRoomCameraMetadata(true, 2);
        Assert.IsTrue((await scenario.ControlLooseAsync(scenario.PlayerB, ScannerRoomScenarioFixture.CameraBId, true)).Granted);

        IReadOnlyList<MapRoomCameraControl> grants = await scenario.JoinedPlayerControlGrantsAsync(joinedSessionId);

        Assert.AreEqual(2, grants.Count);
        Assert.IsTrue(grants.All(grant => grant.IsServerResponse && grant.Granted && grant.IsControlling),
            "The joined player's client handles them like the grants everyone else received.");
        MapRoomCameraControl docked = grants.Single(grant => grant.CameraId == ScannerRoomScenarioFixture.CameraAId);
        Assert.AreEqual(scenario.PlayerA.SessionId, docked.ControllerSessionId);
        Assert.AreEqual(ScannerRoomScenarioFixture.RoomId, docked.MapRoomId.Value);
        Assert.AreEqual(1, docked.CameraIndex);
        Assert.IsTrue(docked.LightOn);
        MapRoomCameraControl loose = grants.Single(grant => grant.CameraId == ScannerRoomScenarioFixture.CameraBId);
        Assert.AreEqual(scenario.PlayerB.SessionId, loose.ControllerSessionId);
        Assert.IsFalse(loose.MapRoomId.HasValue);
        Assert.AreEqual(-1, loose.CameraIndex);
        Assert.IsTrue(loose.LightOn);
    }

    [TestMethod]
    public async Task JoinedPlayerIsToldNothingAboutCamerasThatAreNotControlled()
    {
        ScannerRoomScenarioFixture scenario = new();
        // Released, only simulated, and held for a Stalker: none of these is camera control.
        Assert.IsTrue((await scenario.ControlLooseAsync(scenario.PlayerA, ScannerRoomScenarioFixture.CameraAId, true)).Granted);
        Assert.IsTrue((await scenario.ControlLooseAsync(scenario.PlayerA, ScannerRoomScenarioFixture.CameraAId, false)).Granted);
        Assert.IsTrue(scenario.Ownership.TryAcquire(ScannerRoomScenarioFixture.CameraBId, scenario.PlayerB, SimulationLockType.TRANSIENT));
        Assert.IsTrue((await scenario.RequestOwnershipAsync(scenario.PlayerB, ScannerRoomScenarioFixture.CameraCId, SimulationLockType.EXCLUSIVE)).LockAcquired);

        Assert.AreEqual(0, (await scenario.JoinedPlayerControlGrantsAsync(joinedSessionId)).Count);
    }

    [TestMethod]
    public async Task ControlThatEndedWhenItsPlayerLeftIsNotAnnounced()
    {
        ScannerRoomScenarioFixture scenario = new();
        Assert.IsTrue((await scenario.ControlLooseAsync(scenario.PlayerA, ScannerRoomScenarioFixture.CameraAId, true)).Granted);
        await scenario.DisconnectAsync(scenario.PlayerA);

        Assert.AreEqual(0, (await scenario.JoinedPlayerControlGrantsAsync(joinedSessionId)).Count);
    }
}
