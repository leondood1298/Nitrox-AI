using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Bases;
using Nitrox.Model.Subnautica.Packets;
using NitroxClient.GameLogic;

namespace Nitrox.Test.Client.GameLogic;

[TestClass]
public sealed class MapRoomCameraRestoreStateCacheTest
{
    [TestMethod]
    public void RoomBeforeLooseCameraRetainsFullCanonicalStateUntilObjectSpawns()
    {
        NitroxId cameraId = new();
        MapRoomCameraRestoreStateCache cache = new();
        cache.Retain(new MapRoomCameraRecord(cameraId, 7, true, 11, 63.5f, 125f, 19));

        Assert.IsFalse(cache.TryTake(cameraId, false, out _));
        Assert.IsTrue(cache.HasPending(cameraId));
        Assert.IsTrue(cache.TryTake(cameraId, true, out MapRoomCameraRestoreStateCache.PendingCameraState state));

        Assert.AreEqual(7, state.Record!.CameraNumber);
        Assert.IsTrue(state.Light!.On);
        Assert.AreEqual(11L, state.Light.Revision);
        Assert.AreEqual(63.5f, state.Component!.Energy);
        Assert.AreEqual(125f, state.Component.Health);
        Assert.AreEqual(19L, state.Component.Revision);
        Assert.IsFalse(cache.HasPending(cameraId));
        Assert.IsTrue(cache.HasKnownState(cameraId));
    }

    [TestMethod]
    public void CameraBeforeRoomAllowsCanonicalStateToApplyImmediately()
    {
        NitroxId cameraId = new();
        MapRoomCameraRestoreStateCache cache = new();
        cache.Retain(new MapRoomCameraRecord(cameraId, 2, false, 4, 99.65f, 100f, 38));

        Assert.IsTrue(cache.TryTake(cameraId, true, out MapRoomCameraRestoreStateCache.PendingCameraState state));
        Assert.AreEqual(2, state.Record!.CameraNumber);
        Assert.AreEqual(99.65f, state.Component!.Energy);
        Assert.AreEqual(100f, state.Component.Health);

        Assert.IsTrue(cache.MarkPendingForSpawn(cameraId));
        Assert.IsTrue(cache.HasPending(cameraId));
        Assert.IsTrue(cache.TryTake(cameraId, true, out MapRoomCameraRestoreStateCache.PendingCameraState respawnState));
        Assert.AreEqual(99.65f, respawnState.Component!.Energy);
    }

    [TestMethod]
    public void NewestDeferredComponentStateWinsOverPrefabAndStaleState()
    {
        NitroxId cameraId = new();
        MapRoomCameraRestoreStateCache cache = new();
        cache.Retain(new MapRoomCameraRecord(cameraId, 1, false, 1, 99.65f, 100f, 38));
        cache.Retain(new MapRoomCameraComponentState(cameraId, 75f, 90f, 40, true, true));
        cache.Retain(new MapRoomCameraComponentState(cameraId, 0f, 400f, 39, true, true));

        Assert.IsTrue(cache.TryTake(cameraId, true, out MapRoomCameraRestoreStateCache.PendingCameraState state));
        Assert.AreEqual(75f, state.Component!.Energy);
        Assert.AreEqual(90f, state.Component.Health);
        Assert.AreEqual(40L, state.Component.Revision);
    }

    [TestMethod]
    public void AppliedNewerStateRemainsCanonicalWhenOlderRoomRecordArrivesLater()
    {
        NitroxId cameraId = new();
        MapRoomCameraRestoreStateCache cache = new();
        cache.Retain(new MapRoomCameraRecord(cameraId, 1, false, 38, 99.65f, 100f, 38));
        cache.Retain(new MapRoomCameraLight(cameraId, true, 40, true, true));
        cache.Retain(new MapRoomCameraComponentState(cameraId, 75f, 90f, 40, true, true));

        Assert.IsTrue(cache.TryTake(cameraId, true, out _));
        cache.Retain(new MapRoomCameraRecord(cameraId, 1, false, 38, 99.65f, 100f, 38));

        Assert.IsFalse(cache.HasPending(cameraId));
        Assert.IsTrue(cache.MarkPendingForSpawn(cameraId));
        Assert.IsTrue(cache.TryTake(cameraId, true, out MapRoomCameraRestoreStateCache.PendingCameraState state));
        Assert.IsTrue(state.Light!.On);
        Assert.AreEqual(40L, state.Light.Revision);
        Assert.AreEqual(75f, state.Component!.Energy);
        Assert.AreEqual(90f, state.Component.Health);
        Assert.AreEqual(40L, state.Component.Revision);
    }

