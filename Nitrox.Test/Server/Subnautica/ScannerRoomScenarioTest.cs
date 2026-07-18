using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Bases;
using Nitrox.Model.Subnautica.Packets;
using Nitrox.Server.Subnautica.Models.GameLogic;
using Nitrox.Server.Subnautica.Models.GameLogic.Entities;
using Nitrox.Server.Subnautica.Models.Serialization;
using ServerPlayer = Nitrox.Server.Subnautica.Models.Player;

namespace Nitrox.Test.Server.Subnautica;

[TestClass]
public sealed class ScannerRoomScenarioTest
{
    [TestMethod]
    public async Task DockMutationRequiresCameraOrRoomAuthorityButReplayDoesNot()
    {
        ScannerRoomScenarioFixture scenario = new();

        MapRoomCameraDock unauthorizedDock = await scenario.DockAsync(scenario.PlayerB,
            ScannerRoomScenarioFixture.CameraAId, 0, true, establishAuthority: false);
        Assert.IsFalse(unauthorizedDock.Granted);
        Assert.IsNull(scenario.Room.LeftDockCameraId);

        Assert.IsTrue((await scenario.DockAsync(scenario.PlayerA, ScannerRoomScenarioFixture.CameraAId, 0, true)).Granted);
        MapRoomCameraDock replay = await scenario.DockAsync(scenario.PlayerB,
            ScannerRoomScenarioFixture.CameraAId, 0, true, establishAuthority: false);
        Assert.IsTrue(replay.Granted, "Exact canonical replay is safe without mutation authority.");
        Assert.AreEqual(1, replay.Revision);

        MapRoomCameraDock unauthorizedUndock = await scenario.DockAsync(scenario.PlayerB,
            ScannerRoomScenarioFixture.CameraAId, 0, false, establishAuthority: false);
        Assert.IsFalse(unauthorizedUndock.Granted);
        Assert.AreEqual(ScannerRoomScenarioFixture.CameraAId, scenario.Room.LeftDockCameraId);

        Assert.IsTrue(scenario.Ownership.TryToAcquire(scenario.Room.Id, scenario.PlayerB, SimulationLockType.TRANSIENT));
        MapRoomCameraDock roomAuthorizedUndock = await scenario.DockAsync(scenario.PlayerB,
            ScannerRoomScenarioFixture.CameraAId, 0, false, establishAuthority: false);
        Assert.IsTrue(roomAuthorizedUndock.Granted);
        Assert.IsNull(scenario.Room.LeftDockCameraId);
    }

    [TestMethod]
    public async Task DockUndockReplayAndDelayedUndockPreserveCanonicalState()
    {
        ScannerRoomScenarioFixture scenario = new();

        MapRoomCameraDock firstDock = await scenario.DockAsync(scenario.PlayerA, ScannerRoomScenarioFixture.CameraAId, 0, true);
        Assert.IsTrue(firstDock.Granted);
        Assert.AreEqual(1, firstDock.Revision);

        string afterFirstDock = scenario.Snapshot();
        MapRoomCameraDock replayedDock = await scenario.DockAsync(scenario.PlayerB, ScannerRoomScenarioFixture.CameraAId, 0, true);
        Assert.IsTrue(replayedDock.Granted);
        Assert.AreEqual(1, replayedDock.Revision);
        Assert.AreEqual(afterFirstDock, scenario.Snapshot(), "An exact dock replay must be idempotent regardless of sender.");

        MapRoomCameraDock undock = await scenario.DockAsync(scenario.PlayerA, ScannerRoomScenarioFixture.CameraAId, 0, false);
        Assert.IsTrue(undock.Granted);
        Assert.AreEqual(2, undock.Revision);

        MapRoomCameraDock replacementDock = await scenario.DockAsync(scenario.PlayerB, ScannerRoomScenarioFixture.CameraBId, 0, true);
        Assert.IsTrue(replacementDock.Granted);
        Assert.AreEqual(3, replacementDock.Revision);

        string beforeDelayedUndock = scenario.Snapshot();
        MapRoomCameraDock delayedUndock = await scenario.DockAsync(scenario.PlayerA, ScannerRoomScenarioFixture.CameraAId, 0, false);
        Assert.IsFalse(delayedUndock.Granted);
        Assert.AreEqual(3, delayedUndock.Revision);
        Assert.AreEqual(beforeDelayedUndock, scenario.Snapshot(), "A delayed undock must not clear the replacement camera.");

        MapRoomCameraDock replayedReplacement = await scenario.DockAsync(scenario.PlayerA, ScannerRoomScenarioFixture.CameraBId, 0, true);
        Assert.IsTrue(replayedReplacement.Granted);
        Assert.AreEqual(3, replayedReplacement.Revision);
        Assert.AreEqual(beforeDelayedUndock, scenario.Snapshot());

        scenario.AssertCanonicalInvariants();
        Assert.AreEqual(2, scenario.Room.CameraRegistry.Count, "Undocking must retain the stable camera-number registration.");
        Assert.AreEqual(1, scenario.Room.GetCameraRecord(ScannerRoomScenarioFixture.CameraAId)?.CameraNumber);
        Assert.AreEqual(2, scenario.Room.GetCameraRecord(ScannerRoomScenarioFixture.CameraBId)?.CameraNumber);
    }

