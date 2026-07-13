using Nitrox.Model.DataStructures;
using Nitrox.Model.DataStructures.Unity;
using Nitrox.Model.Subnautica.Packets;

namespace Nitrox.Server.Subnautica.Models.GameLogic.Entities;

[TestClass]
public sealed class VehicleAuthorityTest
{
    [DataTestMethod]
    [DataRow(0, false)]
    [DataRow(1, true)]
    [DataRow(VehicleAuthority.MAX_MOVEMENTS_PER_PACKET, true)]
    [DataRow(VehicleAuthority.MAX_MOVEMENTS_PER_PACKET + 1, false)]
    public void MovementPacketCountIsBounded(int count, bool expected)
    {
        Assert.AreEqual(expected, VehicleAuthority.IsValidMovementCount(count));
    }

    [DataTestMethod]
    [DataRow(true, true, true)]
    [DataRow(true, false, false)]
    [DataRow(false, true, false)]
    [DataRow(false, false, false)]
    public void DockingRequiresUnparentedVehicleAndAvailableDock(bool vehicleIsUnparented, bool dockAvailable, bool expected)
    {
        Assert.AreEqual(expected, VehicleAuthority.CanDock(vehicleIsUnparented, dockAvailable));
    }

    [TestMethod]
    public void AcceptsFiniteMovement()
    {
        SimpleMovementData movement = new(new NitroxId(), new NitroxVector3(1f, 2f, 3f), NitroxQuaternion.Identity);

        Assert.IsTrue(VehicleAuthority.IsFinite(movement));
    }

    [TestMethod]
    public void RejectsNonFinitePosition()
    {
        SimpleMovementData movement = new(new NitroxId(), new NitroxVector3(float.NaN, 2f, 3f), NitroxQuaternion.Identity);

        Assert.IsFalse(VehicleAuthority.IsFinite(movement));
    }

    [TestMethod]
    public void RejectsInvalidQuaternion()
    {
        SimpleMovementData movement = new(new NitroxId(), new NitroxVector3(1f, 2f, 3f), new NitroxQuaternion(0f, 0f, 0f, 0f));

        Assert.IsFalse(VehicleAuthority.IsFinite(movement));
    }

    [TestMethod]
    public void RejectsNonFiniteExosuitAimTarget()
    {
        ExosuitMovementData movement = new(new NitroxId(), new NitroxVector3(1f, 2f, 3f), NitroxQuaternion.Identity,
                                           new NitroxVector3(float.PositiveInfinity, 0f, 0f), new NitroxVector3(0f, 0f, 0f),
                                           0, 0, false, true);

        Assert.IsFalse(VehicleAuthority.IsFinite(movement));
    }
}
