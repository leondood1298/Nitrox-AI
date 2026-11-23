using Nitrox.Model.DataStructures;
using Nitrox.Model.DataStructures.Unity;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Bases;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;
using Nitrox.Model.Subnautica.Helper;

namespace Nitrox.Test.Model.Subnautica;

[TestClass]
public sealed class SimulationWhitelistTest
{
    [TestMethod]
    [DataRow(MapRoomCameraMetadata.NOT_DOCKED)]
    [DataRow(1)]
    public void CameraIsSimulatedForItsBatteryNotForItsMovement(int dockingIndex)
    {
        // The entity a client makes for a camera, in a dock or dropped into the world (Items.Dropped).
        GlobalRootEntity camera = new(new NitroxTransform(), GlobalRootEntity.GLOBAL_ROOT_LEVEL, "733fd479-0760-4bc2-a03e-281cbf02bfa4", true, new NitroxId(),
            new NitroxTechType("MapRoomCamera"), new MapRoomCameraMetadata(dockingIndex: dockingIndex), null, []);

        Assert.IsTrue(SimulationWhitelist.ShouldSimulateEntity(camera), "Its simulating player reports its battery and health and recharges it in its dock.");
        Assert.IsFalse(SimulationWhitelist.ShouldSimulateEntityMovement(camera), "Only a player who flies a camera moves it.");
    }

    [TestMethod]
    [DataRow("Bleeder")]
    [DataRow("ReefbackBaby")]
    [DataRow("SkyrayNonRoosting")]
    [DataRow("Crash")]
    public void ServerSpawnedCreaturesAreSimulated(string techType)
    {
        WorldEntity creature = new(NitroxVector3.Zero, NitroxQuaternion.Identity, NitroxVector3.One, new NitroxTechType(techType), 0, "creature", true, new NitroxId(), null);

        Assert.IsTrue(SimulationWhitelist.ShouldSimulateEntity(creature));
        Assert.IsTrue(SimulationWhitelist.ShouldSimulateEntityMovement(creature));
    }

    [TestMethod]
    public void WhoeverIsGivenARoomToSimulateIsGivenWhatIsInIt()
    {
        Assert.IsTrue(SimulationWhitelist.ShouldPropagateSimulation(new MapRoomEntity(new NitroxId(), new NitroxId(), new NitroxInt3(0, 0, 0))));
    }
}
