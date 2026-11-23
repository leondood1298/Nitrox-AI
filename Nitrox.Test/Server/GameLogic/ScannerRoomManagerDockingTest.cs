using System.Threading.Tasks;
using Nitrox.Model.DataStructures;
using Nitrox.Model.DataStructures.Unity;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Bases;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;
using Nitrox.Model.Subnautica.Packets;
using Nitrox.Server.Subnautica.Models.GameLogic;
using Nitrox.Test.Server.Subnautica;
using Packet = Nitrox.Model.Packets.Packet;
using ServerPlayer = Nitrox.Server.Subnautica.Models.Player;

namespace Nitrox.Test.Server.GameLogic;

/// <summary>
///     A docked camera is a child entity of its room. These tests cover how a camera gets into a dock and out of it:
///     reported by its simulating player, built with the room, or removed with it.
/// </summary>
[TestClass]
public sealed class ScannerRoomManagerDockingTest
{
    private static readonly NitroxId cameraA = ScannerRoomScenarioFixture.CameraAId;
    private static readonly NitroxId cameraB = ScannerRoomScenarioFixture.CameraBId;
    private static readonly NitroxId baseId = new("00000000-0000-0000-0000-000000000400");
    private static readonly NitroxId builtRoomId = new("00000000-0000-0000-0000-000000000401");
    private static readonly NitroxId builtCameraLeftId = new("00000000-0000-0000-0000-000000000411");
    private static readonly NitroxId builtCameraRightId = new("00000000-0000-0000-0000-000000000412");

    [TestMethod]
    public async Task DockedCameraIsAChildOfItsRoomAndAnUndockedOneAnEntityOfTheWorld()
    {
        ScannerRoomScenarioFixture scenario = new();
        Entity camera = scenario.EntityRegistry.GetEntityById(cameraA).Value;

        ScannerRoomScenarioFixture.DockResult docked = await scenario.DockAsync(scenario.PlayerA, cameraA, 1, true);

        Assert.IsTrue(docked.Granted);
        Assert.AreEqual(scenario.Room.Id, camera.ParentId);
        Assert.AreSame(camera, scenario.Room.ChildEntities.Single());
        Assert.AreEqual(1, docked.State.DockingIndex);
        Assert.AreSame(camera, scenario.EntityRegistry.GetEntityById(cameraA).Value, "Docking moves the entity, it does not replace it.");
        Assert.IsFalse(scenario.WorldEntityManager.GetGlobalRootEntities(true).Any(entity => entity.Id == cameraA), "A docked camera is saved with its room, not as an entity of its own.");
        MapRoomCameraDocked relayedDock = (MapRoomCameraDocked)docked.Sent.Single();
        Assert.AreSame(relayedDock, scenario.SentToOthers<MapRoomCameraDocked>().Single(), "The player who reported the dock is not told about it.");
        Assert.AreEqual((cameraA, scenario.Room.Id, 1), (relayedDock.CameraId, relayedDock.MapRoomId, relayedDock.DockingIndex));

        ScannerRoomScenarioFixture.DockResult undocked = await scenario.DockAsync(scenario.PlayerA, cameraA, 1, false);

        Assert.IsTrue(undocked.Granted);
        Assert.IsNull(camera.ParentId);
        Assert.AreEqual(0, scenario.Room.ChildEntities.Count);
        Assert.AreEqual(MapRoomCameraMetadata.NOT_DOCKED, undocked.State.DockingIndex);
        Assert.IsTrue(scenario.WorldEntityManager.GetGlobalRootEntities(true).Any(entity => ReferenceEquals(entity, camera)), "An undocked camera is an entity of the world again.");
        MapRoomCameraUndocked relayedUndock = (MapRoomCameraUndocked)undocked.Sent.Single();
        Assert.AreSame(relayedUndock, scenario.SentToOthers<MapRoomCameraUndocked>().Single());
        Assert.AreEqual((cameraA, scenario.Room.Id), (relayedUndock.CameraId, relayedUndock.MapRoomId));
    }

    [TestMethod]
    [DataRow(true, true, true, 0, null)]
    [DataRow(true, true, true, 1, null)]
    [DataRow(true, true, true, 2, "invalid_slot")]
    [DataRow(true, true, true, -1, "invalid_slot")]
    [DataRow(true, false, true, 0, "room_removed")]
    [DataRow(true, false, false, 0, "unknown_room")]
    [DataRow(false, true, true, 0, "unknown_camera")]
    [DataRow(false, false, false, 5, "invalid_slot")]
    public void DockNeedsACameraOutInTheWorldARoomAndOneOfItsTwoDocks(bool isWorldCamera, bool roomExists, bool roomWasKnown, int dockingIndex, string? expectedRefusal)
    {
        Assert.AreEqual(expectedRefusal, ScannerRoomManager.GetDockRefusal(isWorldCamera, roomExists, roomWasKnown, dockingIndex));
    }