    [TestMethod]
    public async Task ExclusiveControlTransfersOnlyAfterCanonicalRelease()
    {
        ScannerRoomScenarioFixture scenario = new();
        Assert.IsTrue((await scenario.DockAsync(scenario.PlayerA, ScannerRoomScenarioFixture.CameraAId, 0, true)).Granted);

        MapRoomCameraControl acquired = await scenario.ControlAsync(scenario.PlayerA, ScannerRoomScenarioFixture.CameraAId, 0, true);
        Assert.IsTrue(acquired.Granted);
        Assert.AreEqual(scenario.PlayerA.SessionId, acquired.ControllerSessionId);
        Assert.AreSame(scenario.PlayerA, scenario.Ownership.GetPlayerForLock(ScannerRoomScenarioFixture.CameraAId));

        MapRoomCameraControl conflict = await scenario.ControlAsync(scenario.PlayerB, ScannerRoomScenarioFixture.CameraAId, 0, true);
        Assert.IsFalse(conflict.Granted);
        Assert.AreEqual(scenario.PlayerA.SessionId, conflict.ControllerSessionId);
        Assert.AreSame(scenario.PlayerA, scenario.Ownership.GetPlayerForLock(ScannerRoomScenarioFixture.CameraAId));

        MapRoomCameraControl replay = await scenario.ControlAsync(scenario.PlayerA, ScannerRoomScenarioFixture.CameraAId, 0, true);
        Assert.IsTrue(replay.Granted);
        Assert.AreSame(scenario.PlayerA, scenario.Ownership.GetPlayerForLock(ScannerRoomScenarioFixture.CameraAId));

        Assert.IsTrue((await scenario.ControlAsync(scenario.PlayerA, ScannerRoomScenarioFixture.CameraAId, 0, false)).Granted);
        Assert.IsNull(scenario.Ownership.GetPlayerForLock(ScannerRoomScenarioFixture.CameraAId));

        MapRoomCameraControl replayedRelease = await scenario.ControlAsync(scenario.PlayerA, ScannerRoomScenarioFixture.CameraAId, 0, false);
        Assert.IsTrue(replayedRelease.Granted, "Release replay is an acknowledged no-op once the lock is absent.");
        Assert.IsNull(scenario.Ownership.GetPlayerForLock(ScannerRoomScenarioFixture.CameraAId));

        MapRoomCameraControl transferred = await scenario.ControlAsync(scenario.PlayerB, ScannerRoomScenarioFixture.CameraAId, 0, true);
        Assert.IsTrue(transferred.Granted);
        Assert.AreSame(scenario.PlayerB, scenario.Ownership.GetPlayerForLock(ScannerRoomScenarioFixture.CameraAId));
    }

