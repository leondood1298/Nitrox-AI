using Nitrox.Model.DataStructures;
using Nitrox.Model.DataStructures.Unity;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Bases;

namespace Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities;

[TestClass]
public sealed class MapRoomEntityDockingTest
{
    [TestMethod]
    public void DockAndMatchingUndockAdvanceRevision()
    {
        MapRoomEntity room = CreateRoom();
        NitroxId cameraId = new();

        room.SetDockedCamera(0, cameraId);
        bool cleared = room.TryClearDockedCamera(0, cameraId);

        Assert.IsTrue(cleared);
        Assert.IsNull(room.LeftDockCameraId);
        Assert.AreEqual(2, room.DockingRevision);
    }

    [TestMethod]
    public void WrongCameraCannotClearDockSlot()
    {
        MapRoomEntity room = CreateRoom();
        NitroxId cameraId = new();
        room.SetDockedCamera(1, cameraId);

        bool cleared = room.TryClearDockedCamera(1, new NitroxId());

        Assert.IsFalse(cleared);
        Assert.AreEqual(cameraId, room.RightDockCameraId);
        Assert.AreEqual(1, room.DockingRevision);
    }

    [TestMethod]
    public void CameraAssociationCoversBothSlots()
    {
        MapRoomEntity room = CreateRoom();
        NitroxId left = new();
        NitroxId right = new();
        room.SetDockedCamera(0, left);
        room.SetDockedCamera(1, right);

        Assert.IsTrue(room.IsCameraDocked(left));
        Assert.IsTrue(room.IsCameraDocked(right));
        Assert.IsFalse(room.IsCameraDocked(new NitroxId()));
    }

    private static MapRoomEntity CreateRoom() => new(new NitroxId(), new NitroxId(), new NitroxInt3());
}