    [TestMethod]
    public async Task DockOfAnEntityThatIsNoCameraIsRefused()
    {
        ScannerRoomScenarioFixture scenario = new();
        NitroxId seaglideId = new("00000000-0000-0000-0000-000000000250");
        WorldEntity seaglide = new(NitroxVector3.Zero, NitroxQuaternion.Identity, NitroxVector3.One, new NitroxTechType("Seaglide"), 0, "seaglide", false, seaglideId, null);
        scenario.EntityRegistry.AddEntity(seaglide);

        // The sender simulates the entity, so nothing but its kind stands in the way.
        ScannerRoomScenarioFixture.DockResult refused = await scenario.DockAsync(scenario.PlayerA, seaglideId, 0, true);

        Assert.IsFalse(refused.Granted);
        Assert.IsNull(seaglide.ParentId);
        Assert.AreEqual(0, scenario.Room.ChildEntities.Count);
        Assert.AreEqual(0, refused.Sent.Count, "There is no camera state to correct the sender with.");
        Assert.IsFalse(scenario.Manager.IsScannerCamera(seaglideId), "A refused report must not turn the id into a camera's.");
        AssertLastDiagnostic(scenario, "dock", ScannerRoomDiagnosticOutcome.REJECTED, "unknown_camera");
    }

    [TestMethod]
    [DataRow(-1)]
    [DataRow(2)]
    public async Task DockIntoADockTheRoomDoesNotHaveIsRefused(int dockingIndex)
    {
        ScannerRoomScenarioFixture scenario = new();

        ScannerRoomScenarioFixture.DockResult refused = await scenario.DockAsync(scenario.PlayerA, cameraA, dockingIndex, true);

        Assert.IsFalse(refused.Granted);
        Assert.IsFalse(scenario.IsDocked(cameraA));
        AssertCorrection(scenario, refused, scenario.PlayerA, cameraA, MapRoomCameraMetadata.NOT_DOCKED);
        AssertLastDiagnostic(scenario, "dock", ScannerRoomDiagnosticOutcome.REJECTED, "invalid_slot");
    }

    [TestMethod]
    public async Task DockIntoAnOccupiedDockIsRefused()
    {
        ScannerRoomScenarioFixture scenario = new();
        Assert.IsTrue((await scenario.DockAsync(scenario.PlayerA, cameraA, 0, true)).Granted);

        ScannerRoomScenarioFixture.DockResult refused = await scenario.DockAsync(scenario.PlayerB, cameraB, 0, true);

        Assert.IsFalse(refused.Granted);
        Assert.AreEqual(cameraA, scenario.DockedCamera(0));
        Assert.IsFalse(scenario.IsDocked(cameraB));
        AssertCorrection(scenario, refused, scenario.PlayerB, cameraB, MapRoomCameraMetadata.NOT_DOCKED);
        AssertLastDiagnostic(scenario, "dock", ScannerRoomDiagnosticOutcome.REJECTED, "slot_conflict");
    }

    [TestMethod]
    public async Task DockOfACameraThatSitsInAnotherDockIsRefused()
    {
        ScannerRoomScenarioFixture scenario = new();
        MapRoomEntity otherRoom = scenario.AddRoom(new MapRoomEntity(builtRoomId, baseId, new NitroxInt3(5, -2, 9)));
        Assert.IsTrue((await scenario.DockAsync(scenario.PlayerA, cameraA, 0, true)).Granted);

        ScannerRoomScenarioFixture.DockResult otherDock = await scenario.DockAsync(scenario.PlayerA, cameraA, 1, true);
        ScannerRoomScenarioFixture.DockResult otherRoomDock = await scenario.DockAsync(scenario.PlayerA, cameraA, 0, true, roomId: otherRoom.Id);

        // A camera leaves its dock before it can reach another one; a report that skips that is not from the game.
        Assert.IsFalse(otherDock.Granted);
        Assert.IsFalse(otherRoomDock.Granted);
        Assert.AreEqual(cameraA, scenario.DockedCamera(0));
        Assert.IsNull(scenario.DockedCamera(1));
        Assert.AreEqual(0, otherRoom.ChildEntities.Count);
        AssertCorrection(scenario, otherDock, scenario.PlayerA, cameraA, 0);
        AssertLastDiagnostic(scenario, "dock", ScannerRoomDiagnosticOutcome.REJECTED, "slot_conflict");
    }

    [TestMethod]
    public async Task DockOfACameraInAnInventoryIsRefused()
    {
        ScannerRoomScenarioFixture scenario = new();
        await scenario.PickupAsync(scenario.PlayerA, cameraA);

        // The report of a dock can cross the pickup of the camera on its way to the server.
        ScannerRoomScenarioFixture.DockResult refused = await scenario.DockAsync(scenario.PlayerA, cameraA, 0, true, establishAuthority: false);

        Assert.IsFalse(refused.Granted);
        Assert.AreEqual(0, scenario.Room.ChildEntities.Count);
        Assert.IsInstanceOfType<InventoryItemEntity>(scenario.EntityRegistry.GetEntityById(cameraA).Value);
        AssertLastDiagnostic(scenario, "dock", ScannerRoomDiagnosticOutcome.REJECTED, "unknown_camera");
    }

