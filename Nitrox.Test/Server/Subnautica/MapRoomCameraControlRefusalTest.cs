using System.Threading.Tasks;
using Nitrox.Model.DataStructures;
using Nitrox.Model.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Bases;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;
using Nitrox.Model.Subnautica.Packets;
using Nitrox.Server.Subnautica.Models.GameLogic;
using Packet = Nitrox.Model.Packets.Packet;

namespace Nitrox.Test.Server.Subnautica;

/// <summary>
///     The server grants camera control, so it has to refuse what the game itself would not allow
///     (MapRoomCamera.CanBeControlled): a camera somebody carries, a dead one, one with an empty battery, and a second
///     camera for a player who is already controlling one.
/// </summary>
[TestClass]
public sealed class MapRoomCameraControlRefusalTest
{
    private static readonly NitroxId cameraId = ScannerRoomScenarioFixture.CameraAId;
    private static readonly NitroxId otherCameraId = ScannerRoomScenarioFixture.CameraBId;

    [TestMethod]
    public async Task CameraInAnInventoryCannotBeControlled()
    {
        // Picking a camera up keeps its room record, which alone used to be enough to control it.
        ScannerRoomScenarioFixture scenario = new();
        Assert.IsTrue((await scenario.DockAsync(scenario.PlayerA, cameraId, 0, true)).Granted);
        await scenario.PickupAsync(scenario.PlayerB, cameraId);
        Assert.IsNotNull(scenario.Room.GetCameraRecord(cameraId));

        MapRoomCameraControl response = await scenario.ControlLooseAsync(scenario.PlayerA, cameraId, true);

        Assert.IsFalse(response.Granted);
        Assert.IsNull(scenario.Ownership.GetPlayerForLock(cameraId));
    }

    [TestMethod]
    public async Task DroppedCameraCanBeControlledAgain()
    {
        ScannerRoomScenarioFixture scenario = new();
        Assert.IsTrue((await scenario.DockAsync(scenario.PlayerA, cameraId, 0, true)).Granted);
        await scenario.PickupAsync(scenario.PlayerB, cameraId);
        await scenario.DropAsync(scenario.PlayerB, cameraId);

        Assert.IsTrue((await scenario.ControlLooseAsync(scenario.PlayerA, cameraId, true)).Granted);
    }

    [TestMethod]
    public async Task DeadCameraCannotBeControlledUntilItIsRepaired()
    {
        ScannerRoomScenarioFixture scenario = new();
        Assert.IsTrue((await scenario.DockAsync(scenario.PlayerA, cameraId, 0, true)).Granted);
        MapRoomCameraRecord record = scenario.Room.GetCameraRecord(cameraId)!;
        record.Health = 0f;

        Assert.IsFalse((await scenario.ControlAsync(scenario.PlayerA, cameraId, 0, true)).Granted);
        Assert.IsNull(scenario.Ownership.GetPlayerForLock(cameraId));

        record.Health = 25f;
        Assert.IsTrue((await scenario.ControlAsync(scenario.PlayerA, cameraId, 0, true)).Granted);
    }

    [TestMethod]
    public async Task EmptyCameraCannotBeControlledWhereTheGameNeedsPower()
    {
        ScannerRoomScenarioFixture scenario = new();
        Assert.IsTrue((await scenario.DockAsync(scenario.PlayerA, cameraId, 0, true)).Granted);
        scenario.Room.GetCameraRecord(cameraId)!.Energy = 0f;
        scenario.PlayerB.GameMode = SubnauticaGameMode.CREATIVE;

        Assert.IsFalse((await scenario.ControlAsync(scenario.PlayerA, cameraId, 0, true)).Granted);
        Assert.IsTrue((await scenario.ControlAsync(scenario.PlayerB, cameraId, 0, true)).Granted, "Nothing needs power in Creative.");
    }

    [TestMethod]
    public async Task CameraWithoutARoomIsJudgedByItsOwnState()
    {
        ScannerRoomScenarioFixture scenario = new();
        Assert.IsTrue(scenario.EntityRegistry.TryGetEntityById(cameraId, out WorldEntity camera));
        camera.Metadata = new MapRoomCameraMetadata(false, 0, 0f, MapRoomCameraRecord.MAX_HEALTH, 3);

        Assert.IsFalse((await scenario.ControlLooseAsync(scenario.PlayerA, cameraId, true)).Granted);

        camera.Metadata = new MapRoomCameraMetadata(false, 0, 1f, MapRoomCameraRecord.MAX_HEALTH, 4);
        Assert.IsTrue((await scenario.ControlLooseAsync(scenario.PlayerA, cameraId, true)).Granted);
    }

