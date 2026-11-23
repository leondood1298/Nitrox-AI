using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Nitrox.Model.DataStructures;
using Nitrox.Model.DataStructures.Unity;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Bases;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;
using Nitrox.Model.Subnautica.Packets;
using Nitrox.Server.Subnautica.Models.GameLogic;
using Nitrox.Server.Subnautica.Models.GameLogic.Entities;
using Nitrox.Server.Subnautica.Models.Packets.Core;
using Nitrox.Server.Subnautica.Models.Serialization;
using ServerPlayer = Nitrox.Server.Subnautica.Models.Player;

namespace Nitrox.Test.Server.GameLogic;

/// <summary>
///     A save, a joining player's sync and the diagnostics read Scanner Rooms on their own threads while packets keep
///     changing them. They are handed copies the manager takes at one moment; nothing holds a room still for them.
/// </summary>
[TestClass]
public sealed class ScannerRoomManagerTest
{
    private static readonly NitroxTechType quartz = new("Quartz");

    [TestMethod]
    public void RoomsOfALoadedWorldAreKnownWithTheCamerasInTheirDocks()
    {
        ScannerRoomScenarioFixture scenario = new();
        NitroxId numberedCameraId = new("00000000-0000-0000-0000-000000000298");
        NitroxId unnumberedCameraId = new("00000000-0000-0000-0000-000000000299");
        MapRoomEntity loaded = new(new NitroxId("00000000-0000-0000-0000-000000000110"), ScannerRoomScenarioFixture.RoomParentId, new NitroxInt3(5, -2, 9));
        loaded.ChildEntities.Add(ScannerRoomScenarioFixture.CreateCamera(numberedCameraId, loaded.Id, new MapRoomCameraMetadata(cameraNumber: 7, dockingIndex: 0)));
        loaded.ChildEntities.Add(ScannerRoomScenarioFixture.CreateCamera(unnumberedCameraId, loaded.Id, new MapRoomCameraMetadata(dockingIndex: 1)));
        scenario.EntityRegistry.AddOrUpdate(loaded);

        scenario.Manager.WorldLoaded();

        CollectionAssert.AreEquivalent(new[] { ScannerRoomScenarioFixture.RoomId, loaded.Id },
            scenario.Manager.GetSnapshots().Select(snapshot => snapshot.Room.Id).ToArray());
        Assert.AreEqual(7, scenario.CameraState(numberedCameraId).CameraNumber, "A camera keeps the number it was saved with.");
        Assert.AreEqual(8, scenario.CameraState(unnumberedCameraId).CameraNumber, "A camera without a number gets the next one after the highest in the world.");
        Assert.AreEqual(1, scenario.CameraState(ScannerRoomScenarioFixture.CameraAId).CameraNumber, "The numbers the cameras had before are kept.");
        Assert.AreEqual(numberedCameraId, scenario.DockedCamera(0, loaded));
        Assert.AreEqual(unnumberedCameraId, scenario.DockedCamera(1, loaded));
        Assert.IsNull(ScannerRoomStateFingerprint.Validate(scenario.Manager.GetSnapshot(loaded.Id)!.Room));
    }

