using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nitrox.Model.DataStructures;
using NitroxClient.GameLogic;
using UnityEngine;

namespace Nitrox.Test.Client.GameLogic;

[TestClass]
public sealed class MapRoomCameraMigrationTest
{
    [TestMethod]
    public void DefaultCameraIdsAreStableAndSlotSpecific()
    {
        NitroxId room = new();
        Vector3 leftDock = new(-2f, 0f, 1f);
        Vector3 rightDock = new(2f, 0f, 1f);

        NitroxId first = MapRoomCameras.GetDeterministicCameraId(room, leftDock);

        Assert.AreEqual(first, MapRoomCameras.GetDeterministicCameraId(room, leftDock));
        Assert.AreNotEqual(first, MapRoomCameras.GetDeterministicCameraId(room, rightDock));
        Assert.AreNotEqual(first, MapRoomCameras.GetDeterministicCameraId(new NitroxId(), leftDock));
    }

    [TestMethod]
    public void RestoredPrefabCameraOnlySurvivesAnAuthoritativelyOccupiedSlot()
    {
        Assert.IsFalse(MapRoomCameras.ShouldRestoreDefaultCamera(null));
        Assert.IsTrue(MapRoomCameras.ShouldRestoreDefaultCamera(new NitroxId()));
    }

    [TestMethod]
    public void FreshAndRestoredRoomsWaitUntilEachDefaultSlotIsSettled()
    {
        Assert.IsFalse(MapRoomCameras.ExpectedDockCamerasReady([true, false]));
        Assert.IsTrue(MapRoomCameras.ExpectedDockCamerasReady([true, true]));

        NitroxId occupied = new();
        Assert.IsFalse(MapRoomCameras.ExpectedAuthoritativeDockCamerasReady(
            [true, false], [false, false], [occupied, null]));
        Assert.IsTrue(MapRoomCameras.ExpectedAuthoritativeDockCamerasReady(
            [true, true], [false, false], [occupied, null]));
        Assert.IsTrue(MapRoomCameras.ExpectedAuthoritativeDockCamerasReady(
            [true, false], [false, true], [occupied, null]));
        Assert.IsFalse(MapRoomCameras.ExpectedAuthoritativeDockCamerasReady(
            [false, false], [false, false], [null, null]));
        Assert.IsTrue(MapRoomCameras.ExpectedAuthoritativeDockCamerasReady(
            [false, false], [true, true], [null, null]));
    }

    [TestMethod]
    public void CanonicalDockReplaySkipsOnlyAnAlreadyDockedMatchingCamera()
    {
        Assert.IsFalse(MapRoomCameras.ShouldApplyPhysicalDockTransition(true, true, true));
        Assert.IsTrue(MapRoomCameras.ShouldApplyPhysicalDockTransition(true, true, false));
        Assert.IsTrue(MapRoomCameras.ShouldApplyPhysicalDockTransition(true, false, true));
        Assert.IsTrue(MapRoomCameras.ShouldApplyPhysicalDockTransition(false, true, true));
        Assert.IsFalse(MapRoomCameras.ShouldApplyPhysicalDockTransition(false, false, false));
    }

    [TestMethod]
    public void CanonicalCameraIdentityNeverOverwritesAnotherRegisteredObject()
    {
        Assert.IsTrue(MapRoomCameras.CanAssignCameraIdentity(false, false, false, false));
        Assert.IsTrue(MapRoomCameras.CanAssignCameraIdentity(true, true, true, true));
        Assert.IsFalse(MapRoomCameras.CanAssignCameraIdentity(true, false, false, false));
        Assert.IsFalse(MapRoomCameras.CanAssignCameraIdentity(false, false, true, false));
    }
}