    [DataTestMethod]
    [DataRow(1425)]
    [DataRow(2410)]
    public async Task TwoCamerasRacingOneDockSlotProduceExactlyOneWinner(int seed)
    {
        Random random = new(seed);

        for (int iteration = 0; iteration < 16; iteration++)
        {
            ScannerRoomScenarioFixture scenario = new();
            bool swap = random.Next(2) == 0;
            ServerPlayer firstPlayer = swap ? scenario.PlayerB : scenario.PlayerA;
            ServerPlayer secondPlayer = swap ? scenario.PlayerA : scenario.PlayerB;
            NitroxId firstCamera = swap ? ScannerRoomScenarioFixture.CameraBId : ScannerRoomScenarioFixture.CameraAId;
            NitroxId secondCamera = swap ? ScannerRoomScenarioFixture.CameraAId : ScannerRoomScenarioFixture.CameraBId;

            using Barrier start = new(2);
            Task<MapRoomCameraDock> first = Task.Run(async () =>
            {
                start.SignalAndWait();
                return await scenario.DockAsync(firstPlayer, firstCamera, 0, true);
            });
            Task<MapRoomCameraDock> second = Task.Run(async () =>
            {
                start.SignalAndWait();
                return await scenario.DockAsync(secondPlayer, secondCamera, 0, true);
            });

            MapRoomCameraDock[] responses = await Task.WhenAll(first, second);
            MapRoomCameraDock winner = responses.Single(response => response.Granted);

            Assert.AreEqual(1, responses.Count(response => response.Granted), $"seed={seed}, iteration={iteration}");
            Assert.AreEqual(1, scenario.Room.DockingRevision, $"seed={seed}, iteration={iteration}");
            Assert.AreEqual(winner.CameraId, scenario.Room.LeftDockCameraId, $"seed={seed}, iteration={iteration}");
            Assert.IsNull(scenario.Room.RightDockCameraId, $"seed={seed}, iteration={iteration}");
            Assert.AreEqual(1, scenario.Room.CameraRegistry.Count, $"seed={seed}, iteration={iteration}");
            scenario.AssertCanonicalInvariants();
        }
    }

    [DataTestMethod]
    [DataRow(1533)]
    [DataRow(2576)]
    public async Task TwoPlayersRacingOneCameraControlLockProduceExactlyOneWinner(int seed)
    {
        Random random = new(seed);

        for (int iteration = 0; iteration < 16; iteration++)
        {
            ScannerRoomScenarioFixture scenario = new();
            Assert.IsTrue((await scenario.DockAsync(scenario.PlayerA, ScannerRoomScenarioFixture.CameraAId, 0, true)).Granted);
            bool swap = random.Next(2) == 0;
            ServerPlayer firstPlayer = swap ? scenario.PlayerB : scenario.PlayerA;
            ServerPlayer secondPlayer = swap ? scenario.PlayerA : scenario.PlayerB;

            using Barrier start = new(2);
            Task<MapRoomCameraControl> first = Task.Run(async () =>
            {
                start.SignalAndWait();
                return await scenario.ControlAsync(firstPlayer, ScannerRoomScenarioFixture.CameraAId, 0, true);
            });
            Task<MapRoomCameraControl> second = Task.Run(async () =>
            {
                start.SignalAndWait();
                return await scenario.ControlAsync(secondPlayer, ScannerRoomScenarioFixture.CameraAId, 0, true);
            });

            MapRoomCameraControl[] responses = await Task.WhenAll(first, second);
            MapRoomCameraControl winner = responses.Single(response => response.Granted);
            ServerPlayer expectedOwner = winner.ControllerSessionId == scenario.PlayerA.SessionId ? scenario.PlayerA : scenario.PlayerB;

            Assert.AreEqual(1, responses.Count(response => response.Granted), $"seed={seed}, iteration={iteration}");
            Assert.AreSame(expectedOwner, scenario.Ownership.GetPlayerForLock(ScannerRoomScenarioFixture.CameraAId), $"seed={seed}, iteration={iteration}");
            Assert.AreEqual(winner.ControllerSessionId, scenario.Ownership.GetPlayerForLock(ScannerRoomScenarioFixture.CameraAId)?.SessionId, $"seed={seed}, iteration={iteration}");
            scenario.AssertCanonicalInvariants();
        }
    }

