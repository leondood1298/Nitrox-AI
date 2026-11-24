using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;

namespace Nitrox.Server.Subnautica.Models.GameLogic.Entities;

[TestClass]
public sealed class DrillableMetadataAuthorityTest
{
    [TestMethod]
    public void ChunkBrokenByOnePlayerStaysBrokenWhenAnotherReportsItsOlderCopy()
    {
        // A broke chunk 1; B, drilling chunk 0, still had chunk 1 whole
        DrillableMetadata stored = new([200f, 0f, 200f], 10f);
        DrillableMetadata fromB = new([150f, 200f, 200f], 11f);

        DrillableMetadata accepted = DrillableMetadataAuthority.Merge(stored, fromB, false);

        CollectionAssert.AreEqual(new[] { 150f, 0f, 200f }, accepted.ChunkHealth);
        Assert.AreEqual(11f, accepted.TimeLastDrilled);
        Assert.IsTrue(DrillableMetadataAuthority.SenderIsBehind(fromB, accepted), "B must break chunk 1 in its game too, or it drills it again.");
    }

    [TestMethod]
    public void ReportOfTheLatestStateIsNotSentBack()
    {
        DrillableMetadata stored = new([200f, 0f, 200f], 10f);
        DrillableMetadata fromA = new([200f, 0f, 120f], 12f);

        DrillableMetadata accepted = DrillableMetadataAuthority.Merge(stored, fromA, false);

        CollectionAssert.AreEqual(fromA.ChunkHealth, accepted.ChunkHealth);
        Assert.IsFalse(DrillableMetadataAuthority.SenderIsBehind(fromA, accepted));
    }

    [TestMethod]
    public void FacilityDepositIsRestoredWhole()
    {
        DrillableMetadata drilledOut = new([0f, 0f], 10f);
        DrillableMetadata restored = new([200f, 200f], 10f);

        Assert.AreSame(restored, DrillableMetadataAuthority.Merge(drilledOut, restored, true));
    }

    [TestMethod]
    public void OrdinaryDepositIsNeverRestored()
    {
        DrillableMetadata drilledOut = new([0f, 0f], 10f);
        DrillableMetadata staleCopy = new([200f, 180f], 9f);

        CollectionAssert.AreEqual(new[] { 0f, 0f }, DrillableMetadataAuthority.Merge(drilledOut, staleCopy, false).ChunkHealth);
    }

    [TestMethod]
    public void PartlyDrilledFacilityDepositIsNotRestoredByAnOlderCopy()
    {
        DrillableMetadata stored = new([0f, 50f], 10f);
        DrillableMetadata staleCopy = new([200f, 200f], 9f);

        CollectionAssert.AreEqual(new[] { 0f, 50f }, DrillableMetadataAuthority.Merge(stored, staleCopy, true).ChunkHealth);
    }

    [TestMethod]
    public void FirstReportIsAcceptedAndAnotherChunkCountIsRefused()
    {
        DrillableMetadata first = new([200f, 150f], 3f);

        Assert.AreSame(first, DrillableMetadataAuthority.Merge(null, first, false));
        Assert.IsNull(DrillableMetadataAuthority.Merge(first, new DrillableMetadata([1f], 4f), false));
    }
}
