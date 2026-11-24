using Nitrox.Model.DataStructures;
using Nitrox.Model.DataStructures.Unity;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Bases;

namespace Nitrox.Server.Subnautica.Models.GameLogic.Entities;

[TestClass]
public sealed class VehicleDockingAuthorityTest
{
    [TestMethod]
    public void FreeBayTakesTheVehicleOfItsSimulatingPlayer()
    {
        MoonpoolEntity bay = NewBay();

        Assert.AreEqual(VehicleDockingAuthority.Decision.ACCEPT, VehicleDockingAuthority.MayDock(NewVehicle(null), bay, true));
    }

    [TestMethod]
    public void SecondVehicleInOneBayIsSentOut()
    {
        MoonpoolEntity bay = NewBay();
        bay.ChildEntities.Add(NewVehicle(bay.Id));

        Assert.AreEqual(VehicleDockingAuthority.Decision.SEND_OUT, VehicleDockingAuthority.MayDock(NewVehicle(null), bay, true));
    }

    [TestMethod]
    public void RepeatedDockingOfTheSameVehicleIsAccepted()
    {
        MoonpoolEntity bay = NewBay();
        VehicleEntity vehicle = NewVehicle(bay.Id);
        bay.ChildEntities.Add(vehicle);

        Assert.AreEqual(VehicleDockingAuthority.Decision.ACCEPT, VehicleDockingAuthority.MayDock(vehicle, bay, true));
    }

    [TestMethod]
    public void DockingByAPlayerWhoDoesNotSimulateTheVehicleIsIgnored()
    {
        Assert.AreEqual(VehicleDockingAuthority.Decision.IGNORE, VehicleDockingAuthority.MayDock(NewVehicle(null), NewBay(), false));
    }

    [TestMethod]
    public void VehicleLeavesOnlyTheBayItIsDockedIn()
    {
        MoonpoolEntity bay = NewBay();
        VehicleEntity vehicle = NewVehicle(bay.Id);

        Assert.IsTrue(VehicleDockingAuthority.MayUndock(vehicle, bay, true));
        Assert.IsFalse(VehicleDockingAuthority.MayUndock(vehicle, bay, false), "Undocking asks for the vehicle's lock first.");
        Assert.IsFalse(VehicleDockingAuthority.MayUndock(vehicle, NewBay(), true));
        Assert.IsFalse(VehicleDockingAuthority.MayUndock(NewVehicle(null), bay, true));
    }

    private static MoonpoolEntity NewBay() => new(new NitroxId(), new NitroxId(), new NitroxInt3(0, 0, 0));

    private static VehicleEntity NewVehicle(NitroxId parentId) =>
        new(null, 0f, new NitroxTransform(), 0, "", false, new NitroxId(), new NitroxTechType("Seamoth"), null, parentId, []);
}