    [DataTestMethod]
    [DataRow(1425)]
    [DataRow(2410)]
    [DataRow(2576)]
    [DataRow(1533)]
    public async Task SeededMultiPlayerInterleavingsKeepRevisionsAndRegistryCanonical(int seed)
    {
        ScannerRoomScenarioFixture scenario = new();
        Random random = new(seed);

        for (int step = 0; step < 96; step++)
        {
            ServerPlayer player = random.Next(2) == 0 ? scenario.PlayerA : scenario.PlayerB;
            NitroxId cameraId = scenario.CameraIds[random.Next(scenario.CameraIds.Count)];
            int slot = random.Next(2);
            bool isDocked = random.Next(2) == 0;

            long revisionBefore = scenario.Room.DockingRevision;
            MapRoomCameraDock response = await scenario.DockAsync(player, cameraId, slot, isDocked);
            long revisionAfter = scenario.Room.DockingRevision;

            Assert.IsTrue(revisionAfter >= revisionBefore, $"seed={seed}, step={step}");
            Assert.IsTrue(revisionAfter - revisionBefore <= 1, $"seed={seed}, step={step}");
            Assert.AreEqual(revisionAfter, response.Revision, $"seed={seed}, step={step}");
            scenario.AssertCanonicalInvariants();

            if (random.Next(4) == 0)
            {
                string beforeReplay = scenario.Snapshot();
                MapRoomCameraDock replay = await scenario.DockAsync(player, cameraId, slot, isDocked);

                Assert.AreEqual(beforeReplay, scenario.Snapshot(), $"Exact replay mutated state at seed={seed}, step={step}.");
                Assert.AreEqual(revisionAfter, replay.Revision, $"seed={seed}, step={step}");
                scenario.AssertCanonicalInvariants();
            }
        }
    }

    [TestMethod]
    public async Task RealProcessorTraceRecordsAcceptedAndRejectedCanonicalTransitions()
    {
        ScannerRoomScenarioFixture scenario = new();

        Assert.IsTrue((await scenario.DockAsync(scenario.PlayerA, ScannerRoomScenarioFixture.CameraAId, 0, true)).Granted);
        Assert.IsTrue((await scenario.ControlAsync(scenario.PlayerA, ScannerRoomScenarioFixture.CameraAId, 0, true)).Granted);
        Assert.IsFalse((await scenario.ControlAsync(scenario.PlayerB, ScannerRoomScenarioFixture.CameraAId, 0, true)).Granted);
        Assert.IsTrue((await scenario.ControlAsync(scenario.PlayerA, ScannerRoomScenarioFixture.CameraAId, 0, false)).Granted);

        ScannerRoomDiagnosticEntry[] trace = scenario.Diagnostics.GetHistory().ToArray();
        CollectionAssert.AreEqual(new long[] { 1, 2, 3, 4 }, trace.Select(entry => entry.Sequence).ToArray());
        CollectionAssert.AreEqual(new[] { "dock", "control_acquire", "control_acquire", "control_release" }, trace.Select(entry => entry.EventName).ToArray());
        Assert.AreEqual(ScannerRoomDiagnosticOutcome.Rejected, trace[2].Outcome);
        Assert.AreEqual("locked", trace[2].Reason);
        Assert.AreEqual(ScannerRoomScenarioFixture.RoomId.ToString()[..8], trace[2].RoomId);
        Assert.AreEqual(ScannerRoomScenarioFixture.CameraAId.ToString()[..8], trace[2].CameraId);
        Assert.AreEqual(new ScannerRoomDiagnosticCounters(4, 3, 1, 0, 0), scenario.Diagnostics.GetCounters());
    }

    [TestMethod]
    public async Task DisconnectProducesExplicitReleaseOnlyForExclusiveCameraControl()
    {
        ScannerRoomScenarioFixture scenario = new();
        Assert.IsTrue((await scenario.DockAsync(scenario.PlayerA, ScannerRoomScenarioFixture.CameraAId, 0, true)).Granted);
        Assert.IsTrue((await scenario.DockAsync(scenario.PlayerB, ScannerRoomScenarioFixture.CameraBId, 1, true)).Granted);
        Assert.IsTrue((await scenario.ControlAsync(scenario.PlayerA, ScannerRoomScenarioFixture.CameraAId, 0, true)).Granted);
        Assert.IsTrue(scenario.Ownership.TryToAcquire(ScannerRoomScenarioFixture.CameraBId, scenario.PlayerA, SimulationLockType.TRANSIENT));

        List<SimulationOwnershipData.RevokedLock> revokedLocks = scenario.Ownership.RevokeAllLocksForOwner(scenario.PlayerA.SessionId);
        List<MapRoomCameraControl> releases = [];
        foreach (SimulationOwnershipData.RevokedLock revokedLock in revokedLocks)
        {
            if (scenario.ControlReleaseFactory.TryCreate(revokedLock.EntityId, revokedLock.Lock, "disconnect_test", out MapRoomCameraControl releasePacket))
            {
                releases.Add(releasePacket);
            }
        }

        Assert.AreEqual(1, releases.Count);
        MapRoomCameraControl release = releases[0];
        Assert.AreEqual(ScannerRoomScenarioFixture.CameraAId, release.CameraId);
        Assert.IsTrue(release.IsServerResponse);
        Assert.IsTrue(release.Granted);
        Assert.IsFalse(release.IsControlling);
        Assert.AreEqual(scenario.PlayerA.SessionId, release.ControllerSessionId);
        Assert.AreEqual(0, release.CameraIndex);
        Assert.AreEqual(ScannerRoomScenarioFixture.RoomId, release.MapRoomId.Value);
    }