    [TestMethod]
    public async Task ScanResultsAreCountedWithTheCopyButAreNoPartOfTheSaveOrTheFingerprint()
    {
        ScannerRoomScenarioFixture scenario = new();
        scenario.StartScan(scenario.PlayerA, scenario.Room, quartz);
        string fingerprintBefore = ScannerRoomStateFingerprint.Create(scenario.Manager.GetSnapshot(scenario.Room.Id)!.Room).Fingerprint;

        await scenario.ReportScanAsync(scenario.PlayerA, scenario.Room, new MapRoomScanResultRecord("found", quartz, new NitroxVector3(1f, 2f, 3f)));
        await scenario.Manager.ReportScanTypesAsync(scenario.PlayerA, new MapRoomScanTypesReport(scenario.Room.Id, [quartz], [quartz], NitroxVector3.Zero, 300f));

        ScannerRoomSnapshot withResults = scenario.Manager.GetSnapshot(scenario.Room.Id)!;
        Assert.AreEqual(1, withResults.ScanResultCount);
        Assert.AreEqual(1, withResults.ScanTypeCount);
        Assert.AreEqual(fingerprintBefore, ScannerRoomStateFingerprint.Create(withResults.Room).Fingerprint,
            "A world that is saved and loaded again has no scan results. Its rooms must still have the fingerprint they were saved with.");

        // What is saved of a scan is in the fingerprint.
        await scenario.UpdateMetadataAsync(scenario.PlayerA, scenario.Room.Id, new MapRoomMetadata(quartz, 1, 5));
        string afterProgress = ScannerRoomStateFingerprint.Create(scenario.Manager.GetSnapshot(scenario.Room.Id)!.Room).Fingerprint;
        await scenario.UpdateMetadataAsync(scenario.PlayerA, scenario.Room.Id, new MapRoomMetadata(NitroxTechType.None, 0, 5));
        string afterStop = ScannerRoomStateFingerprint.Create(scenario.Manager.GetSnapshot(scenario.Room.Id)!.Room).Fingerprint;
        Assert.AreNotEqual(fingerprintBefore, afterProgress);
        Assert.AreNotEqual(afterProgress, afterStop);
    }

    [TestMethod]
    public void SnapshotsFollowRoomsThatAreBuiltReplacedAndRemoved()
    {
        ScannerRoomScenarioFixture scenario = new();
        MapRoomEntity built = scenario.AddRoom(new MapRoomEntity(new NitroxId("00000000-0000-0000-0000-000000000110"), ScannerRoomScenarioFixture.RoomParentId,
            new NitroxInt3(5, -2, 9)));
        Assert.AreEqual(2, scenario.Manager.GetSnapshots().Count);

        MapRoomEntity replacement = new(built.Id, ScannerRoomScenarioFixture.RoomParentId, new NitroxInt3(6, -2, 9));
        scenario.EntityRegistry.AddOrUpdate(replacement);
        Assert.IsTrue(scenario.EntityRegistry.RemoveEntity(ScannerRoomScenarioFixture.RoomId).HasValue);

        ScannerRoomSnapshot only = scenario.Manager.GetSnapshots().Single();
        Assert.AreEqual(replacement.Id, only.Room.Id);
        Assert.AreEqual(replacement.Cell, only.Room.Cell, "A replaced room must not stay behind as the one the manager works on.");
        Assert.IsNull(scenario.Manager.GetSnapshot(ScannerRoomScenarioFixture.RoomId));
    }

    [TestMethod]
    public async Task SnapshotIsACopyThatLaterChangesDoNotReach()
    {
        ScannerRoomScenarioFixture scenario = new();
        Assert.IsTrue((await scenario.DockAsync(scenario.PlayerA, ScannerRoomScenarioFixture.CameraAId, 0, true)).Granted);

        ScannerRoomSnapshot before = scenario.Manager.GetSnapshot(ScannerRoomScenarioFixture.RoomId)!;
        Assert.IsTrue((await scenario.DockAsync(scenario.PlayerA, ScannerRoomScenarioFixture.CameraBId, 1, true)).Granted);
        scenario.SetCameraComponents(ScannerRoomScenarioFixture.CameraAId, 5f, MapRoomCameraMetadata.MAX_HEALTH);

        Assert.AreNotSame(scenario.Room, before.Room);
        Entity copiedCamera = ScannerRoomStateFingerprint.GetCameras(before.Room).Single();
        Assert.AreEqual(ScannerRoomScenarioFixture.CameraAId, copiedCamera.Id);
        Assert.AreNotSame(scenario.EntityRegistry.GetEntityById(ScannerRoomScenarioFixture.CameraAId).Value, copiedCamera, "The cameras in the docks are copied with the room.");
        Assert.AreEqual(MapRoomCameraMetadata.MAX_ENERGY, ScannerRoomStateFingerprint.ReadState(copiedCamera).Energy);
        Assert.AreEqual(2, ScannerRoomStateFingerprint.GetCameras(scenario.Room).Length);
    }

