using System.Threading.Tasks;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.Packets;
using Nitrox.Server.Subnautica.Models.GameLogic;
using ServerPlayer = Nitrox.Server.Subnautica.Models.Player;

namespace Nitrox.Test.Server.Subnautica;

/// <summary>
///     Camera control and the simulation locks of cameras, on the real manager.
/// </summary>
[TestClass]
public sealed class ScannerRoomScenarioTest
{
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
        Assert.IsFalse(scenario.HasController(ScannerRoomScenarioFixture.CameraAId));
        Assert.IsTrue(scenario.Ownership.TryGetLock(ScannerRoomScenarioFixture.CameraAId, out SimulationOwnershipData.PlayerLock keptLock));
        Assert.AreSame(scenario.PlayerA, keptLock.Player, "The player who flew the camera goes on simulating it, which is who recharges it in its dock.");
        Assert.AreEqual(SimulationLockType.TRANSIENT, keptLock.LockType);

        MapRoomCameraControl replayedRelease = await scenario.ControlAsync(scenario.PlayerA, ScannerRoomScenarioFixture.CameraAId, 0, false);
        Assert.IsTrue(replayedRelease.Granted, "A release that is repeated is acknowledged and changes nothing.");
        Assert.AreSame(scenario.PlayerA, scenario.Ownership.GetPlayerForLock(ScannerRoomScenarioFixture.CameraAId));