    [TestMethod]
    public async Task LifecycleGatePreventsOldReleaseFromOvertakingNewAcquire()
    {
        ScannerRoomScenarioFixture scenario = new();
        Assert.IsTrue((await scenario.DockAsync(scenario.PlayerA, ScannerRoomScenarioFixture.CameraAId, 0, true)).Granted);
        Assert.IsTrue((await scenario.ControlAsync(scenario.PlayerA, ScannerRoomScenarioFixture.CameraAId, 0, true)).Granted);

        IDisposable lifecycleGate = await scenario.ControlLifecycle.EnterAsync(ScannerRoomScenarioFixture.CameraAId);
        Assert.IsTrue(scenario.Ownership.RevokeOwnerOfId(ScannerRoomScenarioFixture.CameraAId,
            out SimulationOwnershipData.PlayerLock revokedLock));
        Assert.IsTrue(scenario.ControlReleaseFactory.TryCreate(ScannerRoomScenarioFixture.CameraAId, revokedLock,
            "race_test", out MapRoomCameraControl release));

        Task<MapRoomCameraControl> newAcquire = scenario.ControlAsync(scenario.PlayerB,
            ScannerRoomScenarioFixture.CameraAId, 0, true);
        Task firstCompleted = await Task.WhenAny(newAcquire, Task.Delay(100));
        Assert.AreNotSame(newAcquire, firstCompleted, "New acquire must wait until the old release is queued.");

        Assert.IsFalse(release.IsControlling);
        lifecycleGate.Dispose();
        MapRoomCameraControl acquired = await newAcquire.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.IsTrue(acquired.Granted);
        Assert.AreEqual(scenario.PlayerB.SessionId, acquired.ControllerSessionId);
    }

    [TestMethod]
    public async Task GenericScannerOwnershipRequestWaitsForCameraLifecycleGate()
    {
        ScannerRoomScenarioFixture scenario = new();
        IDisposable destructiveLifecycleGate =
            await scenario.ControlLifecycle.EnterAsync(ScannerRoomScenarioFixture.CameraAId);
        Task<SimulationOwnershipResponse> request = scenario.RequestOwnershipAsync(scenario.PlayerA,
            ScannerRoomScenarioFixture.CameraAId, SimulationLockType.EXCLUSIVE);
        try
        {
            Task first = await Task.WhenAny(request, Task.Delay(100));
            Assert.AreNotSame(request, first,
                "A Stalker-style generic ownership request must wait for destructive camera cleanup.");
            Assert.IsNull(scenario.Ownership.GetPlayerForLock(ScannerRoomScenarioFixture.CameraAId));
        }
        finally
        {
            destructiveLifecycleGate.Dispose();
        }

        SimulationOwnershipResponse response = await request.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.IsTrue(response.LockAcquired);
        Assert.AreEqual(SimulationLockType.EXCLUSIVE, response.LockType);
        Assert.AreSame(scenario.PlayerA,
            scenario.Ownership.GetPlayerForLock(ScannerRoomScenarioFixture.CameraAId));
        ScannerRoomDiagnosticEntry diagnostic = scenario.Diagnostics.GetHistory().Single();
        Assert.AreEqual("camera_lock", diagnostic.EventName);
        Assert.AreEqual(ScannerRoomDiagnosticOutcome.Accepted, diagnostic.Outcome);
        Assert.AreEqual("exclusive", diagnostic.Reason);
    }