    [TestMethod]
    public async Task SavedRoomIsExactlyTheCopyTakenForTheSaveWhateverHappensWhileItIsWritten()
    {
        ScannerRoomScenarioFixture scenario = new();
        BuildEntity baseEntity = AddBase(scenario);
        Assert.IsTrue((await scenario.DockAsync(scenario.PlayerA, ScannerRoomScenarioFixture.CameraAId, 0, true)).Granted);
        ServerJsonSerializer serializer = new(NullLogger<ServerJsonSerializer>.Instance);

        List<GlobalRootEntity> detached = scenario.Manager.DetachRooms([baseEntity], out IReadOnlyList<ScannerRoomSnapshot> savedRooms);
        // The save is still being written. Nothing holds the room still for it, so this request goes through at once.
        Task<ScannerRoomScenarioFixture.DockResult> dockDuringSave = scenario.DockAsync(scenario.PlayerA, ScannerRoomScenarioFixture.CameraBId, 1, true);
        Assert.IsTrue(dockDuringSave.Wait(TimeSpan.FromSeconds(5)), "A save must not hold up Scanner Room requests.");
        Assert.IsTrue(dockDuringSave.Result.Granted);
        using MemoryStream payload = new();
        serializer.Serialize(payload, GlobalRootData.From(detached));

        using MemoryStream input = new(payload.ToArray());
        MapRoomEntity savedRoom = serializer.Deserialize<GlobalRootData>(input).Entities.Single().ChildEntities.OfType<MapRoomEntity>().Single();
        ScannerRoomSnapshot saved = savedRooms.Single();
        ScannerRoomStateSnapshot savedFingerprint = ScannerRoomStateFingerprint.Create(saved.Room);
        Assert.AreNotSame(baseEntity, detached.Single(), "The live base must not be the one that is written.");
        Assert.AreSame(scenario.Room, baseEntity.ChildEntities.Single(), "The live base keeps its live room.");
        Assert.IsNull(ScannerRoomStateFingerprint.Validate(saved.Room));
        Assert.AreEqual(savedFingerprint.CanonicalState, ScannerRoomStateFingerprint.Create(savedRoom).CanonicalState);
        // The camera in the dock is saved as a child of the room, with its state, and comes back as one.
        GlobalRootEntity savedCamera = savedRoom.ChildEntities.OfType<GlobalRootEntity>().Single();
        Assert.AreEqual(ScannerRoomScenarioFixture.CameraAId, savedCamera.Id);
        Assert.AreEqual(savedRoom.Id, savedCamera.ParentId);
        Assert.AreEqual(scenario.CameraState(ScannerRoomScenarioFixture.CameraAId).CameraNumber, ((MapRoomCameraMetadata)savedCamera.Metadata).CameraNumber);
        Assert.AreEqual(0, ((MapRoomCameraMetadata)savedCamera.Metadata).DockingIndex);
        Assert.AreNotEqual(savedFingerprint.Fingerprint, ScannerRoomStateFingerprint.Create(scenario.Room).Fingerprint,
            "The dock made while the save was written is live state only, not part of the save.");

        ScannerRoomDiagnosticEntry checkpoint = scenario.Diagnostics.RecordCapturedCheckpoint("save", saved.Room, savedFingerprint, null, "serialized_snapshot");
        Assert.AreEqual(ScannerRoomDiagnosticOutcome.CHECKPOINT, checkpoint.Outcome);
        Assert.AreEqual(savedFingerprint.Fingerprint, checkpoint.StateFingerprint,
            "The save checkpoint must report what was saved, not the live state at the time of the report.");
    }

