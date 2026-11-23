using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nitrox.Model.DataStructures;
using Nitrox.Server.Subnautica.Models.Packets.Processors;

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
        Assert.AreEqual(expected, MapRoomCameraDockProcessor.IsKnownCamera(validWorldCamera, registeredCamera));
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
        Assert.AreEqual(expected, MapRoomCameraDockProcessor.CanBootstrapRestoredCamera(isDocked, senderOwnsRoom, slotAvailable, registeredCameraCount));
    }

    [TestMethod]
    [DataRow(SimulationLockType.EXCLUSIVE, true, true)]
    [DataRow(SimulationLockType.EXCLUSIVE, false, false)]
    [DataRow(SimulationLockType.TRANSIENT, true, false)]
    [DataRow(SimulationLockType.TRANSIENT, false, false)]
    public void DockingPreservesOnlyTheControllersExclusiveLock(SimulationLockType lockType, bool holderIsActiveController, bool expected)
    {
        Assert.AreEqual(expected, MapRoomCameraDockProcessor.ShouldPreserveControlLock(lockType, holderIsActiveController));
    }

    [TestMethod]
    [DataRow(false, false, true, true)]
    [DataRow(true, false, true, false)]
    [DataRow(false, true, true, false)]
    [DataRow(false, false, false, false)]
    public void PersistsOnlyPreviouslyUntrackedUndockedCameras(bool isDocked, bool entityExists, bool hasTransform, bool expected)
    {
        Assert.AreEqual(expected, MapRoomCameraDockProcessor.ShouldPersistLooseCamera(isDocked, entityExists, hasTransform));
    }
}