    [TestMethod]
    public async Task GenericScannerOwnershipRequestIsRejectedWhenCameraIsRemovedWhileWaitingForGate()
    {
        ScannerRoomScenarioFixture scenario = new();
        IDisposable destructiveLifecycleGate =
            await scenario.ControlLifecycle.EnterAsync(ScannerRoomScenarioFixture.CameraAId);
        Task<SimulationOwnershipResponse> request = scenario.RequestOwnershipAsync(scenario.PlayerA,
            ScannerRoomScenarioFixture.CameraAId, SimulationLockType.EXCLUSIVE);
        try
        {
            Task first = await Task.WhenAny(request, Task.Delay(100));
            Assert.AreNotSame(request, first);
            Assert.IsTrue(scenario.EntityRegistry.RemoveEntity(ScannerRoomScenarioFixture.CameraAId).HasValue);
        }
        finally
        {
            destructiveLifecycleGate.Dispose();
        }

        SimulationOwnershipResponse response = await request.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.IsFalse(response.LockAcquired);
        Assert.IsNull(scenario.Ownership.GetPlayerForLock(ScannerRoomScenarioFixture.CameraAId));
        ScannerRoomDiagnosticEntry diagnostic = scenario.Diagnostics.GetHistory().Single();
        Assert.AreEqual("camera_lock", diagnostic.EventName);
        Assert.AreEqual(ScannerRoomDiagnosticOutcome.Rejected, diagnostic.Outcome);
        Assert.AreEqual("removed", diagnostic.Reason);
    }

    [TestMethod]
    public async Task DelayedGenericScannerOwnershipRequestIsRejectedByLifecycleTombstoneAfterRemoval()
    {
        ScannerRoomScenarioFixture scenario = new();
        using (await scenario.ControlLifecycle.EnterAsync(ScannerRoomScenarioFixture.CameraAId))
        {
            Assert.IsTrue(scenario.EntityRegistry.RemoveEntity(ScannerRoomScenarioFixture.CameraAId).HasValue);
        }

        SimulationOwnershipResponse response = await scenario.RequestOwnershipAsync(scenario.PlayerA,
            ScannerRoomScenarioFixture.CameraAId, SimulationLockType.EXCLUSIVE);

        Assert.IsFalse(response.LockAcquired);
        Assert.IsNull(scenario.Ownership.GetPlayerForLock(ScannerRoomScenarioFixture.CameraAId));
        ScannerRoomDiagnosticEntry diagnostic = scenario.Diagnostics.GetHistory().Single();
        Assert.AreEqual("camera_lock", diagnostic.EventName);
        Assert.AreEqual(ScannerRoomDiagnosticOutcome.Rejected, diagnostic.Outcome);
        Assert.AreEqual("removed", diagnostic.Reason);
    }

    [TestMethod]
    public async Task PremarkedRegistryOnlyCameraIsRejectedAfterItsRoomIsRemoved()
    {
        ScannerRoomScenarioFixture scenario = new();
        NitroxId registryOnlyCamera = new("00000000-0000-0000-0000-000000000229");
        NitroxId[] cameraSnapshot;
        lock (scenario.Room)
        {
            scenario.Room.GetOrAssignCameraNumber(registryOnlyCamera, 1);
            cameraSnapshot = scenario.Room.CameraRegistry.Select(camera => camera.CameraId).ToArray();
        }
        scenario.ControlLifecycle.RememberMany(cameraSnapshot);
        Assert.IsTrue(scenario.EntityRegistry.RemoveEntity(ScannerRoomScenarioFixture.RoomId).HasValue);

        SimulationOwnershipResponse response = await scenario.RequestOwnershipAsync(scenario.PlayerA,
            registryOnlyCamera, SimulationLockType.EXCLUSIVE);

        Assert.IsFalse(response.LockAcquired);
        Assert.IsNull(scenario.Ownership.GetPlayerForLock(registryOnlyCamera));
        ScannerRoomDiagnosticEntry diagnostic = scenario.Diagnostics.GetHistory().Single();
        Assert.AreEqual("camera_lock", diagnostic.EventName);
        Assert.AreEqual(ScannerRoomDiagnosticOutcome.Rejected, diagnostic.Outcome);
        Assert.AreEqual("removed", diagnostic.Reason);
    }