    [TestMethod]
    public async Task DockIntoARoomThatIsGoneOrNeverWasIsRefused()
    {
        ScannerRoomScenarioFixture scenario = new();

        ScannerRoomScenarioFixture.DockResult unknownRoom = await scenario.DockAsync(scenario.PlayerA, cameraA, 0, true, roomId: new NitroxId("00000000-0000-0000-0000-000000000999"));
        AssertLastDiagnostic(scenario, "dock", ScannerRoomDiagnosticOutcome.REJECTED, "unknown_room");
        Assert.IsTrue(scenario.EntityRegistry.RemoveEntity(scenario.Room.Id).HasValue);
        ScannerRoomScenarioFixture.DockResult removedRoom = await scenario.DockAsync(scenario.PlayerA, cameraA, 0, true);
        AssertLastDiagnostic(scenario, "dock", ScannerRoomDiagnosticOutcome.REJECTED, "room_removed");

        Assert.IsFalse(unknownRoom.Granted);
        Assert.IsFalse(removedRoom.Granted);
        Assert.IsNull(scenario.EntityRegistry.GetEntityById(cameraA).Value.ParentId);
        AssertCorrection(scenario, removedRoom, scenario.PlayerA, cameraA, MapRoomCameraMetadata.NOT_DOCKED);
    }

    [TestMethod]
    public async Task UndockByAPlayerWhoDoesNotSimulateTheCameraIsRefused()
    {
        ScannerRoomScenarioFixture scenario = new();
        Assert.IsTrue((await scenario.DockAsync(scenario.PlayerA, cameraA, 0, true)).Granted);
        // Simulating the room gives no say over the cameras in it.
        Assert.IsTrue(scenario.Ownership.TryAcquire(scenario.Room.Id, scenario.PlayerB, SimulationLockType.TRANSIENT));

        ScannerRoomScenarioFixture.DockResult refused = await scenario.DockAsync(scenario.PlayerB, cameraA, 0, false, establishAuthority: false);

        Assert.IsFalse(refused.Granted);
        Assert.AreEqual(cameraA, scenario.DockedCamera(0));
        AssertCorrection(scenario, refused, scenario.PlayerB, cameraA, 0);
        AssertLastDiagnostic(scenario, "undock", ScannerRoomDiagnosticOutcome.REJECTED, "non_owner");
    }

    [TestMethod]
    public async Task UndockFromARoomTheCameraIsNotInIsRefused()
    {
        ScannerRoomScenarioFixture scenario = new();
        MapRoomEntity otherRoom = scenario.AddRoom(new MapRoomEntity(builtRoomId, baseId, new NitroxInt3(5, -2, 9)));
        Assert.IsTrue((await scenario.DockAsync(scenario.PlayerA, cameraA, 0, true)).Granted);

        ScannerRoomScenarioFixture.DockResult refused = await scenario.DockAsync(scenario.PlayerA, cameraA, 0, false, roomId: otherRoom.Id);

        Assert.IsFalse(refused.Granted);
        Assert.AreEqual(cameraA, scenario.DockedCamera(0));
        AssertCorrection(scenario, refused, scenario.PlayerA, cameraA, 0);
        AssertLastDiagnostic(scenario, "undock", ScannerRoomDiagnosticOutcome.REJECTED, "not_docked_there");
    }

    [TestMethod]
    public async Task ReportOfWhatIsAlreadySoChangesNothingAndIsNotPassedOn()
    {
        ScannerRoomScenarioFixture scenario = new();
        Assert.IsTrue((await scenario.DockAsync(scenario.PlayerA, cameraA, 0, true)).Granted);
        string docked = scenario.Snapshot();

        ScannerRoomScenarioFixture.DockResult ownReplay = await scenario.DockAsync(scenario.PlayerA, cameraA, 0, true);
        ScannerRoomScenarioFixture.DockResult othersReplay = await scenario.DockAsync(scenario.PlayerB, cameraA, 0, true, establishAuthority: false);

        Assert.IsTrue(ownReplay.Granted);
        Assert.IsTrue(othersReplay.Granted, "Nothing changes, so it does not matter who says it.");
        Assert.AreEqual(0, ownReplay.Sent.Count + othersReplay.Sent.Count);
        Assert.AreEqual(docked, scenario.Snapshot());

        Assert.IsTrue((await scenario.DockAsync(scenario.PlayerA, cameraA, 0, false)).Granted);
        Assert.IsTrue((await scenario.DockAsync(scenario.PlayerB, cameraB, 0, true)).Granted);
        string replaced = scenario.Snapshot();

        ScannerRoomScenarioFixture.DockResult lateUndock = await scenario.DockAsync(scenario.PlayerA, cameraA, 0, false);

        Assert.IsTrue(lateUndock.Granted, "The camera is out of the room, which is all an undock says.");
        Assert.AreEqual(0, lateUndock.Sent.Count);
        Assert.AreEqual(replaced, scenario.Snapshot(), "A late undock must not empty the dock another camera has taken.");
        Assert.AreEqual(cameraB, scenario.DockedCamera(0));
        scenario.AssertCanonicalInvariants();
    }