        MapRoomCameraControl transferred = await scenario.ControlAsync(scenario.PlayerB, ScannerRoomScenarioFixture.CameraAId, 0, true);
        Assert.IsTrue(transferred.Granted);
        Assert.AreSame(scenario.PlayerB, scenario.Ownership.GetPlayerForLock(ScannerRoomScenarioFixture.CameraAId));
    }

    [TestMethod]
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
        Assert.AreEqual(ScannerRoomDiagnosticOutcome.REJECTED, trace[2].Outcome);
        Assert.AreEqual("locked", trace[2].Reason);
        Assert.AreEqual(ScannerRoomScenarioFixture.RoomId.ToString()[..8], trace[2].RoomId);
        string cameraId = ScannerRoomScenarioFixture.CameraAId.ToString();
        Assert.AreEqual($"{cameraId[..8]}.{cameraId.Substring(19, 4)}", trace[2].CameraId);
        Assert.AreEqual(new ScannerRoomDiagnosticCounters(4, 3, 1, 0, 0), scenario.Diagnostics.GetCounters());
    }

    [TestMethod]
    public async Task DisconnectProducesExplicitReleaseOnlyForExclusiveCameraControl()
    {
        ScannerRoomScenarioFixture scenario = new();
        Assert.IsTrue((await scenario.DockAsync(scenario.PlayerA, ScannerRoomScenarioFixture.CameraAId, 0, true)).Granted);
        Assert.IsTrue((await scenario.DockAsync(scenario.PlayerA, ScannerRoomScenarioFixture.CameraBId, 1, true)).Granted);
        Assert.IsTrue((await scenario.ControlAsync(scenario.PlayerA, ScannerRoomScenarioFixture.CameraAId, 0, true)).Granted);

        List<SimulationOwnershipData.RevokedLock> revokedLocks = scenario.Ownership.RevokeAllLocksForOwner(scenario.PlayerA.SessionId);
        List<MapRoomCameraControl> releases = [];
        foreach (SimulationOwnershipData.RevokedLock revokedLock in revokedLocks)
        {
            if (scenario.Manager.TryCreateControlRelease(revokedLock.EntityId, revokedLock.Lock, "disconnect_test", out MapRoomCameraControl releasePacket))
            {
                releases.Add(releasePacket);
            }
        }

        Assert.AreEqual(2, revokedLocks.Count, "The player simulated one docked camera and flew the other.");
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

        IDisposable lifecycleGate = await scenario.Manager.HoldGateAsync();
        Assert.IsTrue(scenario.Ownership.RevokeOwnerOfId(ScannerRoomScenarioFixture.CameraAId, out SimulationOwnershipData.PlayerLock revokedLock));
        Assert.IsTrue(scenario.Manager.TryCreateControlRelease(ScannerRoomScenarioFixture.CameraAId, revokedLock, "race_test", out MapRoomCameraControl release));

        Task<MapRoomCameraControl> newAcquire = scenario.ControlAsync(scenario.PlayerB, ScannerRoomScenarioFixture.CameraAId, 0, true);
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
        IDisposable destructiveLifecycleGate = await scenario.Manager.HoldGateAsync();
        Task<SimulationOwnershipResponse> request = scenario.RequestOwnershipAsync(scenario.PlayerA, ScannerRoomScenarioFixture.CameraAId, SimulationLockType.EXCLUSIVE);
        try
        {
            Task first = await Task.WhenAny(request, Task.Delay(100));
            Assert.AreNotSame(request, first, "A Stalker-style generic ownership request must wait for destructive camera cleanup.");
            Assert.IsNull(scenario.Ownership.GetPlayerForLock(ScannerRoomScenarioFixture.CameraAId));
        }
        finally
        {
            destructiveLifecycleGate.Dispose();
        }

        SimulationOwnershipResponse response = await request.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.IsTrue(response.LockAcquired);
        Assert.AreEqual(SimulationLockType.EXCLUSIVE, response.LockType);
        Assert.AreSame(scenario.PlayerA, scenario.Ownership.GetPlayerForLock(ScannerRoomScenarioFixture.CameraAId));
        ScannerRoomDiagnosticEntry diagnostic = scenario.Diagnostics.GetHistory().Single();
        Assert.AreEqual("camera_lock", diagnostic.EventName);
        Assert.AreEqual(ScannerRoomDiagnosticOutcome.ACCEPTED, diagnostic.Outcome);
        Assert.AreEqual("exclusive", diagnostic.Reason);
    }

    [TestMethod]
    [DataRow(SimulationLockType.EXCLUSIVE)]
    [DataRow(SimulationLockType.TRANSIENT)]
    public async Task GenericOwnershipRequestCannotTakeADockedCameraFromItsSimulatingPlayer(SimulationLockType requested)
    {
        ScannerRoomScenarioFixture scenario = new();
        Assert.IsTrue((await scenario.DockAsync(scenario.PlayerA, ScannerRoomScenarioFixture.CameraAId, 0, true)).Granted);

        SimulationOwnershipResponse response = await scenario.RequestOwnershipAsync(scenario.PlayerB, ScannerRoomScenarioFixture.CameraAId, requested);

        Assert.IsFalse(response.LockAcquired, "A docked camera changes hands by camera control only.");
        Assert.AreSame(scenario.PlayerA, scenario.Ownership.GetPlayerForLock(ScannerRoomScenarioFixture.CameraAId));
        ScannerRoomDiagnosticEntry diagnostic = scenario.Diagnostics.GetHistory().Last(entry => entry.EventName == "camera_lock");
        Assert.AreEqual(ScannerRoomDiagnosticOutcome.REJECTED, diagnostic.Outcome);
        Assert.AreEqual("control_required", diagnostic.Reason);
    }

    [TestMethod]
    public async Task LooseWorldCameraGenericExclusiveCanDowngradeToTransient()
    {
        ScannerRoomScenarioFixture scenario = new();
        Assert.IsTrue((await scenario.DockAsync(scenario.PlayerA, ScannerRoomScenarioFixture.CameraAId, 0, true)).Granted);
        Assert.IsTrue((await scenario.DockAsync(scenario.PlayerA, ScannerRoomScenarioFixture.CameraAId, 0, false)).Granted);
        Assert.IsFalse(scenario.IsDocked(ScannerRoomScenarioFixture.CameraAId));
        Assert.IsTrue((await scenario.RequestOwnershipAsync(scenario.PlayerA, ScannerRoomScenarioFixture.CameraAId, SimulationLockType.EXCLUSIVE)).LockAcquired);

        SimulationOwnershipResponse response = await scenario.RequestOwnershipAsync(scenario.PlayerA, ScannerRoomScenarioFixture.CameraAId, SimulationLockType.TRANSIENT);

        Assert.IsTrue(response.LockAcquired);
        Assert.IsTrue(scenario.Ownership.TryGetLock(ScannerRoomScenarioFixture.CameraAId, out SimulationOwnershipData.PlayerLock downgradedLock));
        Assert.AreSame(scenario.PlayerA, downgradedLock.Player);
        Assert.AreEqual(SimulationLockType.TRANSIENT, downgradedLock.LockType);
        Assert.IsFalse(scenario.HasController(ScannerRoomScenarioFixture.CameraAId));
    }

    [TestMethod]
    public async Task LooseWorldCameraCannotMintGenericTransientOwnership()
    {
        ScannerRoomScenarioFixture scenario = new();

        SimulationOwnershipResponse response = await scenario.RequestOwnershipAsync(scenario.PlayerA, ScannerRoomScenarioFixture.CameraAId, SimulationLockType.TRANSIENT);

        Assert.IsFalse(response.LockAcquired);
        Assert.IsNull(scenario.Ownership.GetPlayerForLock(ScannerRoomScenarioFixture.CameraAId));
    }

    [TestMethod]
    public async Task ActiveControllerCanRefreshGenericExclusiveOwnershipOfADockedCamera()
    {
        ScannerRoomScenarioFixture scenario = new();
        Assert.IsTrue((await scenario.DockAsync(scenario.PlayerA, ScannerRoomScenarioFixture.CameraAId, 0, true)).Granted);
        MapRoomCameraControl control = await scenario.ControlAsync(scenario.PlayerA, ScannerRoomScenarioFixture.CameraAId, 0, true);
        Assert.IsTrue(control.Granted);
        Assert.IsTrue(scenario.IsController(ScannerRoomScenarioFixture.CameraAId, scenario.PlayerA.SessionId));

        SimulationOwnershipResponse response = await scenario.RequestOwnershipAsync(scenario.PlayerA, ScannerRoomScenarioFixture.CameraAId, SimulationLockType.EXCLUSIVE);

        Assert.IsTrue(response.LockAcquired);
        Assert.AreSame(scenario.PlayerA, scenario.Ownership.GetPlayerForLock(ScannerRoomScenarioFixture.CameraAId));
        Assert.IsTrue(scenario.IsController(ScannerRoomScenarioFixture.CameraAId, scenario.PlayerA.SessionId));
    }

    [TestMethod]
    public async Task StaleActiveLifecycleCannotRemintExclusiveOwnership()
    {
        ScannerRoomScenarioFixture scenario = new();
        Assert.IsTrue((await scenario.DockAsync(scenario.PlayerA, ScannerRoomScenarioFixture.CameraAId, 0, true)).Granted);
        Assert.IsTrue((await scenario.ControlAsync(scenario.PlayerA, ScannerRoomScenarioFixture.CameraAId, 0, true)).Granted);
        Assert.IsTrue(scenario.Ownership.TryAcquire(ScannerRoomScenarioFixture.CameraAId, scenario.PlayerA, SimulationLockType.TRANSIENT));
        Assert.IsTrue(scenario.IsController(ScannerRoomScenarioFixture.CameraAId, scenario.PlayerA.SessionId));

        SimulationOwnershipResponse response = await scenario.RequestOwnershipAsync(scenario.PlayerA, ScannerRoomScenarioFixture.CameraAId, SimulationLockType.EXCLUSIVE);

        Assert.IsFalse(response.LockAcquired);
        Assert.IsTrue(scenario.Ownership.TryGetLock(ScannerRoomScenarioFixture.CameraAId, out SimulationOwnershipData.PlayerLock retainedLock));
        Assert.AreEqual(SimulationLockType.TRANSIENT, retainedLock.LockType);
    }

    [TestMethod]
    public async Task GenericScannerOwnershipRequestIsRejectedWhenCameraIsRemovedWhileWaitingForGate()
    {
        ScannerRoomScenarioFixture scenario = new();
        IDisposable destructiveLifecycleGate = await scenario.Manager.HoldGateAsync();
        Task<SimulationOwnershipResponse> request = scenario.RequestOwnershipAsync(scenario.PlayerA, ScannerRoomScenarioFixture.CameraAId, SimulationLockType.EXCLUSIVE);
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
        Assert.AreEqual(ScannerRoomDiagnosticOutcome.REJECTED, diagnostic.Outcome);
        Assert.AreEqual("removed", diagnostic.Reason);
    }

    [TestMethod]
    public async Task DelayedGenericScannerOwnershipRequestIsRejectedByLifecycleTombstoneAfterRemoval()
    {
        ScannerRoomScenarioFixture scenario = new();
        await scenario.DestroyAsync(scenario.PlayerB, ScannerRoomScenarioFixture.CameraAId);
        Assert.IsFalse(scenario.EntityRegistry.GetEntityById(ScannerRoomScenarioFixture.CameraAId).HasValue);

        SimulationOwnershipResponse response = await scenario.RequestOwnershipAsync(scenario.PlayerA, ScannerRoomScenarioFixture.CameraAId, SimulationLockType.EXCLUSIVE);

        Assert.IsFalse(response.LockAcquired);
        Assert.IsNull(scenario.Ownership.GetPlayerForLock(ScannerRoomScenarioFixture.CameraAId));
        ScannerRoomDiagnosticEntry diagnostic = scenario.Diagnostics.GetHistory().Single();
        Assert.AreEqual("camera_lock", diagnostic.EventName);
        Assert.AreEqual(ScannerRoomDiagnosticOutcome.REJECTED, diagnostic.Outcome);
        Assert.AreEqual("removed", diagnostic.Reason);
    }

    [TestMethod]
    public async Task CameraThatWentWithItsDeconstructedRoomIsRefusedALock()
    {
        ScannerRoomScenarioFixture scenario = new();
        Assert.IsTrue((await scenario.DockAsync(scenario.PlayerA, ScannerRoomScenarioFixture.CameraAId, 0, true)).Granted);
        Assert.IsTrue(scenario.EntityRegistry.RemoveEntity(ScannerRoomScenarioFixture.RoomId).HasValue);
        await scenario.CleanupAsync(scenario.Room);

        SimulationOwnershipResponse response = await scenario.RequestOwnershipAsync(scenario.PlayerB, ScannerRoomScenarioFixture.CameraAId, SimulationLockType.EXCLUSIVE);

        Assert.IsFalse(response.LockAcquired, "The id is remembered as a camera's: a request that crossed the deconstruction is not granted as a lock on an unknown entity.");
        Assert.IsNull(scenario.Ownership.GetPlayerForLock(ScannerRoomScenarioFixture.CameraAId));
        ScannerRoomDiagnosticEntry diagnostic = scenario.Diagnostics.GetHistory().Last();
        Assert.AreEqual("camera_lock", diagnostic.EventName);
        Assert.AreEqual(ScannerRoomDiagnosticOutcome.REJECTED, diagnostic.Outcome);
        Assert.AreEqual("removed", diagnostic.Reason);
    }

    [TestMethod]
    public void ReleaseFactoryCoversAssociationlessLooseWorldCamera()
    {
        ScannerRoomScenarioFixture scenario = new();
        Assert.IsTrue(scenario.Ownership.TryAcquire(ScannerRoomScenarioFixture.CameraCId, scenario.PlayerA, SimulationLockType.EXCLUSIVE));
        Assert.IsTrue(scenario.Ownership.RevokeOwnerOfId(ScannerRoomScenarioFixture.CameraCId, out SimulationOwnershipData.PlayerLock revokedLock));

        Assert.IsTrue(scenario.Manager.TryCreateControlRelease(ScannerRoomScenarioFixture.CameraCId, revokedLock, "loose_test", out MapRoomCameraControl release));
        Assert.IsFalse(release.MapRoomId.HasValue);
        Assert.AreEqual(-1, release.CameraIndex);
        Assert.IsFalse(release.IsControlling);
        Assert.AreEqual(scenario.PlayerA.SessionId, release.ControllerSessionId);
    }
}
