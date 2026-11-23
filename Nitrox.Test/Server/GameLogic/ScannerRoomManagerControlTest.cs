using System.Threading.Tasks;
using Nitrox.Model.Core;
using Nitrox.Model.DataStructures;
using Nitrox.Model.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.Packets;
using Nitrox.Server.Subnautica.Models.GameLogic;
using Nitrox.Test.Model.Subnautica;
using Nitrox.Test.Server.Subnautica;
using Packet = Nitrox.Model.Packets.Packet;
using ServerPlayer = Nitrox.Server.Subnautica.Models.Player;

namespace Nitrox.Test.Server.GameLogic;

/// <summary>
///     A player controls a camera with the exclusive simulation lock on it, asked for through the lock request
///     processor like the lock on any entity, and one packet that says so. The same lock is taken by a client whose
///     Stalker carries the camera, without that packet.
/// </summary>
[TestClass]
public sealed class ScannerRoomManagerControlTest
{
    private static readonly NitroxId cameraId = ScannerRoomScenarioFixture.CameraAId;
    private static readonly NitroxId otherCameraId = ScannerRoomScenarioFixture.CameraBId;

    [TestMethod]
    public async Task PlayerTakesACameraWithItsLockAndTheOthersAreToldWhoControlsIt()
    {
        ScannerRoomScenarioFixture scenario = new();

        ScannerRoomScenarioFixture.ControlResult taken = await scenario.ControlAsync(scenario.PlayerA, cameraId, true);

        Assert.IsTrue(taken.Granted);
        AssertLock(scenario, cameraId, scenario.PlayerA, SimulationLockType.EXCLUSIVE);
        MapRoomCameraControlChanged announced = scenario.SentToOthers<MapRoomCameraControlChanged>().Single();
        Assert.AreEqual((cameraId, scenario.PlayerA.SessionId, true), (announced.CameraId, announced.SessionId, announced.IsControlling));
        Assert.IsTrue(taken.Sent.OfType<SimulationOwnershipChange>().Any(change => change.Entities.Any(entity =>
            entity.Id == cameraId && entity.SessionId == scenario.PlayerA.SessionId && entity.LockType == SimulationLockType.EXCLUSIVE)), "The lock is announced like for any entity.");
    }

    [TestMethod]
    public async Task CameraAnotherPlayerControlsIsRefusedByItsLock()
    {
        ScannerRoomScenarioFixture scenario = new();
        Assert.IsTrue((await scenario.ControlAsync(scenario.PlayerA, cameraId, true)).Granted);

        ScannerRoomScenarioFixture.ControlResult refused = await scenario.ControlAsync(scenario.PlayerB, cameraId, true);

        Assert.IsFalse(refused.Granted);
        Assert.IsFalse(((SimulationOwnershipResponse)refused.Sent.Single()).LockAcquired, "The refusal is the ordinary answer to a lock request.");
        Assert.AreEqual(scenario.PlayerA.SessionId, refused.Controller);
        AssertLock(scenario, cameraId, scenario.PlayerA, SimulationLockType.EXCLUSIVE);
    }

    [TestMethod]
    public async Task WordOfTakingACameraIsNoControlUntilTheLockIsGranted()
    {
        ScannerRoomScenarioFixture scenario = new();
        Assert.IsTrue((await scenario.ControlAsync(scenario.PlayerA, cameraId, true)).Granted);

        // B says that it takes the camera, which A controls. Its request for the lock is then refused.
        IReadOnlyList<Packet> said = await scenario.ControlChangedAsync(scenario.PlayerB, cameraId, true);
        Assert.AreEqual(0, said.Count, "Nobody is told of an intent.");
        Assert.IsTrue(scenario.IsController(cameraId, scenario.PlayerA.SessionId));
        Assert.IsFalse((await scenario.RequestOwnershipAsync(scenario.PlayerB, cameraId, SimulationLockType.EXCLUSIVE)).LockAcquired);
        Assert.AreEqual("locked", scenario.Diagnostics.GetHistory()[^1].Reason);

        // The refusal ended the intent. A later lock of B on the camera, for its Stalker, is no control.
        Assert.IsTrue((await scenario.ControlAsync(scenario.PlayerA, cameraId, false)).Granted);
        Assert.IsTrue((await scenario.RequestOwnershipAsync(scenario.PlayerB, cameraId, SimulationLockType.EXCLUSIVE)).LockAcquired);
        Assert.IsFalse(scenario.HasController(cameraId));
    }