    [TestMethod]
    public async Task ControllerKeepsControlWhileTheCameraLeavesAndEntersItsDock()
    {
        ScannerRoomScenarioFixture scenario = new();
        Assert.IsTrue((await scenario.DockAsync(scenario.PlayerA, cameraA, 0, true)).Granted);
        Assert.IsTrue((await scenario.ControlAsync(scenario.PlayerA, cameraA, 0, true)).Granted);

        ScannerRoomScenarioFixture.DockResult undocked = await scenario.DockAsync(scenario.PlayerA, cameraA, 0, false, establishAuthority: false);
        ScannerRoomScenarioFixture.DockResult docked = await scenario.DockAsync(scenario.PlayerA, cameraA, 1, true, establishAuthority: false);

        Assert.IsTrue(undocked.Granted);
        Assert.IsTrue(docked.Granted);
        Assert.AreEqual(0, undocked.Sent.Concat(docked.Sent).OfType<MapRoomCameraControl>().Count(), "Nobody may be told that control ended.");
        AssertLock(scenario, cameraA, scenario.PlayerA, SimulationLockType.EXCLUSIVE);
        Assert.IsTrue(scenario.IsController(cameraA, scenario.PlayerA.SessionId));
    }

    [TestMethod]
    public async Task DockTurnsAnExclusiveLockThatIsNoCameraControlIntoOrdinarySimulation()
    {
        // A client takes such a lock while its Stalker carries the camera. It ends when the camera is in a dock.
        ScannerRoomScenarioFixture scenario = new();
        Assert.IsTrue((await scenario.RequestOwnershipAsync(scenario.PlayerA, cameraA, SimulationLockType.EXCLUSIVE)).LockAcquired);
        Assert.IsFalse(scenario.IsController(cameraA, scenario.PlayerA.SessionId));

        ScannerRoomScenarioFixture.DockResult docked = await scenario.DockAsync(scenario.PlayerA, cameraA, 0, true, establishAuthority: false);

        Assert.IsTrue(docked.Granted);
        AssertLock(scenario, cameraA, scenario.PlayerA, SimulationLockType.TRANSIENT);
        Assert.IsTrue(docked.Sent.OfType<SimulationOwnershipChange>().Any(change => change.Entities.Any(entity =>
            entity.Id == cameraA && entity.SessionId == scenario.PlayerA.SessionId && entity.LockType == SimulationLockType.TRANSIENT)), "Everyone is told what the lock is now.");
        Assert.AreEqual(0, docked.Sent.OfType<DropSimulationOwnership>().Count(), "The camera still needs its simulating player, who recharges it.");
    }

    [TestMethod]
    [DataRow(1425)]
    [DataRow(2410)]
    public async Task TwoCamerasRacingForOneDockProduceExactlyOneWinner(int seed)
    {
        Random random = new(seed);

        for (int iteration = 0; iteration < 16; iteration++)
        {
            ScannerRoomScenarioFixture scenario = new();
            bool swap = random.Next(2) == 0;
            ServerPlayer firstPlayer = swap ? scenario.PlayerB : scenario.PlayerA;
            ServerPlayer secondPlayer = swap ? scenario.PlayerA : scenario.PlayerB;
            NitroxId firstCamera = swap ? cameraB : cameraA;
            NitroxId secondCamera = swap ? cameraA : cameraB;

            using Barrier start = new(2);
            Task<ScannerRoomScenarioFixture.DockResult> first = Task.Run(async () =>
            {
                start.SignalAndWait();
                return await scenario.DockAsync(firstPlayer, firstCamera, 0, true);
            });
            Task<ScannerRoomScenarioFixture.DockResult> second = Task.Run(async () =>
            {
                start.SignalAndWait();
                return await scenario.DockAsync(secondPlayer, secondCamera, 0, true);
            });

            ScannerRoomScenarioFixture.DockResult[] results = await Task.WhenAll(first, second);

            Assert.AreEqual(1, results.Count(result => result.Granted), $"seed={seed}, iteration={iteration}");
            NitroxId winner = results[0].Granted ? firstCamera : secondCamera;
            Assert.AreEqual(winner, scenario.DockedCamera(0), $"seed={seed}, iteration={iteration}");
            Assert.IsNull(scenario.DockedCamera(1), $"seed={seed}, iteration={iteration}");
            Assert.AreEqual(1, scenario.Room.ChildEntities.Count, $"seed={seed}, iteration={iteration}");
            scenario.AssertCanonicalInvariants();
        }
    }