    [TestMethod, Timeout(30_000)]
    public async Task SaveCopiesTakenDuringDockingAndScanningAreConsistent()
    {
        ScannerRoomScenarioFixture scenario = new(new NopPacketSender());
        BuildEntity baseEntity = AddBase(scenario);
        Assert.IsTrue(scenario.Ownership.TryAcquire(ScannerRoomScenarioFixture.RoomId, scenario.PlayerA, SimulationLockType.TRANSIENT));
        Assert.IsTrue(scenario.Ownership.TryAcquire(ScannerRoomScenarioFixture.CameraAId, scenario.PlayerA, SimulationLockType.TRANSIENT));
        scenario.Room.Metadata = new MapRoomMetadata(quartz, 0, 1);
        long dockReports = 0;
        long scanReports = 0;
        using CancellationTokenSource stop = new();

        // Every dock or undock moves the camera into or out of the room's children and changes the camera's parent
        // and docking index. A copy taken in the middle of one would show them apart. The scan results change all
        // the while; they are counted with the copy, at the same moment, and are no part of it.
        Task docking = Task.Run(async () =>
        {
            for (bool dock = true; !stop.IsCancellationRequested; dock = !dock)
            {
                Assert.IsTrue(dock
                    ? await scenario.Manager.DockCameraAsync(scenario.PlayerA, new MapRoomCameraDocked(ScannerRoomScenarioFixture.CameraAId, ScannerRoomScenarioFixture.RoomId, 0))
                    : await scenario.Manager.UndockCameraAsync(scenario.PlayerA, new MapRoomCameraUndocked(ScannerRoomScenarioFixture.CameraAId, ScannerRoomScenarioFixture.RoomId)));
                Interlocked.Increment(ref dockReports);
            }
        });
        Task scanning = Task.Run(async () =>
        {
            for (bool found = true; !stop.IsCancellationRequested; found = !found)
            {
                await scenario.Manager.ReportScanResultAsync(scenario.PlayerA, new MapRoomScanResultReport(ScannerRoomScenarioFixture.RoomId, 1,
                    new MapRoomScanResultRecord("node", quartz, new NitroxVector3(10f, 0f, 0f)), !found, false, NitroxVector3.Zero, 300f));
                Interlocked.Increment(ref scanReports);
            }
        });

        List<string> inconsistencies = [];
        long dockChangesSeen = 0;
        long scanChangesSeen = 0;
        DateTime deadline = DateTime.UtcNow.AddSeconds(15);
        // Copies are taken until both kinds of change have happened many times in between.
        while ((dockChangesSeen < 200 || scanChangesSeen < 200) && !docking.IsCompleted && !scanning.IsCompleted && DateTime.UtcNow < deadline)
        {
            scenario.Manager.DetachRooms([baseEntity], out IReadOnlyList<ScannerRoomSnapshot> savedRooms);
            ScannerRoomSnapshot saved = savedRooms.Single();
            MapRoomEntity copy = saved.Room;
            dockChangesSeen = Interlocked.Read(ref dockReports);
            scanChangesSeen = Interlocked.Read(ref scanReports);
            Entity[] cameras = ScannerRoomStateFingerprint.GetCameras(copy);
            if (ScannerRoomStateFingerprint.Validate(copy) is { } violation)
            {
                inconsistencies.Add(violation);
            }
            if (cameras.Length > 1 || cameras.Any(camera => camera.ParentId != copy.Id || ScannerRoomStateFingerprint.ReadState(camera).DockingIndex != 0))
            {
                inconsistencies.Add($"{cameras.Length} camera(s) in the room, parent {cameras.FirstOrDefault()?.ParentId}, dock {cameras.Select(camera => ScannerRoomStateFingerprint.ReadState(camera).DockingIndex).FirstOrDefault()}");
            }
            if (saved.ScanResultCount is not (0 or 1))
            {
                inconsistencies.Add($"{saved.ScanResultCount} scan results of the one resource that is reported");
            }
            await Task.Yield();
        }
        await stop.CancelAsync();
        await Task.WhenAll(docking, scanning);

        Assert.AreEqual(0, inconsistencies.Count, string.Join("; ", inconsistencies.Distinct()));
        Assert.IsTrue(dockChangesSeen >= 200 && scanChangesSeen >= 200,
            $"The room was not changed often enough while the copies were taken: {dockChangesSeen} dock change(s), {scanChangesSeen} scan change(s).");
    }