    [TestMethod]
    public async Task RoomLifecycleGateRejectsDockBlockedByDeconstruction()
    {
        ScannerRoomScenarioFixture scenario = new();
        IDisposable deconstructionGate = await scenario.RoomLifecycle.EnterAsync(ScannerRoomScenarioFixture.RoomId);
        Task<MapRoomCameraDock> dock = scenario.DockAsync(scenario.PlayerA,
            ScannerRoomScenarioFixture.CameraAId, 0, true);
        try
        {
            Task first = await Task.WhenAny(dock, Task.Delay(100));
            Assert.AreNotSame(dock, first, "Dock must wait while deconstruction owns the room lifecycle gate.");
            Assert.IsTrue(scenario.EntityRegistry.RemoveEntity(ScannerRoomScenarioFixture.RoomId).HasValue);
        }
        finally
        {
            deconstructionGate.Dispose();
        }

        MapRoomCameraDock response = await dock.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.IsFalse(response.Granted);
        Assert.IsNull(scenario.Room.GetDockedCamera(0));
        Assert.IsNull(scenario.Room.GetCameraRecord(ScannerRoomScenarioFixture.CameraAId));
    }

    [TestMethod]
    public async Task DeconstructionCleanupPreservesCameraRegisteredWithNewLiveRoomWhileWaitingForCameraGate()
    {
        ScannerRoomScenarioFixture scenario = new();
        Assert.IsTrue((await scenario.DockAsync(scenario.PlayerA,
            ScannerRoomScenarioFixture.CameraAId, 0, true)).Granted);
        Assert.IsTrue((await scenario.DockAsync(scenario.PlayerA,
            ScannerRoomScenarioFixture.CameraAId, 0, false)).Granted);
        Assert.IsNotNull(scenario.Room.GetCameraRecord(ScannerRoomScenarioFixture.CameraAId));
        Assert.IsTrue(scenario.Ownership.TryToAcquire(ScannerRoomScenarioFixture.CameraAId,
            scenario.PlayerA, SimulationLockType.EXCLUSIVE));

        MapRoomEntity targetRoom = new(
            new NitroxId("00000000-0000-0000-0000-000000000110"),
            new NitroxId("00000000-0000-0000-0000-000000000111"),
            new(5, -2, 9));
        scenario.EntityRegistry.AddEntity(targetRoom);

        IDisposable targetDockGate = await scenario.ControlLifecycle.EnterAsync(ScannerRoomScenarioFixture.CameraAId);
        Assert.IsTrue(scenario.EntityRegistry.RemoveEntity(ScannerRoomScenarioFixture.RoomId).HasValue);
        Task<IReadOnlyList<Nitrox.Model.Packets.Packet>> cleanup = scenario.CleanupAsync(scenario.Room);
        try
        {
            Task first = await Task.WhenAny(cleanup, Task.Delay(100));
            Assert.AreNotSame(cleanup, first, "Source cleanup must wait for the target dock camera gate.");

            // This is the target dock processor's camera-gated registry mutation after the source
            // room has left the live registry. The removed source object still holds its snapshot.
            lock (targetRoom)
            {
                targetRoom.GetOrAssignCameraNumber(ScannerRoomScenarioFixture.CameraAId, 1);
                targetRoom.SetDockedCamera(0, ScannerRoomScenarioFixture.CameraAId);
            }
        }
        finally
        {
            targetDockGate.Dispose();
        }

        IReadOnlyList<Nitrox.Model.Packets.Packet> cleanupPackets =
            await cleanup.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.IsTrue(scenario.Ownership.TryGetLock(ScannerRoomScenarioFixture.CameraAId,
            out SimulationOwnershipData.PlayerLock cameraLock));
        Assert.AreSame(scenario.PlayerA, cameraLock.Player);
        Assert.AreEqual(SimulationLockType.EXCLUSIVE, cameraLock.LockType);
        Assert.IsFalse(cleanupPackets.OfType<DropSimulationOwnership>()
            .Any(packet => packet.EntityId == ScannerRoomScenarioFixture.CameraAId));
        Assert.IsFalse(cleanupPackets.OfType<MapRoomCameraControl>()
            .Any(packet => packet.CameraId == ScannerRoomScenarioFixture.CameraAId));
    }

