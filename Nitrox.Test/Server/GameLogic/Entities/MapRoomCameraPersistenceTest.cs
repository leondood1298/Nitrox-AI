using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nitrox.Model.DataStructures;
using Nitrox.Model.DataStructures.Unity;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Bases;
using Nitrox.Server.Subnautica.Models.GameLogic.Entities;

namespace Nitrox.Test.Server.GameLogic.Entities;

[TestClass]
public sealed class MapRoomCameraPersistenceTest
{
    [TestMethod]
    public void RestoresOnlyMissingRegistrationAndUsesItsNumberedSlot()
    {
        NitroxId existingCamera = new();
        NitroxId orphanedCamera = new();
        MapRoomEntity mapRoom = new(new NitroxId(), new NitroxId(), new NitroxInt3())
        {
            CameraRegistry =
            [
                new MapRoomCameraRecord(existingCamera, 1),
                new MapRoomCameraRecord(orphanedCamera, 2)
            ]
        };

        int restored = MapRoomCameraPersistence.RestoreOrphanedRegistrations(mapRoom, id => id == existingCamera);

        Assert.AreEqual(1, restored);
        Assert.IsNull(mapRoom.LeftDockCameraId);
        Assert.AreEqual(orphanedCamera, mapRoom.RightDockCameraId);
    }

    [TestMethod]
    public void DoesNotDuplicateAlreadyDockedRegistration()
    {
        NitroxId camera = new();
        MapRoomEntity mapRoom = new(new NitroxId(), new NitroxId(), new NitroxInt3())
        {
            CameraRegistry = [new MapRoomCameraRecord(camera, 1)]
        };
        mapRoom.SetDockedCamera(0, camera);

        Assert.AreEqual(0, MapRoomCameraPersistence.RestoreOrphanedRegistrations(mapRoom, _ => false));
        Assert.AreEqual(camera, mapRoom.LeftDockCameraId);
        Assert.IsNull(mapRoom.RightDockCameraId);
    }
}