    [TestMethod]
    public void NewerDockStateRemainsCanonicalAcrossCameraRespawn()
    {
        NitroxId cameraId = new();
        MapRoomCameraRestoreStateCache cache = new();
        cache.Retain(new MapRoomCameraRecord(cameraId, 1, false, 38, 99.65f, 100f, 38));
        Assert.IsTrue(cache.TryTake(cameraId, true, out _));

        cache.Retain(new MapRoomCameraRecord(cameraId, 2, true, 41, 66f, 325f, 42));
        Assert.IsTrue(cache.TryTake(cameraId, true, out _));
        Assert.IsTrue(cache.MarkPendingForSpawn(cameraId));

        Assert.IsTrue(cache.TryTake(cameraId, true, out MapRoomCameraRestoreStateCache.PendingCameraState state));
        Assert.AreEqual(2, state.Record!.CameraNumber);
        Assert.IsTrue(state.Light!.On);
        Assert.AreEqual(41L, state.Light.Revision);
        Assert.AreEqual(66f, state.Component!.Energy);
        Assert.AreEqual(325f, state.Component.Health);
        Assert.AreEqual(42L, state.Component.Revision);
    }

    [TestMethod]
    public void OlderRoomRecordCannotRollBackNewerDockCameraNumber()
    {
        NitroxId cameraId = new();
        MapRoomCameraRestoreStateCache cache = new();
        cache.Retain(new MapRoomCameraRecord(cameraId, 2, true, 41, 66f, 325f, 42));
        Assert.IsTrue(cache.TryTake(cameraId, true, out _));

        cache.Retain(new MapRoomCameraRecord(cameraId, 1, false, 38, 99.65f, 100f, 38));

        Assert.IsFalse(cache.HasPending(cameraId));
        Assert.IsTrue(cache.MarkPendingForSpawn(cameraId));
        Assert.IsTrue(cache.TryTake(cameraId, true, out MapRoomCameraRestoreStateCache.PendingCameraState state));
        Assert.AreEqual(2, state.Record!.CameraNumber);
        Assert.AreEqual(41L, state.Light!.Revision);
        Assert.AreEqual(42L, state.Component!.Revision);
    }

    [TestMethod]
    public void EqualRevisionRoomSnapshotCannotRollBackLiveDockCameraNumber()
    {
        NitroxId cameraId = new();
        MapRoomCameraRestoreStateCache cache = new();
        cache.Retain(new MapRoomCameraRecord(cameraId, 1, false, 38, 99.65f, 100f, 38));
        Assert.IsTrue(cache.TryTake(cameraId, true, out _));

        cache.Retain(new MapRoomCameraRecord(cameraId, 2, false, 38, 99.65f, 100f, 38),
            preferCameraNumber: true);
        Assert.IsTrue(cache.TryTake(cameraId, true, out _));
        cache.Retain(new MapRoomCameraRecord(cameraId, 1, false, 38, 99.65f, 100f, 38));
        Assert.IsTrue(cache.TryTake(cameraId, true, out MapRoomCameraRestoreStateCache.PendingCameraState equalState));
        Assert.IsNull(equalState.Record);

        Assert.IsTrue(cache.MarkPendingForSpawn(cameraId));
        Assert.IsTrue(cache.TryTake(cameraId, true, out MapRoomCameraRestoreStateCache.PendingCameraState respawnState));
        Assert.AreEqual(2, respawnState.Record!.CameraNumber);
    }

