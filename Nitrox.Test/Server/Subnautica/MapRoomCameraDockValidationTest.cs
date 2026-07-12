using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nitrox.Model.DataStructures;
using Nitrox.Server.Subnautica.Models.Packets.Processors;

namespace Nitrox.Test.Server.Subnautica;

[TestClass]
public sealed class MapRoomCameraDockValidationTest
{
    [DataTestMethod]
    [DataRow(true, false, true)]
    [DataRow(false, true, true)]
    [DataRow(true, true, true)]
    [DataRow(false, false, false)]
    public void AcceptsWorldOrRestoredRegisteredCamera(bool validWorldCamera, bool registeredCamera, bool expected)
    {
        Assert.AreEqual(expected, MapRoomCameraDockProcessor.IsKnownCamera(validWorldCamera, registeredCamera));
    }

    [DataTestMethod]
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

    [DataTestMethod]
    [DataRow(true, SimulationLockType.EXCLUSIVE, true)]
    [DataRow(false, SimulationLockType.EXCLUSIVE, false)]
    [DataRow(true, SimulationLockType.TRANSIENT, false)]
    public void DockingPreservesOnlyTheControllersExclusiveLock(bool senderOwnsLock, SimulationLockType lockType, bool expected)
    {
        Assert.AreEqual(expected, MapRoomCameraDockProcessor.ShouldPreserveControlLock(senderOwnsLock, lockType));
    }
}