    [TestMethod]
    public async Task PlayerWhoWasGrantedACameraControlsItAtOnceAndLosesItToNobodysRequest()
    {
        // The player says what the lock is for before asking for it, so there is no moment in which the lock is
        // theirs and the server takes it for a Stalker's.
        ScannerRoomScenarioFixture scenario = new();
        await scenario.ControlChangedAsync(scenario.PlayerA, cameraId, true);

        SimulationOwnershipResponse granted = await scenario.RequestOwnershipAsync(scenario.PlayerA, cameraId, SimulationLockType.EXCLUSIVE);

        Assert.IsTrue(granted.LockAcquired);
        Assert.IsTrue(scenario.IsController(cameraId, scenario.PlayerA.SessionId), "With the grant, without another word from the player.");
        MapRoomCameraControlChanged announced = scenario.SentToOthers<MapRoomCameraControlChanged>().Single();
        Assert.AreEqual((cameraId, scenario.PlayerA.SessionId, true), (announced.CameraId, announced.SessionId, announced.IsControlling));

        Assert.IsFalse((await scenario.ControlAsync(scenario.PlayerB, cameraId, true)).Granted, "Not to another player who wants to control it.");
        Assert.IsFalse((await scenario.RequestOwnershipAsync(scenario.PlayerB, cameraId, SimulationLockType.EXCLUSIVE)).LockAcquired, "Not to a client whose Stalker wants to carry it.");
        Assert.IsTrue(scenario.IsController(cameraId, scenario.PlayerA.SessionId));
        Assert.AreEqual(0, (await scenario.ControlChangedAsync(scenario.PlayerA, cameraId, true)).Count, "Saying it again changes nothing.");
    }

    [TestMethod]
    public async Task PlayerWhoHoldsTheLockForTheirStalkerControlsTheCameraWhenTheySaySo()
    {
        ScannerRoomScenarioFixture scenario = new();
        Assert.IsTrue((await scenario.RequestOwnershipAsync(scenario.PlayerA, cameraId, SimulationLockType.EXCLUSIVE)).LockAcquired);

        await scenario.ControlChangedAsync(scenario.PlayerA, cameraId, true);

        Assert.IsTrue(scenario.IsController(cameraId, scenario.PlayerA.SessionId));
        Assert.IsTrue(scenario.SentToOthers<MapRoomCameraControlChanged>().Single().IsControlling);
    }

    [TestMethod]
    public async Task CameraInAnInventoryCannotBeControlled()
    {
        ScannerRoomScenarioFixture scenario = new();
        await scenario.PickupAsync(scenario.PlayerA, cameraId);

        // The lock on something that is no entity of the world is simply granted. What it is for is refused.
        ScannerRoomScenarioFixture.ControlResult refused = await scenario.ControlAsync(scenario.PlayerB, cameraId, true);

        Assert.IsFalse(refused.Granted);
        Assert.IsNull(scenario.Ownership.GetPlayerForLock(cameraId), "The lock that was taken for it is not left behind.");
        Assert.IsFalse(scenario.SentTo<MapRoomCameraControlChanged>(scenario.PlayerB).Single().IsControlling);

        await scenario.DropAsync(scenario.PlayerA, cameraId);
        Assert.IsTrue((await scenario.ControlAsync(scenario.PlayerB, cameraId, true)).Granted, "Dropped, it is a camera like any other.");
    }