    [TestMethod]
    public void LiveLightUpdateDoesNotReapplyDurableComponentState()
    {
        NitroxId cameraId = new();
        MapRoomCameraRestoreStateCache cache = new();
        cache.Retain(new MapRoomCameraRecord(cameraId, 1, false, 38, 99.65f, 100f, 38));
        Assert.IsTrue(cache.TryTake(cameraId, true, out _));

        cache.Retain(new MapRoomCameraLight(cameraId, true, 39, true, true));

        Assert.IsTrue(cache.TryTake(cameraId, true, out MapRoomCameraRestoreStateCache.PendingCameraState state));
        Assert.IsNull(state.Record);
        Assert.IsTrue(state.Light!.On);
        Assert.IsNull(state.Component);
    }

    [TestMethod]
    public void LiveComponentUpdateDoesNotReapplyDurableLightState()
    {
        NitroxId cameraId = new();
        MapRoomCameraRestoreStateCache cache = new();
        cache.Retain(new MapRoomCameraRecord(cameraId, 1, false, 38, 99.65f, 100f, 38));
        Assert.IsTrue(cache.TryTake(cameraId, true, out _));

        cache.Retain(new MapRoomCameraComponentState(cameraId, 75f, 90f, 39, true, true));

        Assert.IsTrue(cache.TryTake(cameraId, true, out MapRoomCameraRestoreStateCache.PendingCameraState state));
        Assert.IsNull(state.Record);
        Assert.IsNull(state.Light);
        Assert.AreEqual(75f, state.Component!.Energy);
    }

    [DataTestMethod]
    [DataRow(false, false, false, false)]
    [DataRow(true, false, false, true)]
    [DataRow(false, true, false, true)]
    [DataRow(false, false, true, true)]
    [DataRow(true, true, true, true)]
    public void DefaultComponentBroadcastIsSuppressedUntilRestoreCompletes(bool statePending,
        bool batteryInitializing, bool restoreBarrier, bool expected)
    {
        Assert.AreEqual(expected, MapRoomCameras.ShouldSuppressComponentBroadcast(statePending,
            batteryInitializing, restoreBarrier));
    }

    [DataTestMethod]
    [DataRow(false, false, false, false)]
    [DataRow(true, false, false, true)]
    [DataRow(true, true, false, false)]
    [DataRow(true, true, true, true)]
    [DataRow(false, false, true, false)]
    public void RestoreBarrierCoversUnknownInitialCameraAndKnownRespawns(bool isMapRoomCamera,
        bool initialSyncCompleted, bool hasKnownState, bool expected)
    {
        Assert.AreEqual(expected, MapRoomCameras.ShouldCreateCameraRestoreBarrier(isMapRoomCamera,
            initialSyncCompleted, hasKnownState));
    }

    [DataTestMethod]
    [DataRow(false, false, false)]
    [DataRow(false, true, false)]
    [DataRow(true, true, false)]
    [DataRow(true, false, true)]
    public void UnknownCameraBarrierReleasesOnlyAfterInitialSyncProvesNoCanonicalState(
        bool initialSyncCompleted, bool hasKnownState, bool expected)
    {
        Assert.AreEqual(expected, MapRoomCameras.ShouldReleaseUnknownCameraRestoreBarrier(
            initialSyncCompleted, hasKnownState));
    }

    [DataTestMethod]
    [DataRow(true, true, true, true, true)]
    [DataRow(true, true, true, false, false)]
    [DataRow(true, true, false, false, false)]
    [DataRow(true, false, false, false, true)]
    [DataRow(false, false, true, true, false)]
    public void BatteryInitializationCleanupCannotReleaseAnIncompleteOrStaleGeneration(bool currentGeneration,
        bool cameraAlive, bool batteryAvailable, bool canonicalEnergyApplied, bool expected)
    {
        Assert.AreEqual(expected, MapRoomCameras.ShouldClearCameraBatteryInitialization(currentGeneration,
            cameraAlive, batteryAvailable, canonicalEnergyApplied));
    }
}
