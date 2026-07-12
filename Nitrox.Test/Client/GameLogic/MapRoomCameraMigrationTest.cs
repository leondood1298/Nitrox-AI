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
}