    [TestMethod, Timeout(30_000)]
    public async Task OperationsOnDifferentRoomsAtTheSameTimeDoNotDeadlock()
    {
        ScannerRoomScenarioFixture scenario = new(new NopPacketSender());
        MapRoomEntity otherRoom = scenario.AddRoom(new MapRoomEntity(new NitroxId("00000000-0000-0000-0000-000000000110"), ScannerRoomScenarioFixture.RoomParentId,
            new NitroxInt3(5, -2, 9)));
        Assert.IsTrue(scenario.Ownership.TryAcquire(ScannerRoomScenarioFixture.CameraAId, scenario.PlayerA, SimulationLockType.TRANSIENT));
        Assert.IsTrue(scenario.Ownership.TryAcquire(ScannerRoomScenarioFixture.CameraBId, scenario.PlayerB, SimulationLockType.TRANSIENT));

        // Each camera goes back and forth between the two rooms, which makes it the child of one and then of the
        // other: the two tasks need the rooms in opposite order. A third one keeps taking copies of both.
        Task aToB = Task.Run(() => MoveBetweenRoomsAsync(scenario, ScannerRoomScenarioFixture.CameraAId, scenario.PlayerA, scenario.Room, otherRoom, 0));
        Task bToA = Task.Run(() => MoveBetweenRoomsAsync(scenario, ScannerRoomScenarioFixture.CameraBId, scenario.PlayerB, otherRoom, scenario.Room, 1));
        Task saving = Task.Run(() =>
        {
            while (!aToB.IsCompleted || !bToA.IsCompleted)
            {
                foreach (ScannerRoomSnapshot snapshot in scenario.Manager.GetSnapshots())
                {
                    Assert.IsNull(ScannerRoomStateFingerprint.Validate(snapshot.Room));
                }
            }
        });

        Task all = Task.WhenAll(aToB, bToA, saving);
        Assert.AreSame(all, await Task.WhenAny(all, Task.Delay(TimeSpan.FromSeconds(20))), "Requests for two rooms are waiting for each other.");
        await all;
        Assert.IsTrue(scenario.Manager.GetSnapshots().Count(snapshot => ScannerRoomStateFingerprint.GetCameras(snapshot.Room).Any(camera => camera.Id == ScannerRoomScenarioFixture.CameraAId)) <= 1,
            "A camera is in one room at a time.");
    }

    [TestMethod]
    public async Task JoiningPlayerIsSentTheRoomsAsCopiesWithTheCamerasInTheirDocks()
    {
        ScannerRoomScenarioFixture scenario = new();
        BuildEntity baseEntity = AddBase(scenario);
        Assert.IsTrue((await scenario.DockAsync(scenario.PlayerA, ScannerRoomScenarioFixture.CameraAId, 0, true)).Granted);
        // What a joining player is sent: the entities at the root of the world. A docked camera is not one of them.
        List<GlobalRootEntity> globalRoots = [baseEntity, .. new[] { ScannerRoomScenarioFixture.CameraBId, ScannerRoomScenarioFixture.CameraCId }
            .Select(cameraId => scenario.EntityRegistry.GetEntityById<GlobalRootEntity>(cameraId).Value)];

        List<GlobalRootEntity> sync = scenario.Manager.DetachRooms(globalRoots);
        // The sync is written after it was put together. A dock that arrives meanwhile is not half in it.
        Assert.IsTrue((await scenario.DockAsync(scenario.PlayerA, ScannerRoomScenarioFixture.CameraBId, 1, true)).Granted);

        MapRoomEntity syncedRoom = sync.Single(entity => entity.Id == baseEntity.Id).ChildEntities.OfType<MapRoomEntity>().Single();
        Assert.AreNotSame(scenario.Room, syncedRoom);
        GlobalRootEntity syncedCamera = (GlobalRootEntity)ScannerRoomStateFingerprint.GetCameras(syncedRoom).Single();
        Assert.AreEqual(ScannerRoomScenarioFixture.CameraAId, syncedCamera.Id, "The camera in the dock comes with its room, as its child.");
        Assert.AreEqual(syncedRoom.Id, syncedCamera.ParentId);
        Assert.AreEqual(0, ScannerRoomStateFingerprint.ReadState(syncedCamera).DockingIndex);
        Assert.IsTrue(ScannerRoomStateFingerprint.ReadState(syncedCamera).CameraNumber > 0);
        Assert.AreEqual(2, ScannerRoomStateFingerprint.GetCameras(scenario.Room).Length);
    }