    [TestMethod]
    [DataRow(1425)]
    [DataRow(2410)]
    [DataRow(2576)]
    [DataRow(1533)]
    public async Task AnySequenceOfDockReportsKeepsEveryCameraInAtMostOneDockAndEveryDockWithAtMostOneCamera(int seed)
    {
        ScannerRoomScenarioFixture scenario = new();
        Random random = new(seed);

        for (int step = 0; step < 96; step++)
        {
            ServerPlayer player = random.Next(2) == 0 ? scenario.PlayerA : scenario.PlayerB;
            NitroxId cameraId = scenario.CameraIds[random.Next(scenario.CameraIds.Count)];
            int slot = random.Next(2);
            bool isDocked = random.Next(2) == 0;
            bool wasDocked = scenario.IsDocked(cameraId);
            NitroxId? occupant = scenario.DockedCamera(slot);

            ScannerRoomScenarioFixture.DockResult result = await scenario.DockAsync(player, cameraId, slot, isDocked);

            if (isDocked)
            {
                bool expected = wasDocked ? occupant == cameraId : occupant == null;
                Assert.AreEqual(expected, result.Granted, $"seed={seed}, step={step}");
            }
            else
            {
                Assert.IsTrue(result.Granted, $"seed={seed}, step={step}");
                Assert.IsFalse(scenario.IsDocked(cameraId), $"seed={seed}, step={step}");
            }
            scenario.AssertCanonicalInvariants();
            CollectionAssert.AreEqual(new[] { 1, 2, 3 }, scenario.CameraIds.Select(id => scenario.CameraState(id).CameraNumber).ToArray(),
                $"A camera keeps its number wherever it goes. seed={seed}, step={step}");
            foreach (NitroxId id in scenario.CameraIds)
            {
                Entity camera = scenario.EntityRegistry.GetEntityById(id).Value;
                Assert.AreEqual(camera.ParentId != null, ((MapRoomCameraMetadata)camera.Metadata).IsDocked, $"seed={seed}, step={step}");
                Assert.AreEqual(camera.ParentId == null, scenario.WorldEntityManager.GetGlobalRootEntities(true).Any(entity => ReferenceEquals(entity, camera)), $"seed={seed}, step={step}");
            }
        }
    }

    [TestMethod]
    public async Task DockThatWaitedForADeconstructionIsRefusedOnceTheRoomIsGone()
    {
        ScannerRoomScenarioFixture scenario = new();
        IDisposable deconstruction = await scenario.Manager.HoldGateAsync();
        Task<ScannerRoomScenarioFixture.DockResult> dock = scenario.DockAsync(scenario.PlayerA, cameraA, 0, true);
        try
        {
            Task first = await Task.WhenAny(dock, Task.Delay(100));
            Assert.AreNotSame(dock, first, "A dock must wait while a deconstruction is being handled.");
            Assert.IsTrue(scenario.EntityRegistry.RemoveEntity(scenario.Room.Id).HasValue);
        }
        finally
        {
            deconstruction.Dispose();
        }

        ScannerRoomScenarioFixture.DockResult refused = await dock.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.IsFalse(refused.Granted);
        Assert.AreEqual(0, scenario.Room.ChildEntities.Count);
        Assert.IsNull(scenario.EntityRegistry.GetEntityById(cameraA).Value.ParentId);
    }

    [TestMethod]
    public async Task BuiltRoomComesWithItsCamerasInTheDocksNumberedByTheServer()
    {
        ScannerRoomScenarioFixture scenario = new();
        // What the builder's client says about the cameras besides their ids does not count.
        MapRoomEntity built = CreateBuiltRoom(
            CreateBuiltCamera(builtCameraLeftId, new MapRoomCameraMetadata(true, 5f, 1f, 77, 0)),
            CreateBuiltCamera(builtCameraRightId, new MapRoomCameraMetadata(dockingIndex: 1)));

        IReadOnlyList<Packet> sent = await scenario.BuildRoomAsync(scenario.PlayerA, built);

        MapRoomEntity room = scenario.EntityRegistry.GetEntityById<MapRoomEntity>(builtRoomId).Value;
        Assert.AreEqual(builtCameraLeftId, scenario.DockedCamera(0, room));
        Assert.AreEqual(builtCameraRightId, scenario.DockedCamera(1, room));
        Assert.AreEqual(new MapRoomCameraMetadata(cameraNumber: 4, dockingIndex: 0).ToString(), scenario.CameraState(builtCameraLeftId).ToString(),
            "A new camera is full, intact and dark, and gets the next number after the three cameras of the world.");
        Assert.AreEqual(5, scenario.CameraState(builtCameraRightId).CameraNumber);
        Assert.IsNull(ScannerRoomStateFingerprint.Validate(room));
        foreach (NitroxId cameraId in new[] { builtCameraLeftId, builtCameraRightId })
        {
            Entity camera = scenario.EntityRegistry.GetEntityById(cameraId).Value;
            Assert.AreEqual(builtRoomId, camera.ParentId);
            Assert.IsTrue(scenario.Manager.IsScannerCamera(cameraId));
            AssertLock(scenario, cameraId, scenario.PlayerA, SimulationLockType.TRANSIENT);
            Assert.IsTrue(sent.OfType<SimulationOwnershipChange>().Any(change => change.Entities.Any(entity => entity.Id == cameraId && entity.SessionId == scenario.PlayerA.SessionId)),
                "Everyone has to learn who simulates the camera: that player recharges it.");
            EntityMetadataUpdate numbered = scenario.SentTo<EntityMetadataUpdate>(scenario.PlayerA).Single(update => update.Id == cameraId);
            Assert.AreEqual(scenario.CameraState(cameraId).CameraNumber, ((MapRoomCameraMetadata)numbered.NewValue).CameraNumber, "The builder made the camera and is told its number.");
        }

        // The other players build the room themselves but cannot make up its cameras: they are sent them.
        MapRoomEntity relayed = (MapRoomEntity)scenario.SentToOthers<UpdateBase>().Single().BuiltPieceEntity;
        Assert.AreNotSame(room, relayed);
        CollectionAssert.AreEqual(new[] { builtCameraLeftId, builtCameraRightId }, ScannerRoomStateFingerprint.GetCameras(relayed).Select(camera => camera.Id).ToArray());
        Assert.AreEqual(4, ScannerRoomStateFingerprint.ReadState(ScannerRoomStateFingerprint.GetCameras(relayed)[0]).CameraNumber);
    }