    [TestMethod]
    [DataRow(50f, 100f, SubnauticaGameMode.SURVIVAL, null)]
    [DataRow(50f, 0f, SubnauticaGameMode.SURVIVAL, "dead")]
    [DataRow(50f, 0f, SubnauticaGameMode.CREATIVE, "dead")]
    [DataRow(0f, 100f, SubnauticaGameMode.SURVIVAL, "empty")]
    [DataRow(0f, 100f, SubnauticaGameMode.HARDCORE, "empty")]
    [DataRow(0f, 100f, SubnauticaGameMode.FREEDOM, "empty")]
    [DataRow(0f, 100f, SubnauticaGameMode.CREATIVE, null)]
    public void GameRuleForAControllableCamera(float energy, float health, SubnauticaGameMode gameMode, string? expectedRefusal)
    {
        Assert.AreEqual(expectedRefusal, ScannerRoomManager.GetUnusableReason(energy, health, gameMode));
    }

    [TestMethod]
    public async Task TakingAnotherCameraEndsControlOfTheLooseOneThePlayerHad()
    {
        ScannerRoomScenarioFixture scenario = new();
        Assert.IsTrue((await scenario.ControlLooseAsync(scenario.PlayerA, cameraId, true)).Granted);

        IReadOnlyList<Packet> sent = await scenario.ControlPacketsAsync(scenario.PlayerA,
            new MapRoomCameraControl(otherCameraId, Optional.Empty, -1, true, false));

        Assert.IsTrue(sent.OfType<MapRoomCameraControl>().Single(packet => packet.CameraId == otherCameraId).Granted);
        MapRoomCameraControl release = sent.OfType<MapRoomCameraControl>().Single(packet => packet.CameraId == cameraId);
        Assert.IsFalse(release.IsControlling, "Everyone is told that the first camera is free again.");
        Assert.AreEqual(scenario.PlayerA.SessionId, release.ControllerSessionId);
        Assert.IsFalse(scenario.IsController(cameraId, scenario.PlayerA.SessionId));
        Assert.IsTrue(scenario.IsController(otherCameraId, scenario.PlayerA.SessionId));
        Assert.IsTrue(scenario.Ownership.TryGetLock(cameraId, out SimulationOwnershipData.PlayerLock firstCameraLock));
        Assert.AreEqual(SimulationLockType.TRANSIENT, firstCameraLock.LockType, "A camera out in the world keeps a simulating player.");
        Assert.IsTrue((await scenario.ControlLooseAsync(scenario.PlayerB, cameraId, true)).Granted, "Another player can take the first camera.");
    }

    [TestMethod]
    public async Task TakingAnotherCameraEndsControlOfTheDockedOneThePlayerHad()
    {
        ScannerRoomScenarioFixture scenario = new();
        Assert.IsTrue((await scenario.DockAsync(scenario.PlayerA, cameraId, 0, true)).Granted);
        Assert.IsTrue((await scenario.ControlAsync(scenario.PlayerA, cameraId, 0, true)).Granted);

        Assert.IsTrue((await scenario.ControlLooseAsync(scenario.PlayerA, otherCameraId, true)).Granted);

        Assert.IsNull(scenario.Ownership.GetPlayerForLock(cameraId));
        Assert.IsFalse(scenario.HasController(cameraId));
    }

    [TestMethod]
    public async Task LateReleaseOfThePreviousCameraIsStillAcknowledged()
    {
        // The client's own release of the first camera can be handled after its request for the second one.
        ScannerRoomScenarioFixture scenario = new();
        Assert.IsTrue((await scenario.ControlLooseAsync(scenario.PlayerA, cameraId, true)).Granted);
        Assert.IsTrue((await scenario.ControlLooseAsync(scenario.PlayerA, otherCameraId, true)).Granted);

        MapRoomCameraControl lateRelease = await scenario.ControlLooseAsync(scenario.PlayerA, cameraId, false);

        Assert.IsTrue(lateRelease.Granted);
        Assert.IsTrue(scenario.IsController(otherCameraId, scenario.PlayerA.SessionId), "The second camera is untouched.");
    }

    [TestMethod]
    public async Task RefusedRequestForAnotherCameraLeavesThePlayersCurrentControlAlone()
    {
        ScannerRoomScenarioFixture scenario = new();
        Assert.IsTrue((await scenario.ControlLooseAsync(scenario.PlayerA, cameraId, true)).Granted);
        Assert.IsTrue((await scenario.ControlLooseAsync(scenario.PlayerB, otherCameraId, true)).Granted);

        Assert.IsFalse((await scenario.ControlLooseAsync(scenario.PlayerA, otherCameraId, true)).Granted);

        Assert.IsTrue(scenario.IsController(cameraId, scenario.PlayerA.SessionId));
    }
}
