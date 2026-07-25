using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nitrox.Model.Core;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.Packets;
using NitroxClient.GameLogic;

namespace Nitrox.Test.Client.GameLogic;

[TestClass]
public sealed class MapRoomCameraBootstrapStateCacheTest
{
    [TestMethod]
    public void SeparateSlotsSurviveGlobalRevisionOrderingUntilFreshRoomSpawns()
    {
        NitroxId roomId = new();
        NitroxId leftCameraId = new();
        NitroxId rightCameraId = new();
        MapRoomCameraBootstrapStateCache cache = new();

        Assert.IsTrue(cache.RetainDock(ServerDock(leftCameraId, roomId, 0, 1)));
        Assert.IsTrue(cache.RetainDock(ServerDock(rightCameraId, roomId, 1, 2)));

        IReadOnlyList<MapRoomCameraDock> retained = cache.GetDocks(roomId);
        Assert.AreEqual(2, retained.Count);
        Assert.AreEqual(leftCameraId, retained[0].CameraId);
        Assert.AreEqual(rightCameraId, retained[1].CameraId);
    }

    [TestMethod]
    public void ReversedCrossSlotArrivalReplaysInCanonicalRevisionOrder()
    {
        NitroxId roomId = new();
        NitroxId leftCameraId = new();
        NitroxId rightCameraId = new();
        MapRoomCameraBootstrapStateCache cache = new();

        Assert.IsTrue(cache.RetainDock(ServerDock(rightCameraId, roomId, 1, 2)));
        Assert.IsTrue(cache.RetainDock(ServerDock(leftCameraId, roomId, 0, 1)));

        IReadOnlyList<MapRoomCameraDock> replay = cache.GetDocks(roomId);
        Assert.AreEqual(1L, replay[0].Revision);
        Assert.AreEqual(leftCameraId, replay[0].CameraId);
        Assert.AreEqual(2L, replay[1].Revision);
        Assert.AreEqual(rightCameraId, replay[1].CameraId);
    }

    [TestMethod]
    public void NewestStateWinsPerSlotAndOlderArrivalCannotRedockCamera()
    {
        NitroxId roomId = new();
        NitroxId cameraId = new();
        MapRoomCameraBootstrapStateCache cache = new();

        Assert.IsTrue(cache.RetainDock(ServerDock(cameraId, roomId, 0, 5, isDocked: false)));
        Assert.IsFalse(cache.RetainDock(ServerDock(cameraId, roomId, 0, 4)));

        Assert.IsTrue(cache.TryGetDock(roomId, 0, out MapRoomCameraDock retained));
        Assert.AreEqual(5L, retained.Revision);
        Assert.IsFalse(retained.IsDocked);
    }

    [TestMethod]
    public void ControlAcquireWaitsForCanonicalObjectAndReleaseCancelsReplay()
    {
        NitroxId cameraId = new();
        MapRoomCameraBootstrapStateCache cache = new();
        MapRoomCameraControl acquire = new(cameraId, Optional.Empty, -1, true, false,
            isServerResponse: true, granted: true, controllerSessionId: 2);

        cache.RetainControl(acquire);
        Assert.AreEqual(1, cache.GetPendingControls().Count);

        cache.RetainControl(new MapRoomCameraControl(cameraId, Optional.Empty, -1, false, false,
            isServerResponse: true, granted: true, controllerSessionId: 2));

        Assert.AreEqual(0, cache.GetPendingControls().Count);
    }

    [TestMethod]
    public void DeniedAttemptCannotEraseGrantedRemoteControl()
    {
        NitroxId cameraId = new();
        MapRoomCameraBootstrapStateCache cache = new();
        cache.RetainControl(new MapRoomCameraControl(cameraId, Optional.Empty, -1, true, false,
            isServerResponse: true, granted: true, controllerSessionId: 2));

        cache.RetainControl(new MapRoomCameraControl(cameraId, Optional.Empty, -1, true, false,
            isServerResponse: true, granted: false, controllerSessionId: 2));

        Assert.AreEqual(1, cache.GetPendingControls().Count);
    }

    [TestMethod]
    public void EqualDockRevisionMergesNewerComponentAndLightPayloads()
    {
        NitroxId roomId = new();
        NitroxId cameraId = new();
        MapRoomCameraBootstrapStateCache cache = new();
        Assert.IsTrue(cache.RetainDock(new MapRoomCameraDock(cameraId, roomId, 0, 4,
            isServerResponse: true, granted: true, cameraNumber: 1, lightOn: false,
            lightRevision: 2, energy: 80f, health: 300f, componentRevision: 3)));

        Assert.IsTrue(cache.RetainDock(new MapRoomCameraDock(cameraId, roomId, 0, 4,
            isServerResponse: true, granted: true, cameraNumber: 1, lightOn: true,
            lightRevision: 5, energy: 60f, health: 250f, componentRevision: 7)));

        Assert.IsTrue(cache.TryGetDock(roomId, 0, out MapRoomCameraDock retained));
        Assert.IsTrue(retained.LightOn);
        Assert.AreEqual(5L, retained.LightRevision);
        Assert.AreEqual(60f, retained.Energy);
        Assert.AreEqual(250f, retained.Health);
        Assert.AreEqual(7L, retained.ComponentRevision);
    }

    [TestMethod]
    public void DeferredRetryIncludesOnlyUnappliedTopologyStates()
    {
        NitroxId roomId = new();
        NitroxId leftCameraId = new();
        NitroxId rightCameraId = new();
        MapRoomCameraBootstrapStateCache cache = new();
        MapRoomCameraDock left = ServerDock(leftCameraId, roomId, 0, 1);
        MapRoomCameraDock right = ServerDock(rightCameraId, roomId, 1, 2);
        cache.RetainDock(left);
        cache.RetainDock(right);
        cache.MarkDockApplied(left);

        IReadOnlyList<MapRoomCameraDock> pending = cache.GetPendingDocks(roomId);
        Assert.AreEqual(1, pending.Count);
        Assert.AreEqual(rightCameraId, pending[0].CameraId);

        Assert.IsTrue(cache.RetainDock(ServerDock(leftCameraId, roomId, 0, 3, isDocked: false)));
        Assert.AreEqual(2, cache.GetPendingDocks(roomId).Count);
    }

    [TestMethod]
    public void FailedReapplyMakesStatePendingForReplacementRoomObject()
    {
        NitroxId roomId = new();
        MapRoomCameraDock dock = ServerDock(new NitroxId(), roomId, 0, 7);
        MapRoomCameraBootstrapStateCache cache = new();
        cache.RetainDock(dock);
        cache.MarkDockApplied(dock);
        Assert.AreEqual(0, cache.GetPendingDocks(roomId).Count);

        cache.MarkDockPending(dock);

        Assert.AreEqual(1, cache.GetPendingDocks(roomId).Count);
    }

    private static MapRoomCameraDock ServerDock(NitroxId cameraId, NitroxId roomId, int slot,
        long revision, bool isDocked = true) =>
        new(cameraId, roomId, slot, revision, isServerResponse: true, granted: true, isDocked: isDocked);
}
