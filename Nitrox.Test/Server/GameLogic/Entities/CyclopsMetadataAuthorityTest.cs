using Nitrox.Model.DataStructures;
using Nitrox.Model.DataStructures.Unity;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;

namespace Nitrox.Server.Subnautica.Models.GameLogic.Entities;

[TestClass]
public sealed class CyclopsMetadataAuthorityTest
{
    [TestMethod]
    public void PilotsPressCannotOverwriteHullHealthOrDestruction()
    {
        // The pilot switches silent running on before the lock reached it; its copy of the hull still shows full health
        CyclopsMetadata stored = new(false, false, false, true, 1, 310f, false);
        CyclopsMetadata fromPilot = new(true, false, false, true, 1, 1500f, false);

        CyclopsMetadata accepted = CyclopsMetadataAuthority.Merge(stored, fromPilot, false, true);

        Assert.IsTrue(accepted.SilentRunningOn, "The button press itself is accepted.");
        Assert.AreEqual(310f, accepted.Health);
        Assert.IsFalse(accepted.IsDestroyed);
    }

    [TestMethod]
    public void StaleCopyCannotBringADestroyedCyclopsBack()
    {
        CyclopsMetadata stored = new(false, false, false, false, 0, 0f, true);
        CyclopsMetadata fromPilot = new(false, true, false, false, 0, 900f, false);

        CyclopsMetadata accepted = CyclopsMetadataAuthority.Merge(stored, fromPilot, false, true);

        Assert.IsTrue(accepted.IsDestroyed);
        Assert.AreEqual(0f, accepted.Health);
        Assert.IsTrue(accepted.ShieldOn);
    }

    [TestMethod]
    public void HullReportOfTheSimulatingPlayerKeepsThePilotsControls()
    {
        CyclopsMetadata stored = new(false, true, true, true, 2, 1500f, false);
        CyclopsMetadata hullReport = new(false, false, false, true, 1, 1100f, false);

        CyclopsMetadata accepted = CyclopsMetadataAuthority.Merge(stored, hullReport, true, false);

        Assert.AreEqual(1100f, accepted.Health);
        Assert.IsTrue(accepted.ShieldOn);
        Assert.IsTrue(accepted.SonarOn);
        Assert.AreEqual(2, accepted.EngineMode);
        Assert.IsTrue(CyclopsMetadataAuthority.HelmWasRefused(hullReport, accepted));
    }

    [TestMethod]
    public void PlayerNeitherAtTheHelmNorSimulatingChangesNothing()
    {
        CyclopsMetadata stored = new(false, false, false, true, 1, 1500f, false);
        CyclopsMetadata fromPassenger = new(true, true, true, false, 0, 200f, true);

        CyclopsMetadata accepted = CyclopsMetadataAuthority.Merge(stored, fromPassenger, false, false);

        Assert.IsFalse(CyclopsMetadataAuthority.HelmWasRefused(stored, accepted));
        Assert.AreEqual(1500f, accepted.Health);
        Assert.IsFalse(accepted.IsDestroyed);
    }

    [TestMethod]
    public void SimulatingPilotSetsEverything()
    {
        CyclopsMetadata stored = new(false, false, false, true, 1, 1500f, false);
        CyclopsMetadata fromSimulator = new(true, false, false, true, 1, 740f, false);

        Assert.AreSame(fromSimulator, CyclopsMetadataAuthority.Merge(stored, fromSimulator, true, true));
    }

    [TestMethod]
    public void FirstMetadataIsAcceptedAsSent()
    {
        CyclopsMetadata first = new(false, false, false, true, 1, 1500f, false);

        Assert.AreSame(first, CyclopsMetadataAuthority.Merge(null, first, false, false));
    }

    [TestMethod]
    public void OnlyTheFirstDestructionCounts()
    {
        CyclopsMetadata alive = new(false, false, false, true, 1, 10f, false);
        CyclopsMetadata destroyed = new(false, false, false, false, 1, 0f, true);

        Assert.IsTrue(CyclopsMetadataAuthority.IsNewlyDestroyed(alive, destroyed));
        Assert.IsFalse(CyclopsMetadataAuthority.IsNewlyDestroyed(destroyed, destroyed));
    }

    [TestMethod]
    public void DockedVehiclesAreFoundUnderTheDockingBays()
    {
        VehicleEntity cyclops = NewVehicle("Cyclops", null);
        PathBasedChildEntity bay = new("DockingBay", new NitroxId(), null, null, cyclops.Id, []);
        cyclops.ChildEntities.Add(bay);
        VehicleEntity prawn = NewVehicle("Exosuit", bay.Id);
        bay.ChildEntities.Add(prawn);
        PathBasedChildEntity locker = new("Locker", new NitroxId(), null, null, cyclops.Id, []);
        cyclops.ChildEntities.Add(locker);

        CollectionAssert.AreEqual(new[] { prawn }, CyclopsMetadataAuthority.FindDockedVehicles(cyclops));
    }

    private static VehicleEntity NewVehicle(string techType, NitroxId parentId)
    {
        return new VehicleEntity(null, 0f, new NitroxTransform(), 0, "", false, new NitroxId(), new NitroxTechType(techType), null, parentId, []);
    }
}
