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

    [TestMethod]
    public void PickupStyleClearFindsSlotAndAdvancesRevision()
    {
        MapRoomEntity room = CreateRoom();
        NitroxId cameraId = new();
        room.SetDockedCamera(1, cameraId);

        Assert.IsTrue(room.TryClearDockedCamera(cameraId, out int dockingIndex));
        Assert.AreEqual(1, dockingIndex);
        Assert.IsNull(room.RightDockCameraId);
        Assert.AreEqual(2, room.DockingRevision);
        Assert.IsFalse(room.TryClearDockedCamera(cameraId, out dockingIndex));
        Assert.AreEqual(-1, dockingIndex);
    }

    [TestMethod]
    public void CameraNumbersAreStableAndUnique()
    {
        MapRoomEntity room = CreateRoom();
        NitroxId first = new();
        NitroxId second = new();

        Assert.AreEqual(1, room.GetOrAssignCameraNumber(first, 1));
        Assert.AreEqual(2, room.GetOrAssignCameraNumber(second, 1));
        Assert.AreEqual(1, room.GetOrAssignCameraNumber(first, 2));
        Assert.AreEqual(2, room.CameraRegistry.Count);
    }

    [TestMethod]
    public void RemovingDeadCameraClearsDockAndRegistry()
    {
        MapRoomEntity room = CreateRoom();
        NitroxId cameraId = new();
        room.SetDockedCamera(0, cameraId);
        room.GetOrAssignCameraNumber(cameraId, 1);

        Assert.IsTrue(room.RemoveCamera(cameraId, out int dockingIndex));
        Assert.AreEqual(0, dockingIndex);
        Assert.IsNull(room.LeftDockCameraId);
        Assert.IsNull(room.GetCameraRecord(cameraId));
        Assert.AreEqual(2, room.DockingRevision);
    }

    private static MapRoomEntity CreateRoom() => new(new NitroxId(), new NitroxId(), new NitroxInt3());
}