    [TestMethod]
    public async Task BuiltRoomNeverHasMoreCamerasThanDocksNorTwoInOne()
    {
        ScannerRoomScenarioFixture scenario = new();
        NitroxId extraCameraId = new("00000000-0000-0000-0000-000000000413");
        MapRoomEntity built = CreateBuiltRoom(
            CreateBuiltCamera(builtCameraLeftId, new MapRoomCameraMetadata(dockingIndex: 1)),
            CreateBuiltCamera(builtCameraRightId, new MapRoomCameraMetadata(dockingIndex: 1)),
            CreateBuiltCamera(extraCameraId, new MapRoomCameraMetadata(dockingIndex: 0)));

        await scenario.BuildRoomAsync(scenario.PlayerA, built);

        MapRoomEntity room = scenario.EntityRegistry.GetEntityById<MapRoomEntity>(builtRoomId).Value;
        Assert.AreEqual(builtCameraLeftId, scenario.DockedCamera(1, room));
        Assert.AreEqual(builtCameraRightId, scenario.DockedCamera(0, room), "The dock it asked for is taken, so it gets the free one.");
        Assert.AreEqual(2, room.ChildEntities.Count);
        Assert.IsFalse(scenario.EntityRegistry.GetEntityById(extraCameraId).HasValue, "A room has two docks.");
        Assert.IsNull(ScannerRoomStateFingerprint.Validate(room));
    }

    [TestMethod]
    public async Task BuiltRoomCannotBringAnEntityThatAlreadyExistsAsItsCamera()
    {
        ScannerRoomScenarioFixture scenario = new();
        Assert.IsTrue(scenario.Ownership.TryAcquire(cameraA, scenario.PlayerB, SimulationLockType.TRANSIENT));
        Entity existing = scenario.EntityRegistry.GetEntityById(cameraA).Value;
        MapRoomEntity built = CreateBuiltRoom(CreateBuiltCamera(cameraA, new MapRoomCameraMetadata(dockingIndex: 0)), CreateBuiltCamera(builtCameraRightId, null));

        await scenario.BuildRoomAsync(scenario.PlayerA, built);

        // Otherwise a builder could dock any camera without simulating it, by naming it as one their new room came with.
        MapRoomEntity room = scenario.EntityRegistry.GetEntityById<MapRoomEntity>(builtRoomId).Value;
        Assert.AreSame(existing, scenario.EntityRegistry.GetEntityById(cameraA).Value);
        Assert.IsNull(existing.ParentId);
        Assert.AreEqual(builtCameraRightId, room.ChildEntities.Single().Id);
        Assert.AreEqual(0, scenario.CameraState(builtCameraRightId).DockingIndex);
        AssertLock(scenario, cameraA, scenario.PlayerB, SimulationLockType.TRANSIENT);
    }

    [TestMethod]
    public async Task RoomThatStartsABaseComesWithItsCameras()
    {
        ScannerRoomScenarioFixture scenario = new();
        GhostEntity ghost = GhostEntity.MakeEmpty();
        ghost.Id = baseId;
        ghost.TechType = new NitroxTechType("BaseMapRoom");
        scenario.WorldEntityManager.AddOrUpdateGlobalRootEntity(ghost);
        BuildEntity builtBase = BuildEntity.MakeEmpty();
        builtBase.Id = baseId;
        builtBase.ChildEntities.Add(CreateBuiltRoom(CreateBuiltCamera(builtCameraLeftId, null), CreateBuiltCamera(builtCameraRightId, null)));

        await scenario.PlaceBaseAsync(scenario.PlayerA, new PlaceBase(baseId, builtBase));

        MapRoomEntity room = scenario.EntityRegistry.GetEntityById<MapRoomEntity>(builtRoomId).Value;
        Assert.AreEqual(builtCameraLeftId, scenario.DockedCamera(0, room), "Cameras that name no dock take the docks in their order.");
        Assert.AreEqual(builtCameraRightId, scenario.DockedCamera(1, room));
        AssertLock(scenario, builtCameraLeftId, scenario.PlayerA, SimulationLockType.TRANSIENT);
        AssertLock(scenario, builtCameraRightId, scenario.PlayerA, SimulationLockType.TRANSIENT);
        BuildEntity relayedBase = scenario.SentToOthers<PlaceBase>().Single().BuildEntity;
        MapRoomEntity relayedRoom = relayedBase.ChildEntities.OfType<MapRoomEntity>().Single();
        Assert.AreEqual(baseId, relayedBase.Id);
        Assert.AreEqual(2, ScannerRoomStateFingerprint.GetCameras(relayedRoom).Length);
        Assert.IsNull(ScannerRoomStateFingerprint.Validate(relayedRoom));
    }

