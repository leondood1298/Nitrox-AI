using Nitrox.Model.DataStructures.Unity;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;

namespace Nitrox.Server.Subnautica.Models.GameLogic.Entities;

[TestClass]
public sealed class VehicleMetadataAuthorityTest
{
    private static readonly NitroxVector3[] defaultColors = [new(1, 1, 1)];
    private static readonly NitroxVector3[] redColors = [new(1, 0, 0)];

    [TestMethod]
    public void RenameByAnotherPlayerKeepsTheSimulatedHealth()
    {
        // The bay repaired the Seamoth to 300 in the simulating player's game; the renaming player still has 150
        SeamothMetadata stored = new(true, 300f, false, "Seamoth", defaultColors, 2);
        SeamothMetadata rename = new(false, 150f, true, "Moth", redColors, 3);

        SeamothMetadata accepted = (SeamothMetadata)VehicleMetadataAuthority.Merge(stored, rename, false);

        Assert.AreEqual("Moth", accepted.Name);
        Assert.AreSame(redColors, accepted.Colors);
        Assert.AreEqual(3, accepted.NameVersion);
        Assert.AreEqual(300f, accepted.Health);
        Assert.IsFalse(accepted.InPrecursor);
        Assert.IsTrue(accepted.LightsOn);
    }

    [TestMethod]
    public void DamageReportCarryingTheFormerNameKeepsTheRename()
    {
        // The simulating player was hit before another player's rename reached it
        ExosuitMetadata stored = new(600f, false, "Crab", redColors, 3);
        ExosuitMetadata damageReport = new(540f, false, "Prawn", defaultColors, 2);

        VehicleMetadata accepted = VehicleMetadataAuthority.Merge(stored, damageReport, true);

        Assert.IsInstanceOfType<ExosuitMetadata>(accepted);
        Assert.AreEqual(540f, accepted.Health);
        Assert.AreEqual("Crab", accepted.Name);
        Assert.AreSame(redColors, accepted.Colors);
        Assert.AreEqual(3, accepted.NameVersion);
    }

    [TestMethod]
    public void SimulatingPlayerReportWithTheKnownNameIsAcceptedAsSent()
    {
        SeamothMetadata stored = new(true, 300f, false, "Moth", redColors, 3);
        SeamothMetadata report = new(false, 280f, true, "Moth", redColors, 3);

        SeamothMetadata accepted = (SeamothMetadata)VehicleMetadataAuthority.Merge(stored, report, true);

        Assert.AreEqual(280f, accepted.Health);
        Assert.IsTrue(accepted.InPrecursor);
        Assert.IsFalse(accepted.LightsOn);
        Assert.AreEqual("Moth", accepted.Name);
    }

    [TestMethod]
    public void SimulatingPlayerMayRenameToo()
    {
        SeamothMetadata stored = new(true, 300f, false, "Moth", redColors, 3);
        SeamothMetadata rename = new(true, 300f, false, "Night Moth", defaultColors, 4);

        Assert.AreSame(rename, VehicleMetadataAuthority.Merge(stored, rename, true));
    }

    [TestMethod]
    public void RenamesThatCrossedEachOtherEndOnTheLaterOne()
    {
        SeamothMetadata stored = new(true, 300f, false, "First", redColors, 4);
        SeamothMetadata later = new(true, 300f, false, "Second", defaultColors, 4);

        Assert.AreEqual("Second", VehicleMetadataAuthority.Merge(stored, later, false).Name);
    }

    [TestMethod]
    public void FirstMetadataIsAcceptedAsSent()
    {
        ExosuitMetadata first = new(600f, false, "Prawn", defaultColors, 0);

        Assert.AreSame(first, VehicleMetadataAuthority.Merge(null, first, false));
    }
}