    [TestMethod]
    public async Task DeadOrEmptyCameraCannotBeControlledByWhatTheServerHoldsForIt()
    {
        // The game's rule (MapRoomCamera.CanBeControlled). The client applies it to its own copy of the camera; the
        // server applies it to the state it holds, for a client whose copy is behind.
        ScannerRoomScenarioFixture scenario = new();
        scenario.SetCameraComponents(cameraId, 50f, 0f);

        ScannerRoomScenarioFixture.ControlResult dead = await scenario.ControlAsync(scenario.PlayerA, cameraId, true);

        Assert.IsFalse(dead.Granted);
        Assert.IsFalse(dead.Sent.OfType<MapRoomCameraControlChanged>().Single().IsControlling);
        AssertLock(scenario, cameraId, scenario.PlayerA, SimulationLockType.TRANSIENT);
        Assert.AreEqual("dead", scenario.Diagnostics.GetHistory()[^1].Reason);

        scenario.SetCameraComponents(cameraId, 0f, 10f);
        scenario.PlayerB.GameMode = SubnauticaGameMode.CREATIVE;
        Assert.IsFalse((await scenario.ControlAsync(scenario.PlayerA, cameraId, true)).Granted);
        Assert.AreEqual("empty", scenario.Diagnostics.GetHistory()[^1].Reason);
        Assert.IsTrue((await scenario.ControlAsync(scenario.PlayerB, cameraId, true)).Granted, "Nothing needs power in Creative.");
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
    public async Task GivingUpControlTellsTheOthersAndLeavesThePlayerSimulatingTheCamera()
    {
        ScannerRoomScenarioFixture scenario = new();
        Assert.IsTrue((await scenario.ControlAsync(scenario.PlayerA, cameraId, true)).Granted);

        ScannerRoomScenarioFixture.ControlResult released = await scenario.ControlAsync(scenario.PlayerA, cameraId, false);

        Assert.IsFalse(scenario.HasController(cameraId));
        MapRoomCameraControlChanged announced = released.Sent.OfType<MapRoomCameraControlChanged>().Single();
        Assert.AreSame(announced, scenario.SentToOthers<MapRoomCameraControlChanged>().Last());
        Assert.AreEqual((cameraId, scenario.PlayerA.SessionId, false), (announced.CameraId, announced.SessionId, announced.IsControlling));
        AssertLock(scenario, cameraId, scenario.PlayerA, SimulationLockType.TRANSIENT);
        Assert.IsTrue((await scenario.ControlAsync(scenario.PlayerB, cameraId, true)).Granted, "The camera is free for the next player.");
    }

    [TestMethod]
    public async Task ReleaseFromAPlayerWhoDoesNotControlTheCameraChangesNothingAndIsPassedOnToNobody()
    {
        ScannerRoomScenarioFixture scenario = new();
        Assert.IsTrue((await scenario.ControlAsync(scenario.PlayerA, cameraId, true)).Granted);
        Assert.IsTrue((await scenario.ControlAsync(scenario.PlayerA, cameraId, false)).Granted);
        Assert.IsTrue((await scenario.ControlAsync(scenario.PlayerB, cameraId, true)).Granted);

        // A's release arrives a second time, after B has taken the camera.
        IReadOnlyList<Packet> sent = await scenario.ControlChangedAsync(scenario.PlayerA, cameraId, false);

        Assert.AreEqual(0, sent.Count);
        Assert.IsTrue(scenario.IsController(cameraId, scenario.PlayerB.SessionId));
        AssertLock(scenario, cameraId, scenario.PlayerB, SimulationLockType.EXCLUSIVE);
    }

    [TestMethod]
    public async Task TakingAnotherCameraEndsControlOfTheOneThePlayerHad()
    {
        // The game lets a player control one camera at a time. The release of the first can still be on its way.
        ScannerRoomScenarioFixture scenario = new();
        Assert.IsTrue((await scenario.DockAsync(scenario.PlayerA, cameraId, 0, true)).Granted);
        Assert.IsTrue((await scenario.ControlAsync(scenario.PlayerA, cameraId, true)).Granted);

        ScannerRoomScenarioFixture.ControlResult next = await scenario.ControlAsync(scenario.PlayerA, otherCameraId, true);

        Assert.IsTrue(next.Granted);
        Assert.IsFalse(scenario.HasController(cameraId));
        MapRoomCameraControlChanged ended = scenario.SentToAll<MapRoomCameraControlChanged>().Single();
        Assert.AreEqual((cameraId, scenario.PlayerA.SessionId, false), (ended.CameraId, ended.SessionId, ended.IsControlling));
        AssertLock(scenario, cameraId, scenario.PlayerA, SimulationLockType.TRANSIENT);
        AssertLock(scenario, otherCameraId, scenario.PlayerA, SimulationLockType.EXCLUSIVE);
        Assert.AreEqual(0, (await scenario.ControlChangedAsync(scenario.PlayerA, cameraId, false)).Count, "The late release of the first camera is nothing new.");
        Assert.IsTrue((await scenario.ControlAsync(scenario.PlayerB, cameraId, true)).Granted, "Another player can take the first camera.");
    }

    [TestMethod]
    public async Task ControlEndsForEveryoneWhenItsPlayerLeaves()
    {
        ScannerRoomScenarioFixture scenario = new();
        Assert.IsTrue((await scenario.ControlAsync(scenario.PlayerA, cameraId, true)).Granted);

        IReadOnlyList<Packet> sent = await scenario.DisconnectAsync(scenario.PlayerA);

        AssertEndedForEveryone(scenario, sent, scenario.PlayerA);
        Assert.IsNull(scenario.Ownership.GetPlayerForLock(cameraId));
        Assert.IsTrue((await scenario.ControlAsync(scenario.PlayerB, cameraId, true)).Granted);
    }

    [TestMethod]
    public async Task ControlEndsForEveryoneWhenTheCameraIsDestroyedOrPickedUp()
    {
        ScannerRoomScenarioFixture scenario = new();
        Assert.IsTrue((await scenario.ControlAsync(scenario.PlayerA, cameraId, true)).Granted);
        Assert.IsTrue((await scenario.ControlAsync(scenario.PlayerB, otherCameraId, true)).Granted);

        List<Packet> destroyed = [.. await scenario.DestroyAsync(scenario.PlayerB, cameraId)];
        List<Packet> pickedUp = [.. await scenario.PickupAsync(scenario.PlayerA, otherCameraId)];

        AssertEndedForEveryone(scenario, destroyed, scenario.PlayerA);
        MapRoomCameraControlChanged ended = pickedUp.OfType<MapRoomCameraControlChanged>().Single();
        Assert.AreEqual((otherCameraId, scenario.PlayerB.SessionId, false), (ended.CameraId, ended.SessionId, ended.IsControlling));
        Assert.IsTrue(pickedUp.IndexOf(ended) < pickedUp.FindIndex(packet => packet is SpawnEntities), "Players hear that control ended before the camera becomes an inventory item.");
        Assert.IsFalse(scenario.HasController(otherCameraId));
        Assert.IsNull(scenario.Ownership.GetPlayerForLock(otherCameraId));
    }

    [TestMethod]
    public async Task DeconstructingARoomLeavesTheControllerOfACameraThatIsOutInTheWorldAlone()
    {
        // Any screen flies any camera of the world. A camera that left its dock is no part of the room it came from;
        // the game of a player whose own screen goes with a room takes them out of the camera's view by itself.
        ScannerRoomScenarioFixture scenario = new();
        Assert.IsTrue((await scenario.DockAsync(scenario.PlayerA, cameraId, 0, true)).Granted);
        Assert.IsTrue((await scenario.DockAsync(scenario.PlayerA, cameraId, 0, false)).Granted);
        Assert.IsTrue((await scenario.ControlAsync(scenario.PlayerB, cameraId, true)).Granted);
        Assert.IsTrue(scenario.EntityRegistry.RemoveEntity(scenario.Room.Id).HasValue);

        IReadOnlyList<Packet> sent = await scenario.CleanupAsync(scenario.Room);

        Assert.AreEqual(0, sent.OfType<MapRoomCameraControlChanged>().Count());
        Assert.IsTrue(scenario.IsController(cameraId, scenario.PlayerB.SessionId));
        AssertLock(scenario, cameraId, scenario.PlayerB, SimulationLockType.EXCLUSIVE);
    }

    [TestMethod]
    public async Task LockHeldForAStalkerGivesWayToAPlayerWhoAsksForTheCamera()
    {
        // The client whose Stalker carries a camera takes the same lock and says nothing about control.
        ScannerRoomScenarioFixture scenario = new();
        Assert.IsTrue((await scenario.RequestOwnershipAsync(scenario.PlayerA, cameraId, SimulationLockType.EXCLUSIVE)).LockAcquired);
        Assert.IsFalse(scenario.HasController(cameraId));

        ScannerRoomScenarioFixture.ControlResult taken = await scenario.ControlAsync(scenario.PlayerB, cameraId, true);

        Assert.IsTrue(taken.Granted, "A Stalker that unloads mid-carry would otherwise keep everyone out of the camera for good.");
        AssertLock(scenario, cameraId, scenario.PlayerB, SimulationLockType.EXCLUSIVE);
        Assert.IsTrue(taken.Sent.OfType<SimulationOwnershipChange>().Any(change => change.Entities.Any(entity => entity.Id == cameraId && entity.SessionId == scenario.PlayerB.SessionId)),
            "The Stalker's client is told like of any change of ownership, and makes the Stalker drop the camera.");
        ScannerRoomDiagnosticEntry recorded = scenario.Diagnostics.GetHistory().Single(entry => entry.EventName == "control_preempt");
        Assert.AreEqual((scenario.PlayerA.SessionId, $"exclusive_lock_taken_by_{scenario.PlayerB.SessionId}"), (recorded.SessionId, recorded.Reason));
    }

    [TestMethod]
    public async Task LockOfAPlayerWhoControlsTheCameraGivesWayToNobody()
    {
        ScannerRoomScenarioFixture scenario = new();
        Assert.IsTrue((await scenario.ControlAsync(scenario.PlayerA, cameraId, true)).Granted);

        // What the client of a Stalker that wants the camera sends.
        SimulationOwnershipResponse answer = await scenario.RequestOwnershipAsync(scenario.PlayerB, cameraId, SimulationLockType.EXCLUSIVE);

        Assert.IsFalse(answer.LockAcquired);
        Assert.IsTrue(scenario.IsController(cameraId, scenario.PlayerA.SessionId));
        AssertLock(scenario, cameraId, scenario.PlayerA, SimulationLockType.EXCLUSIVE);
    }

    [TestMethod]
    public async Task LockHeldForAStalkerEndsWhenItIsGivenBackAndWhenItsPlayerLeaves()
    {
        ScannerRoomScenarioFixture scenario = new();
        Assert.IsTrue((await scenario.RequestOwnershipAsync(scenario.PlayerA, cameraId, SimulationLockType.EXCLUSIVE)).LockAcquired);

        // The Stalker dropped the camera.
        Assert.IsTrue((await scenario.RequestOwnershipAsync(scenario.PlayerA, cameraId, SimulationLockType.TRANSIENT)).LockAcquired);
        AssertLock(scenario, cameraId, scenario.PlayerA, SimulationLockType.TRANSIENT);

        Assert.IsTrue((await scenario.RequestOwnershipAsync(scenario.PlayerA, cameraId, SimulationLockType.EXCLUSIVE)).LockAcquired);
        IReadOnlyList<Packet> sent = await scenario.DisconnectAsync(scenario.PlayerA);

        Assert.IsNull(scenario.Ownership.GetPlayerForLock(cameraId));
        Assert.AreEqual(0, sent.OfType<MapRoomCameraControlChanged>().Count(), "Nobody controlled the camera, so no control ends.");
    }

    [TestMethod]
    public async Task LockHeldForAStalkerGivesWayToNoOtherStalker()
    {
        ScannerRoomScenarioFixture scenario = new();
        Assert.IsTrue((await scenario.RequestOwnershipAsync(scenario.PlayerA, cameraId, SimulationLockType.EXCLUSIVE)).LockAcquired);

        // A request that no word of taking the camera came before: another client's Stalker.
        SimulationOwnershipResponse answer = await scenario.RequestOwnershipAsync(scenario.PlayerB, cameraId, SimulationLockType.EXCLUSIVE);

        Assert.IsFalse(answer.LockAcquired);
        AssertLock(scenario, cameraId, scenario.PlayerA, SimulationLockType.EXCLUSIVE);
        Assert.AreEqual(0, scenario.Diagnostics.GetHistory().Count(entry => entry.EventName == "control_preempt"));
    }

    [TestMethod]
    public async Task JoiningPlayerIsToldWhoControlsWhichCameraAndIsSentItsPicture()
    {
        ScannerRoomScenarioFixture scenario = new();
        byte[] picture = MapRoomCameraPreviewImageTest.CreateJpeg(64, 64);
        Assert.IsTrue((await scenario.DockAsync(scenario.PlayerA, cameraId, 0, true)).Granted);
        Assert.IsTrue((await scenario.ControlAsync(scenario.PlayerA, cameraId, true)).Granted);
        Assert.IsTrue((await scenario.ControlAsync(scenario.PlayerB, otherCameraId, true)).Granted);
        await scenario.PreviewAsync(scenario.PlayerB, new MapRoomCameraPreview(otherCameraId, picture));
        SessionId joiner = (SessionId)(ushort)9;

        IReadOnlyList<Packet> sent = await scenario.JoinedPlayerControlGrantsAsync(joiner);

        CollectionAssert.AreEquivalent(new[] { (cameraId, scenario.PlayerA.SessionId, true), (otherCameraId, scenario.PlayerB.SessionId, true) },
            sent.OfType<MapRoomCameraControlChanged>().Select(packet => (packet.CameraId, packet.SessionId, packet.IsControlling)).ToArray());
        MapRoomCameraPreview preview = sent.OfType<MapRoomCameraPreview>().Single();
        Assert.AreEqual(otherCameraId, preview.CameraId);
        CollectionAssert.AreEqual(picture, preview.JpegBytes);
        Assert.AreEqual(0, scenario.SentTo<MapRoomCameraPreview>(scenario.PlayerA).Count, "Everybody who was there when it was taken has the picture.");
        // The lock itself is in the list of simulated entities every joining player gets.
        SimulatedEntity simulated = scenario.EntitySimulation.AssignGlobalRootEntitiesAndGetData(scenario.PlayerB).Single(entity => entity.Id == cameraId);
        Assert.AreEqual((scenario.PlayerA.SessionId, SimulationLockType.EXCLUSIVE), (simulated.SessionId, simulated.LockType));

        Assert.IsTrue((await scenario.ControlAsync(scenario.PlayerA, cameraId, false)).Granted);
        Assert.AreEqual(otherCameraId, (await scenario.JoinedPlayerControlGrantsAsync(joiner)).OfType<MapRoomCameraControlChanged>().Single().CameraId,
            "A camera nobody controls any more is not announced.");
    }

    [TestMethod]
    public async Task ControllerPublishesOnePictureForEveryTimeTheyTakeTheCamera()
    {
        ScannerRoomScenarioFixture scenario = new();
        byte[] picture = MapRoomCameraPreviewImageTest.CreateJpeg(256, 256);
        Assert.IsTrue((await scenario.ControlAsync(scenario.PlayerA, cameraId, true)).Granted);

        IReadOnlyList<Packet> first = await scenario.PreviewAsync(scenario.PlayerA, new MapRoomCameraPreview(cameraId, picture));
        IReadOnlyList<Packet> second = await scenario.PreviewAsync(scenario.PlayerA, new MapRoomCameraPreview(cameraId, picture));

        MapRoomCameraPreview passedOn = (MapRoomCameraPreview)first.Single();
        Assert.AreSame(passedOn, scenario.SentToOthers<MapRoomCameraPreview>().Single(), "The player who took the picture has it.");
        CollectionAssert.AreEqual(picture, passedOn.JpegBytes);
        Assert.AreEqual(0, second.Count);
        Assert.IsTrue(scenario.Diagnostics.GetHistory()[^1].Reason.StartsWith("already_published", StringComparison.Ordinal));

        Assert.IsTrue((await scenario.ControlAsync(scenario.PlayerA, cameraId, false)).Granted);
        Assert.IsTrue((await scenario.ControlAsync(scenario.PlayerA, cameraId, true)).Granted);
        Assert.AreEqual(1, (await scenario.PreviewAsync(scenario.PlayerA, new MapRoomCameraPreview(cameraId, picture))).Count, "Taking the camera again allows a new picture.");
    }

    [TestMethod]
    public async Task PictureFromAnyoneButTheControllerOrThatIsNoPictureIsDropped()
    {
        ScannerRoomScenarioFixture scenario = new();
        byte[] picture = MapRoomCameraPreviewImageTest.CreateJpeg(64, 64);
        Assert.IsTrue((await scenario.ControlAsync(scenario.PlayerA, cameraId, true)).Granted);
        // Held exclusively for a Stalker, which is no control.
        Assert.IsTrue((await scenario.RequestOwnershipAsync(scenario.PlayerB, otherCameraId, SimulationLockType.EXCLUSIVE)).LockAcquired);

        IReadOnlyList<Packet> fromAnotherPlayer = await scenario.PreviewAsync(scenario.PlayerB, new MapRoomCameraPreview(cameraId, picture));
        IReadOnlyList<Packet> withoutControl = await scenario.PreviewAsync(scenario.PlayerB, new MapRoomCameraPreview(otherCameraId, picture));
        IReadOnlyList<Packet> noPicture = await scenario.PreviewAsync(scenario.PlayerA, new MapRoomCameraPreview(cameraId, [1, 2, 3]));
        IReadOnlyList<Packet> afterNoPicture = await scenario.PreviewAsync(scenario.PlayerA, new MapRoomCameraPreview(cameraId, picture));

        Assert.AreEqual(0, fromAnotherPlayer.Count + withoutControl.Count + noPicture.Count);
        Assert.AreEqual(0, afterNoPicture.Count, "What is no picture uses the one opportunity up, or it would be a way to send without end.");
        Assert.AreEqual(0, (await scenario.JoinedPlayerControlGrantsAsync((SessionId)(ushort)9)).OfType<MapRoomCameraPreview>().Count());
    }

    private static void AssertEndedForEveryone(ScannerRoomScenarioFixture scenario, IReadOnlyList<Packet> sent, ServerPlayer controller)
    {
        MapRoomCameraControlChanged ended = sent.OfType<MapRoomCameraControlChanged>().Single();
        CollectionAssert.Contains(scenario.SentToAll<MapRoomCameraControlChanged>(), ended);
        Assert.AreEqual((cameraId, controller.SessionId, false), (ended.CameraId, ended.SessionId, ended.IsControlling));
        Assert.IsFalse(scenario.HasController(cameraId));
    }

    private static void AssertLock(ScannerRoomScenarioFixture scenario, NitroxId id, ServerPlayer expectedPlayer, SimulationLockType expectedType)
    {
        Assert.IsTrue(scenario.Ownership.TryGetLock(id, out SimulationOwnershipData.PlayerLock heldLock), $"Nobody simulates {id}.");
        Assert.AreSame(expectedPlayer, heldLock.Player);
        Assert.AreEqual(expectedType, heldLock.LockType);
    }
}
