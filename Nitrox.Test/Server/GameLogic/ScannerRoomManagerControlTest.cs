using System.Threading.Tasks;
using Nitrox.Model.Core;
using Nitrox.Model.DataStructures;
using Nitrox.Model.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.Packets;
using Nitrox.Server.Subnautica.Models.GameLogic;
using Nitrox.Test.Model.Subnautica;
using Nitrox.Test.Server.Helper;
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

        List<Packet> sent = [.. await scenario.DisconnectAsync(scenario.PlayerA)];

        AssertEndedForEveryone(scenario, sent, scenario.PlayerA);
        Assert.IsTrue(sent.FindIndex(packet => packet is MapRoomCameraControlChanged) < sent.FindIndex(packet => packet is SimulationOwnershipChange),
            "Players hear that control ended before the camera is handed to whoever simulates it next.");
        Assert.IsNull(scenario.Ownership.GetPlayerForLock(cameraId));
        Assert.IsTrue((await scenario.ControlAsync(scenario.PlayerB, cameraId, true)).Granted);
    }

    [TestMethod]
    public async Task ControllerWhoLostTheLockWithoutGivingTheCameraUpIsToldSoBeforeTheNextPlayerIsGranted()
    {
        // Giving a camera up is a word and a request for an ordinary lock. If only the request arrives, the server
        // learns that this player's control is over when another player takes the camera.
        ScannerRoomScenarioFixture scenario = new();
        byte[] picture = MapRoomCameraPreviewImageTest.CreateJpeg(64, 64);
        Assert.IsTrue((await scenario.ControlAsync(scenario.PlayerA, cameraId, true)).Granted);
        Assert.IsTrue((await scenario.RequestOwnershipAsync(scenario.PlayerA, cameraId, SimulationLockType.TRANSIENT)).LockAcquired);
        Assert.IsTrue(scenario.IsController(cameraId, scenario.PlayerA.SessionId), "The lock alone says nothing about control.");

        List<Packet> sent = [.. (await scenario.ControlAsync(scenario.PlayerB, cameraId, true)).Sent];

        MapRoomCameraControlChanged ended = sent.OfType<MapRoomCameraControlChanged>().Single(packet => !packet.IsControlling);
        MapRoomCameraControlChanged granted = sent.OfType<MapRoomCameraControlChanged>().Single(packet => packet.IsControlling);
        Assert.AreEqual((cameraId, scenario.PlayerA.SessionId), (ended.CameraId, ended.SessionId));
        CollectionAssert.Contains(scenario.SentToAll<MapRoomCameraControlChanged>(), ended, "The player who lost the camera is told as well.");
        Assert.AreEqual((cameraId, scenario.PlayerB.SessionId), (granted.CameraId, granted.SessionId));
        Assert.IsTrue(sent.IndexOf(ended) < sent.IndexOf(granted), "Players hear that the old control ended before they hear of the new one.");
        Assert.IsTrue(scenario.IsController(cameraId, scenario.PlayerB.SessionId));

        Assert.AreEqual(0, (await scenario.PreviewAsync(scenario.PlayerA, new MapRoomCameraPreview(cameraId, picture))).Count, "The picture of the player the camera was taken from comes too late.");
        Assert.IsTrue(scenario.Diagnostics.GetHistory()[^1].Reason.StartsWith("not_controller", StringComparison.Ordinal));
        Assert.AreEqual(1, (await scenario.PreviewAsync(scenario.PlayerB, new MapRoomCameraPreview(cameraId, picture))).Count, "The new controller has a picture of their own to publish.");
    }

    [TestMethod]
    public async Task PlayersHearThatControlEndedBeforeTheyHearThatTheCameraIsGone()
    {
        ConnectedPlayers players = new();
        ScannerRoomScenarioFixture scenario = new(connectedPlayers: players);
        Assert.IsTrue((await scenario.ControlAsync(scenario.PlayerA, cameraId, true)).Granted);

        // Reported by another player, whose game saw the camera die.
        List<Packet> sent = [.. await scenario.DestroyAsync(scenario.PlayerB, cameraId)];

        MapRoomCameraControlChanged ended = sent.OfType<MapRoomCameraControlChanged>().Single();
        EntityDestroyed destroyed = sent.OfType<EntityDestroyed>().Single();
        Assert.AreEqual((cameraId, scenario.PlayerA.SessionId, false), (ended.CameraId, ended.SessionId, ended.IsControlling));
        CollectionAssert.Contains(scenario.SentTo<EntityDestroyed>(scenario.PlayerA), destroyed);
        Assert.IsTrue(sent.IndexOf(ended) < sent.IndexOf(destroyed), "The controller's game needs the camera to take its player out of the camera's view.");
    }

    [TestMethod]
    public async Task CameraThatIsGoneCannotBeControlledAndNoLockOnItIsLeftBehind()
    {
        // A request can cross the camera's destruction on its way to the server. The lock on something that is no
        // entity any more is simply granted; what it is for is refused.
        ScannerRoomScenarioFixture scenario = new();
        await scenario.DestroyAsync(scenario.PlayerB, cameraId);

        ScannerRoomScenarioFixture.ControlResult refused = await scenario.ControlAsync(scenario.PlayerA, cameraId, true);

        Assert.IsFalse(refused.Granted);
        Assert.IsFalse(refused.Sent.OfType<MapRoomCameraControlChanged>().Single().IsControlling, "The player's game shows the camera's view by then and is told to leave it.");
        Assert.IsNull(scenario.Ownership.GetPlayerForLock(cameraId));
        Assert.AreEqual("invalid_assoc", scenario.Diagnostics.GetHistory()[^1].Reason);
    }

    [TestMethod]
    [DataRow(1533)]
    [DataRow(2576)]
    public async Task TwoPlayersRacingForOneCameraProduceExactlyOneController(int seed)
    {
        Random random = new(seed);

        for (int iteration = 0; iteration < 16; iteration++)
        {
            ScannerRoomScenarioFixture scenario = new();
            bool swap = random.Next(2) == 0;
            ServerPlayer firstPlayer = swap ? scenario.PlayerB : scenario.PlayerA;
            ServerPlayer secondPlayer = swap ? scenario.PlayerA : scenario.PlayerB;

            using Barrier start = new(2);
            Task<ScannerRoomScenarioFixture.ControlResult> first = Task.Run(async () =>
            {
                start.SignalAndWait();
                return await scenario.ControlAsync(firstPlayer, cameraId, true);
            });
            Task<ScannerRoomScenarioFixture.ControlResult> second = Task.Run(async () =>
            {
                start.SignalAndWait();
                return await scenario.ControlAsync(secondPlayer, cameraId, true);
            });

            ScannerRoomScenarioFixture.ControlResult[] results = await Task.WhenAll(first, second);

            Assert.AreEqual(1, results.Count(result => result.Granted), $"seed={seed}, iteration={iteration}");
            ServerPlayer winner = results[0].Granted ? firstPlayer : secondPlayer;
            Assert.IsTrue(scenario.IsController(cameraId, winner.SessionId), $"seed={seed}, iteration={iteration}");
            AssertLock(scenario, cameraId, winner, SimulationLockType.EXCLUSIVE);
            Assert.AreEqual(winner.SessionId, scenario.Sent<MapRoomCameraControlChanged>().Single().SessionId, $"Nobody is told of the player who lost. seed={seed}, iteration={iteration}");
        }
    }

    [TestMethod]
    public async Task ControlAskedForWhileARoomIsDeconstructedWaitsAndIsRefusedForACameraThatWentWithTheRoom()
    {
        ScannerRoomScenarioFixture scenario = new();
        Assert.IsTrue((await scenario.DockAsync(scenario.PlayerA, cameraId, 0, true)).Granted);

        ScannerRoomScenarioFixture.ControlResult decided = await ControlWhileTheRoomIsDeconstructedAsync(scenario, scenario.PlayerB);

        Assert.IsFalse(decided.Granted, "The cameras in a room's docks go with the room.");
        Assert.IsFalse(decided.Sent.OfType<MapRoomCameraControlChanged>().Single().IsControlling);
        Assert.IsNull(scenario.Ownership.GetPlayerForLock(cameraId), "The lock that was taken for it is not left behind.");
    }

    [TestMethod]
    public async Task ControlAskedForWhileARoomIsDeconstructedWaitsAndIsGrantedForACameraThatIsOutInTheWorld()
    {
        ScannerRoomScenarioFixture scenario = new();
        Assert.IsTrue((await scenario.DockAsync(scenario.PlayerA, cameraId, 0, true)).Granted);
        Assert.IsTrue((await scenario.DockAsync(scenario.PlayerA, cameraId, 0, false)).Granted);

        ScannerRoomScenarioFixture.ControlResult decided = await ControlWhileTheRoomIsDeconstructedAsync(scenario, scenario.PlayerB);

        Assert.IsTrue(decided.Granted, "A camera that left its dock outlives the room.");
        AssertLock(scenario, cameraId, scenario.PlayerB, SimulationLockType.EXCLUSIVE);
        Assert.IsTrue(scenario.SentToOthers<MapRoomCameraControlChanged>().Single().IsControlling);
    }

    [TestMethod]
    public async Task LockAskedForOnACameraWaitsUntilTheManagerHasFinishedWhatItIsHandling()
    {
        // What the client of a Stalker sends is put in order with everything else that happens to the camera: a lock
        // that was granted in the middle of a pickup or a destruction would be a lock on a camera that is gone.
        ScannerRoomScenarioFixture scenario = new();
        IDisposable handling = await scenario.Manager.HoldGateAsync();
        Task<SimulationOwnershipResponse> request = scenario.RequestOwnershipAsync(scenario.PlayerA, cameraId, SimulationLockType.EXCLUSIVE);
        try
        {
            Task first = await Task.WhenAny(request, Task.Delay(100));
            Assert.AreNotSame(request, first, "The request must wait.");
            Assert.IsNull(scenario.Ownership.GetPlayerForLock(cameraId));
        }
        finally
        {
            handling.Dispose();
        }

        Assert.IsTrue((await request.WaitAsync(TimeSpan.FromSeconds(5))).LockAcquired);
        AssertLock(scenario, cameraId, scenario.PlayerA, SimulationLockType.EXCLUSIVE);
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

    [TestMethod]
    public async Task PictureFromAnotherPlayerDoesNotUseUpTheControllersOwn()
    {
        ScannerRoomScenarioFixture scenario = new();
        byte[] picture = MapRoomCameraPreviewImageTest.CreateJpeg(64, 64);
        Assert.IsTrue((await scenario.ControlAsync(scenario.PlayerA, cameraId, true)).Granted);

        IReadOnlyList<Packet> fromAnotherPlayer = await scenario.PreviewAsync(scenario.PlayerB, new MapRoomCameraPreview(cameraId, picture));
        IReadOnlyList<Packet> fromTheController = await scenario.PreviewAsync(scenario.PlayerA, new MapRoomCameraPreview(cameraId, picture));

        Assert.AreEqual(0, fromAnotherPlayer.Count);
        Assert.AreSame(fromTheController.Single(), scenario.SentToOthers<MapRoomCameraPreview>().Single(), "Otherwise any player could keep a camera's picture from everyone.");
    }

    [TestMethod]
    public async Task PictureFromAPlayerWhoGaveTheCameraUpOrLeftIsDropped()
    {
        ScannerRoomScenarioFixture scenario = new();
        byte[] picture = MapRoomCameraPreviewImageTest.CreateJpeg(64, 64);
        Assert.IsTrue((await scenario.ControlAsync(scenario.PlayerA, cameraId, true)).Granted);
        Assert.IsTrue((await scenario.ControlAsync(scenario.PlayerB, otherCameraId, true)).Granted);

        // Both pictures were on their way when control ended.
        Assert.IsTrue((await scenario.ControlAsync(scenario.PlayerA, cameraId, false)).Granted);
        await scenario.DisconnectAsync(scenario.PlayerB);
        IReadOnlyList<Packet> afterGivingUp = await scenario.PreviewAsync(scenario.PlayerA, new MapRoomCameraPreview(cameraId, picture));
        IReadOnlyList<Packet> afterLeaving = await scenario.PreviewAsync(scenario.PlayerB, new MapRoomCameraPreview(otherCameraId, picture));

        Assert.AreEqual(0, afterGivingUp.Count, "The player goes on simulating the camera, which is no control.");
        Assert.AreEqual(0, afterLeaving.Count);
        Assert.AreEqual(0, scenario.Sent<MapRoomCameraPreview>().Count);
    }

    [TestMethod]
    public async Task SayingAgainThatTheyControlTheCameraGivesAPlayerNoSecondPicture()
    {
        ScannerRoomScenarioFixture scenario = new();
        byte[] picture = MapRoomCameraPreviewImageTest.CreateJpeg(64, 64);
        Assert.IsTrue((await scenario.ControlAsync(scenario.PlayerA, cameraId, true)).Granted);
        Assert.AreEqual(1, (await scenario.PreviewAsync(scenario.PlayerA, new MapRoomCameraPreview(cameraId, picture))).Count);

        // Everything a client sends to take a camera, a second time: the word and the request for the lock.
        ScannerRoomScenarioFixture.ControlResult repeated = await scenario.ControlAsync(scenario.PlayerA, cameraId, true);
        IReadOnlyList<Packet> second = await scenario.PreviewAsync(scenario.PlayerA, new MapRoomCameraPreview(cameraId, picture));

        Assert.IsTrue(repeated.Granted);
        Assert.AreEqual(0, repeated.Sent.OfType<MapRoomCameraControlChanged>().Count(), "Nobody is told twice.");
        Assert.AreEqual(0, second.Count, "Only a camera that was given up and taken again is taken anew.");
    }

    [TestMethod]
    public async Task RefreshOfWhatAPlayerSimulatesLeavesTheirCameraAndItsOnePictureWithThem()
    {
        // The server asks for an ordinary lock on everything a player can simulate whenever cells come into their
        // view. That must not turn the lock of the camera they are flying into an ordinary one.
        ScannerRoomScenarioFixture scenario = new();
        Assert.IsTrue((await scenario.ControlAsync(scenario.PlayerA, cameraId, true)).Granted);
        Entity camera = scenario.EntityRegistry.GetEntityById(cameraId).Value;

        SimulatedEntity refreshed = scenario.EntitySimulation.TryAcquireEntity(scenario.PlayerA, camera, propagate: true).Single();

        Assert.AreEqual(SimulationLockType.EXCLUSIVE, refreshed.LockType, "Players are told the lock that is held.");
        AssertLock(scenario, cameraId, scenario.PlayerA, SimulationLockType.EXCLUSIVE);
        Assert.IsTrue(scenario.IsController(cameraId, scenario.PlayerA.SessionId));
        Assert.AreEqual(1, (await scenario.PreviewAsync(scenario.PlayerA, new MapRoomCameraPreview(cameraId, MapRoomCameraPreviewImageTest.CreateJpeg(64, 64)))).Count);
    }

    [TestMethod]
    public async Task JoiningPlayerIsSentNothingForACameraWhoseControlHasEnded()
    {
        ScannerRoomScenarioFixture scenario = new();
        byte[] picture = MapRoomCameraPreviewImageTest.CreateJpeg(64, 64);
        SessionId joiner = (SessionId)(ushort)9;
        Assert.IsTrue((await scenario.ControlAsync(scenario.PlayerA, cameraId, true)).Granted);
        Assert.IsTrue((await scenario.ControlAsync(scenario.PlayerB, otherCameraId, true)).Granted);
        await scenario.PreviewAsync(scenario.PlayerA, new MapRoomCameraPreview(cameraId, picture));
        await scenario.PreviewAsync(scenario.PlayerB, new MapRoomCameraPreview(otherCameraId, picture));

        Assert.IsTrue((await scenario.ControlAsync(scenario.PlayerA, cameraId, false)).Granted);
        await scenario.DisconnectAsync(scenario.PlayerB);

        Assert.AreEqual(0, (await scenario.JoinedPlayerControlGrantsAsync(joiner)).Count, "Neither a controller nor the picture they took.");

        Assert.IsTrue((await scenario.ControlAsync(scenario.PlayerA, cameraId, true)).Granted);
        MapRoomCameraControlChanged announced = (MapRoomCameraControlChanged)(await scenario.JoinedPlayerControlGrantsAsync(joiner)).Single();
        Assert.AreEqual((cameraId, scenario.PlayerA.SessionId, true), (announced.CameraId, announced.SessionId, announced.IsControlling),
            "A camera that is taken again comes without the picture of the time before.");
    }

    /// <summary>
    ///     Has a player take the fixture's first camera while the manager is busy with the deconstruction of the room,
    ///     which the test stands in for: the request has to wait, and is decided once the room is gone.
    /// </summary>
    private static async Task<ScannerRoomScenarioFixture.ControlResult> ControlWhileTheRoomIsDeconstructedAsync(ScannerRoomScenarioFixture scenario, ServerPlayer player)
    {
        IDisposable deconstruction = await scenario.Manager.HoldGateAsync();
        Task<ScannerRoomScenarioFixture.ControlResult> control = scenario.ControlAsync(player, cameraId, true);
        try
        {
            Task first = await Task.WhenAny(control, Task.Delay(100));
            Assert.AreNotSame(control, first, "Whoever asks while the manager is handling something else must wait for it.");
            Assert.IsFalse(scenario.HasController(cameraId));
            // A deconstructed room leaves the entity registry together with the cameras in its docks.
            Assert.IsTrue(scenario.EntityRegistry.RemoveEntity(scenario.Room.Id).HasValue);
        }
        finally
        {
            deconstruction.Dispose();
        }
        return await control.WaitAsync(TimeSpan.FromSeconds(5));
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
