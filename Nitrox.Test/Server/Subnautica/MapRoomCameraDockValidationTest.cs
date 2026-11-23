using System.Threading.Tasks;
using Nitrox.Model.DataStructures;
using Nitrox.Model.DataStructures.Unity;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities;
using Nitrox.Model.Subnautica.Packets;
using Nitrox.Server.Subnautica.Models.GameLogic;

namespace Nitrox.Test.Server.Subnautica;

[TestClass]
public sealed class MapRoomCameraDockValidationTest
{
    [TestMethod]
    [DataRow(true, false, true)]
    [DataRow(false, true, true)]
    [DataRow(true, true, true)]
    [DataRow(false, false, false)]
    public void AcceptsWorldOrRestoredRegisteredCamera(bool validWorldCamera, bool registeredCamera, bool expected)
    {
        Assert.AreEqual(expected, ScannerRoomManager.IsKnownCamera(validWorldCamera, registeredCamera));
    }

    [TestMethod]
    [DataRow(true, true, true, 0, true)]
    [DataRow(true, true, true, 1, true)]
    [DataRow(false, true, true, 0, false)]
    [DataRow(true, false, true, 0, false)]
    [DataRow(true, true, false, 0, false)]
    [DataRow(true, true, true, 2, false)]
    public void BootstrapRequiresOwnedEmptyRestoredSlot(bool isDocked, bool senderOwnsRoom, bool slotAvailable, int registeredCameraCount, bool expected)
    {
        Assert.AreEqual(expected, ScannerRoomManager.CanBootstrapRestoredCamera(isDocked, senderOwnsRoom, slotAvailable, registeredCameraCount));
    }

    [TestMethod]
    [DataRow(SimulationLockType.EXCLUSIVE, true, true)]
    [DataRow(SimulationLockType.EXCLUSIVE, false, false)]
    [DataRow(SimulationLockType.TRANSIENT, true, false)]
    [DataRow(SimulationLockType.TRANSIENT, false, false)]
    public void DockingPreservesOnlyTheControllersExclusiveLock(SimulationLockType lockType, bool holderIsActiveController, bool expected)
    {
        Assert.AreEqual(expected, ScannerRoomManager.ShouldPreserveControlLock(lockType, holderIsActiveController));
    }

    [TestMethod]
    [DataRow(true, false, false, true)]
    [DataRow(true, true, true, true)]
    [DataRow(true, true, false, false)]
    [DataRow(false, true, false, true)]
    public void OnlyAWorldCameraOrARecordOnlyCameraCanBeDocked(bool isDocked, bool entityExists, bool validWorldCamera, bool expected)
    {
        Assert.AreEqual(expected, ScannerRoomManager.CanBeDocked(isDocked, entityExists, validWorldCamera));
    }

    [TestMethod]
    public async Task DockReportThatCrossedAPickupIsRefused()
    {
        // The room owner's client saw the camera drift into the dock just as another player picked it up.
        ScannerRoomScenarioFixture scenario = new();
        NitroxId cameraId = ScannerRoomScenarioFixture.CameraAId;
        Assert.IsTrue((await scenario.DockAsync(scenario.PlayerA, cameraId, 0, true)).Granted);
        Assert.IsTrue((await scenario.DockAsync(scenario.PlayerA, cameraId, 0, false)).Granted);
        Assert.IsTrue(scenario.Ownership.TryAcquire(scenario.Room.Id, scenario.PlayerA, SimulationLockType.TRANSIENT));
        await scenario.PickupAsync(scenario.PlayerB, cameraId);
        Assert.IsNotNull(scenario.Room.GetCameraRecord(cameraId), "The record that used to let the dock through is still there.");

        MapRoomCameraDock response = await scenario.DockAsync(scenario.PlayerA, cameraId, 0, true, establishAuthority: false);

        Assert.IsFalse(response.Granted, "The camera would be docked and in an inventory at once.");
        Assert.IsNull(scenario.Room.LeftDockCameraId);
    }

    [TestMethod]
    public async Task RoomOwnerCannotDockTheIdOfAnotherEntity()
    {
        ScannerRoomScenarioFixture scenario = new();
        NitroxId vehicleId = new("00000000-0000-0000-0000-000000000900");
        scenario.EntityRegistry.AddEntity(new WorldEntity(new NitroxVector3(10f, -20f, 0f), NitroxQuaternion.Identity, NitroxVector3.One,
            new NitroxTechType("Seamoth"), 0, "seamoth-class-id", false, vehicleId, null));
        Assert.IsTrue(scenario.Ownership.TryAcquire(vehicleId, scenario.PlayerB, SimulationLockType.EXCLUSIVE));
        Assert.IsTrue(scenario.Ownership.TryAcquire(scenario.Room.Id, scenario.PlayerA, SimulationLockType.TRANSIENT));

        MapRoomCameraDock response = await scenario.DockAsync(scenario.PlayerA, vehicleId, 0, true, establishAuthority: false);

        Assert.IsFalse(response.Granted);
        Assert.IsNull(scenario.Room.LeftDockCameraId);
        Assert.IsNull(scenario.Room.GetCameraRecord(vehicleId));
        Assert.IsTrue(scenario.Ownership.TryGetLock(vehicleId, out SimulationOwnershipData.PlayerLock pilotLock));
        Assert.AreSame(scenario.PlayerB, pilotLock.Player, "The pilot keeps the vehicle.");
        Assert.IsFalse(scenario.Manager.IsScannerCamera(vehicleId),
            "An id that is marked as a camera is refused every later simulation request, so the vehicle could not be piloted again.");
        Assert.IsTrue((await scenario.RequestOwnershipAsync(scenario.PlayerB, vehicleId, SimulationLockType.TRANSIENT)).LockAcquired);
    }

    [TestMethod]
    public async Task RoomOwnerCanStillDockADefaultCameraThatExistsOnlyAsARecord()
    {
        ScannerRoomScenarioFixture scenario = new();
        NitroxId defaultCameraId = new("00000000-0000-0000-0000-000000000299");
        Assert.IsTrue(scenario.Ownership.TryAcquire(scenario.Room.Id, scenario.PlayerA, SimulationLockType.TRANSIENT));

        MapRoomCameraDock response = await scenario.DockAsync(scenario.PlayerA, defaultCameraId, 0, true, establishAuthority: false);

        Assert.IsTrue(response.Granted);
        Assert.AreEqual(defaultCameraId, scenario.Room.LeftDockCameraId);
    }

    [TestMethod]
    [DataRow(false, false, true, true)]
    [DataRow(true, false, true, false)]
    [DataRow(false, true, true, false)]
    [DataRow(false, false, false, false)]
    public void PersistsOnlyPreviouslyUntrackedUndockedCameras(bool isDocked, bool entityExists, bool hasTransform, bool expected)
    {
        Assert.AreEqual(expected, ScannerRoomManager.ShouldPersistLooseCamera(isDocked, entityExists, hasTransform));
    }
}