    [TestMethod]
    public void CapturedCheckpointRetainsCapturedInvariantAfterLiveRepair()
    {
        ScannerRoomScenarioFixture scenario = new();
        GlobalRootEntity first = scenario.EntityRegistry.GetEntityById<GlobalRootEntity>(ScannerRoomScenarioFixture.CameraAId).Value;
        GlobalRootEntity second = scenario.EntityRegistry.GetEntityById<GlobalRootEntity>(ScannerRoomScenarioFixture.CameraBId).Value;
        first.Metadata = scenario.CameraState(first.Id).WithDockingIndex(0);
        second.Metadata = scenario.CameraState(second.Id).WithDockingIndex(0);
        scenario.Room.ChildEntities.AddRange([first, second]);
        ScannerRoomSnapshot captured = scenario.Manager.GetSnapshot(ScannerRoomScenarioFixture.RoomId)!;
        ScannerRoomStateSnapshot fingerprint = ScannerRoomStateFingerprint.Create(captured.Room);
        string? invariantFailure = ScannerRoomStateFingerprint.Validate(captured.Room);
        scenario.Room.ChildEntities.Remove(second);

        ScannerRoomDiagnosticEntry checkpoint = scenario.Diagnostics.RecordCapturedCheckpoint("save", captured.Room, fingerprint, invariantFailure, "serialized_snapshot");

        Assert.AreEqual(ScannerRoomDiagnosticOutcome.INVARIANT_FAILURE, checkpoint.Outcome);
        Assert.AreEqual("save_invalid", checkpoint.EventName);
        Assert.AreEqual("duplicate_dock", checkpoint.Reason, "A copy is exact: a broken room is saved and reported as it is.");
        Assert.AreEqual(fingerprint.Fingerprint, checkpoint.StateFingerprint);
    }

    /// <summary>
    ///     Docks a camera in one room and then in the other, a number of times, as the player who simulates it.
    /// </summary>
    private static async Task MoveBetweenRoomsAsync(ScannerRoomScenarioFixture scenario, NitroxId cameraId, ServerPlayer simulator, MapRoomEntity firstRoom, MapRoomEntity secondRoom,
        int dockingIndex)
    {
        for (int round = 0; round < 200; round++)
        {
            MapRoomEntity room = round % 2 == 0 ? firstRoom : secondRoom;
            Assert.IsTrue(await scenario.Manager.DockCameraAsync(simulator, new MapRoomCameraDocked(cameraId, room.Id, dockingIndex)));
            Assert.IsTrue(await scenario.Manager.UndockCameraAsync(simulator, new MapRoomCameraUndocked(cameraId, room.Id)));
        }
    }

    /// <summary>
    ///     Makes the fixture's room a piece of a base, as it is in a world. The base's position is what scans are anchored to.
    /// </summary>
    private static BuildEntity AddBase(ScannerRoomScenarioFixture scenario)
    {
        BuildEntity baseEntity = new(null, new NitroxTransform(NitroxVector3.Zero, NitroxQuaternion.Identity, NitroxVector3.One), GlobalRootEntity.GLOBAL_ROOT_LEVEL, "base", true,
            ScannerRoomScenarioFixture.RoomParentId, new NitroxTechType("Base"), null, null, [scenario.Room]);
        scenario.EntityRegistry.AddEntity(baseEntity);
        return baseEntity;
    }
}