    [TestMethod]
    public async Task DeconstructedRoomTakesTheCamerasInItsDocksWithItAndLeavesTheOneOutside()
    {
        // The game destroys a docked camera together with its dock (MapRoomCameraDocking.OnDestroy), and a room that is
        // built again comes with two new cameras. A camera that is out flying is no part of the room.
        ScannerRoomScenarioFixture scenario = new();
        Assert.IsTrue((await scenario.DockAsync(scenario.PlayerA, cameraA, 0, true)).Granted);
        Assert.IsTrue((await scenario.DockAsync(scenario.PlayerA, cameraB, 1, true)).Granted);
        Assert.IsTrue((await scenario.DockAsync(scenario.PlayerA, cameraB, 1, false)).Granted);
        scenario.SetCameraComponents(cameraB, 40f, 300f);
        MapRoomCameraMetadata outsideBefore = scenario.CameraState(cameraB);
        Assert.IsTrue(scenario.EntityRegistry.RemoveEntity(scenario.Room.Id).HasValue);

        IReadOnlyList<Packet> sent = await scenario.CleanupAsync(scenario.Room);

        Assert.IsFalse(scenario.EntityRegistry.GetEntityById(cameraA).HasValue);
        Assert.IsFalse(scenario.WorldEntityManager.GetGlobalRootEntities().Any(entity => entity.Id == cameraA), "A camera that stayed tracked would be handed to every joining player to simulate.");
        Assert.IsNull(scenario.Ownership.GetPlayerForLock(cameraA));
        Assert.AreEqual(cameraA, sent.OfType<EntityDestroyed>().Single().Id);
        Assert.IsTrue(sent.OfType<DropSimulationOwnership>().Any(drop => drop.EntityIds.Contains(cameraA)));
        Assert.IsTrue(scenario.Manager.IsScannerCamera(cameraA), "A request for it that is still on its way is refused as one for a camera.");

        Assert.AreSame(outsideBefore, scenario.CameraState(cameraB), "The camera outside keeps everything.");
        Assert.IsTrue(scenario.WorldEntityManager.GetGlobalRootEntities(true).Any(entity => entity.Id == cameraB));
        AssertLock(scenario, cameraB, scenario.PlayerA, SimulationLockType.TRANSIENT);
        Assert.IsFalse(sent.OfType<DropSimulationOwnership>().Any(drop => drop.EntityIds.Contains(cameraB)));
    }

    [TestMethod]
    public async Task ControlOfADockedCameraEndsBeforeTheCameraGoesWithItsRoom()
    {
        ScannerRoomScenarioFixture scenario = new();
        Assert.IsTrue((await scenario.DockAsync(scenario.PlayerA, cameraA, 0, true)).Granted);
        Assert.IsTrue((await scenario.ControlAsync(scenario.PlayerB, cameraA, 0, true)).Granted);
        Assert.IsTrue(scenario.EntityRegistry.RemoveEntity(scenario.Room.Id).HasValue);

        List<Packet> sent = [.. await scenario.CleanupAsync(scenario.Room)];

        MapRoomCameraControl release = sent.OfType<MapRoomCameraControl>().Single();
        Assert.AreEqual((cameraA, false, scenario.PlayerB.SessionId), (release.CameraId, release.IsControlling, release.ControllerSessionId));
        Assert.IsTrue(sent.IndexOf(release) < sent.FindIndex(packet => packet is EntityDestroyed), "Players hear that control ended before they hear that the camera is gone.");
        Assert.IsFalse(scenario.HasController(cameraA));
        Assert.IsNull(scenario.Ownership.GetPlayerForLock(cameraA));
    }

    [TestMethod]
    public async Task DeconstructedRoomLeavesACameraThatMovedToAnotherRoom()
    {
        ScannerRoomScenarioFixture scenario = new();
        MapRoomEntity otherRoom = scenario.AddRoom(new MapRoomEntity(builtRoomId, baseId, new NitroxInt3(5, -2, 9)));
        Assert.IsTrue((await scenario.DockAsync(scenario.PlayerA, cameraA, 0, true)).Granted);
        Assert.IsTrue((await scenario.DockAsync(scenario.PlayerA, cameraA, 0, false)).Granted);
        Assert.IsTrue((await scenario.DockAsync(scenario.PlayerA, cameraA, 1, true, roomId: otherRoom.Id)).Granted);
        Assert.IsTrue(scenario.EntityRegistry.RemoveEntity(scenario.Room.Id).HasValue);

        IReadOnlyList<Packet> sent = await scenario.CleanupAsync(scenario.Room);

        Assert.AreEqual(cameraA, scenario.DockedCamera(1, otherRoom));
        AssertLock(scenario, cameraA, scenario.PlayerA, SimulationLockType.TRANSIENT);
        Assert.AreEqual(0, sent.OfType<EntityDestroyed>().Count());
        Assert.IsFalse(sent.OfType<DropSimulationOwnership>().Any(drop => drop.EntityIds.Contains(cameraA)));
    }