    [TestMethod]
    public void ReleaseFactoryCoversAssociationlessLooseWorldCamera()
    {
        ScannerRoomScenarioFixture scenario = new();
        Assert.IsTrue(scenario.Ownership.TryToAcquire(ScannerRoomScenarioFixture.CameraCId,
            scenario.PlayerA, SimulationLockType.EXCLUSIVE));
        Assert.IsTrue(scenario.Ownership.RevokeOwnerOfId(ScannerRoomScenarioFixture.CameraCId,
            out SimulationOwnershipData.PlayerLock revokedLock));

        Assert.IsTrue(scenario.ControlReleaseFactory.TryCreate(ScannerRoomScenarioFixture.CameraCId, revokedLock,
            "loose_test", out MapRoomCameraControl release));
        Assert.IsFalse(release.MapRoomId.HasValue);
        Assert.AreEqual(-1, release.CameraIndex);
        Assert.IsFalse(release.IsControlling);
        Assert.AreEqual(scenario.PlayerA.SessionId, release.ControllerSessionId);
    }

    [TestMethod]
    public async Task JsonRoundTripThenOrphanRecoveryPreservesCanonicalCameraState()
    {
        ScannerRoomScenarioFixture scenario = new();
        Assert.IsTrue((await scenario.DockAsync(scenario.PlayerA, ScannerRoomScenarioFixture.CameraAId, 0, true)).Granted);
        Assert.IsTrue((await scenario.DockAsync(scenario.PlayerB, ScannerRoomScenarioFixture.CameraBId, 1, true)).Granted);
        Assert.IsTrue((await scenario.DockAsync(scenario.PlayerB, ScannerRoomScenarioFixture.CameraBId, 1, false)).Granted);

        MapRoomCameraRecord cameraB = scenario.Room.GetCameraRecord(ScannerRoomScenarioFixture.CameraBId)!;
        cameraB.LightOn = true;
        cameraB.LightRevision = 7;
        cameraB.Energy = 73.5f;
        cameraB.Health = 84.25f;
        cameraB.ComponentRevision = 11;

        ServerJsonSerializer serializer = new(NullLogger<ServerJsonSerializer>.Instance);
        using MemoryStream serialized = new();
        serializer.Serialize(serialized, scenario.Room);
        using MemoryStream input = new(serialized.ToArray());
        MapRoomEntity restored = serializer.Deserialize<MapRoomEntity>(input);

        Assert.AreEqual(scenario.Room.Id, restored.Id);
        Assert.AreEqual(scenario.Room.ParentId, restored.ParentId);
        Assert.AreEqual(scenario.Room.Cell, restored.Cell);
        Assert.AreEqual(scenario.Room.LeftDockCameraId, restored.LeftDockCameraId);
        Assert.AreEqual(scenario.Room.RightDockCameraId, restored.RightDockCameraId);
        Assert.AreEqual(3, restored.DockingRevision);
        Assert.AreEqual(2, restored.CameraRegistry.Count);

        MapRoomCameraRecord restoredCameraB = restored.GetCameraRecord(ScannerRoomScenarioFixture.CameraBId)!;
        Assert.AreEqual(2, restoredCameraB.CameraNumber);
        Assert.IsTrue(restoredCameraB.LightOn);
        Assert.AreEqual(7, restoredCameraB.LightRevision);
        Assert.AreEqual(73.5f, restoredCameraB.Energy);
        Assert.AreEqual(84.25f, restoredCameraB.Health);
        Assert.AreEqual(11, restoredCameraB.ComponentRevision);

        int recovered = MapRoomCameraPersistence.RestoreOrphanedRegistrations(restored, _ => false);
        Assert.AreEqual(1, recovered);
        Assert.AreEqual(ScannerRoomScenarioFixture.CameraAId, restored.LeftDockCameraId);
        Assert.AreEqual(ScannerRoomScenarioFixture.CameraBId, restored.RightDockCameraId);
        Assert.AreEqual(4, restored.DockingRevision);

        string afterRecovery = Snapshot(restored);
        Assert.AreEqual(0, MapRoomCameraPersistence.RestoreOrphanedRegistrations(restored, _ => false));
        Assert.AreEqual(afterRecovery, Snapshot(restored), "Late-join orphan recovery must be idempotent.");
    }

    private static string Snapshot(MapRoomEntity room)
    {
        string registrations = string.Join(",", room.CameraRegistry
            .OrderBy(record => record.CameraNumber)
            .Select(record => $"{record.CameraNumber}:{record.CameraId}"));
        return $"rev={room.DockingRevision};left={room.LeftDockCameraId};right={room.RightDockCameraId};registry={registrations}";
    }
}
