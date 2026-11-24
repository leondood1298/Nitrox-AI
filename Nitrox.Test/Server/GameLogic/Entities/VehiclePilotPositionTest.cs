using Nitrox.Model.DataStructures.Unity;

namespace Nitrox.Server.Subnautica.Models.GameLogic.Entities;

[TestClass]
public sealed class VehiclePilotPositionTest
{
    [TestMethod]
    public void PilotIsKeptAboveTheHull()
    {
        NitroxVector3 seamoth = new(120f, -45f, 300f);

        NitroxVector3 kept = VehiclePilotPosition.BesideVehicle(seamoth);

        Assert.AreEqual(seamoth.X, kept.X);
        Assert.AreEqual(seamoth.Z, kept.Z);
        Assert.IsTrue(kept.Y - seamoth.Y >= 3f, "Inside the hull the rejoining player would stand within the vehicle.");
    }
}