    [TestMethod]
    public async Task DestroyedDockedCameraLeavesItsRoom()
    {
        ScannerRoomScenarioFixture scenario = new();
        Assert.IsTrue((await scenario.DockAsync(scenario.PlayerA, cameraA, 0, true)).Granted);

        await scenario.DestroyAsync(scenario.PlayerA, cameraA);

        Assert.AreEqual(0, scenario.Room.ChildEntities.Count);
        Assert.IsFalse(scenario.EntityRegistry.GetEntityById(cameraA).HasValue);
        Assert.IsNull(scenario.Ownership.GetPlayerForLock(cameraA));
        Assert.IsTrue((await scenario.DockAsync(scenario.PlayerB, cameraB, 0, true)).Granted, "The dock is free for another camera.");
        scenario.AssertCanonicalInvariants();
    }

    [TestMethod]
    public async Task JoiningPlayerIsToldWhoSimulatesADockedCameraAndTakesOverOneNobodySimulates()
    {
        ScannerRoomScenarioFixture scenario = new();
        Assert.IsTrue((await scenario.DockAsync(scenario.PlayerA, cameraA, 0, true)).Granted);
        Assert.IsTrue((await scenario.DockAsync(scenario.PlayerA, cameraB, 1, true)).Granted);
        // Its simulating player left while alone on the server.
        Assert.IsTrue(scenario.Ownership.RevokeOwnerOfId(cameraB));

        List<SimulatedEntity> joinData = scenario.EntitySimulation.AssignGlobalRootEntitiesAndGetData(scenario.PlayerB);

        Assert.AreEqual(scenario.PlayerA.SessionId, joinData.Single(entity => entity.Id == cameraA).SessionId);
        Assert.AreEqual(scenario.PlayerB.SessionId, joinData.Single(entity => entity.Id == cameraB).SessionId, "A docked camera nobody simulates would never be recharged.");
        AssertLock(scenario, cameraB, scenario.PlayerB, SimulationLockType.TRANSIENT);
    }

    [TestMethod]
    public async Task PlayerGivenARoomToSimulateAlsoGetsItsCamerasThatNobodySimulates()
    {
        ScannerRoomScenarioFixture scenario = new();
        Assert.IsTrue((await scenario.DockAsync(scenario.PlayerA, cameraA, 0, true)).Granted);
        Assert.IsTrue((await scenario.DockAsync(scenario.PlayerA, cameraB, 1, true)).Granted);
        Assert.IsTrue(scenario.Ownership.RevokeOwnerOfId(cameraB));

        List<SimulatedEntity> acquired = [.. scenario.EntitySimulation.TryAcquireEntity(scenario.PlayerB, scenario.Room, true)];

        CollectionAssert.AreEquivalent(new[] { scenario.Room.Id, cameraB }, acquired.Select(entity => entity.Id).ToArray());
        Assert.IsFalse(acquired.Single(entity => entity.Id == cameraB).ChangesPosition, "Only a player who flies a camera moves it.");
        AssertLock(scenario, cameraB, scenario.PlayerB, SimulationLockType.TRANSIENT);
        AssertLock(scenario, cameraA, scenario.PlayerA, SimulationLockType.TRANSIENT);
    }

    private static MapRoomEntity CreateBuiltRoom(params GlobalRootEntity[] cameras) => new(builtRoomId, baseId, new NitroxInt3(2, 0, 2))
    {
        ChildEntities = [.. cameras]
    };

    /// <summary>
    ///     A camera as the builder's client sends it with a new room: a child of the room.
    /// </summary>
    private static GlobalRootEntity CreateBuiltCamera(NitroxId cameraId, MapRoomCameraMetadata? clientState) =>
        ScannerRoomScenarioFixture.CreateCamera(cameraId, builtRoomId, clientState);

    private static void AssertLock(ScannerRoomScenarioFixture scenario, NitroxId cameraId, ServerPlayer expectedPlayer, SimulationLockType expectedType)
    {
        Assert.IsTrue(scenario.Ownership.TryGetLock(cameraId, out SimulationOwnershipData.PlayerLock heldLock), $"Nobody simulates {cameraId}.");
        Assert.AreSame(expectedPlayer, heldLock.Player);
        Assert.AreEqual(expectedType, heldLock.LockType);
    }

    /// <summary>
    ///     A player whose report was refused has already moved the camera in its own game. It is sent what the server
    ///     holds for the camera, and nobody else is sent anything.
    /// </summary>
    private static void AssertCorrection(ScannerRoomScenarioFixture scenario, ScannerRoomScenarioFixture.DockResult refused, ServerPlayer sender, NitroxId cameraId, int expectedDockingIndex)
    {
        EntityMetadataUpdate correction = (EntityMetadataUpdate)refused.Sent.Single();
        CollectionAssert.Contains(scenario.SentTo<EntityMetadataUpdate>(sender), correction);
        Assert.AreEqual(cameraId, correction.Id);
        Assert.AreEqual(expectedDockingIndex, ((MapRoomCameraMetadata)correction.NewValue).DockingIndex);
    }

    private static void AssertLastDiagnostic(ScannerRoomScenarioFixture scenario, string eventName, ScannerRoomDiagnosticOutcome outcome, string reason)
    {
        ScannerRoomDiagnosticEntry entry = scenario.Diagnostics.GetHistory()[^1];
        Assert.AreEqual((eventName, outcome, reason), (entry.EventName, entry.Outcome, entry.Reason));
    }
}
