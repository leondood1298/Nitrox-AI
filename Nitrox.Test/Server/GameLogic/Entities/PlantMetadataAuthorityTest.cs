using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;

namespace Nitrox.Server.Subnautica.Models.GameLogic.Entities;

[TestClass]
public sealed class PlantMetadataAuthorityTest
{
    private static readonly Func<int, bool> nothingPickedLately = _ => false;

    [TestMethod]
    public void PicksOfTwoPlayersWhoHadNotHeardOfEachOtherBothStay()
    {
        // A picked fruit 0; B, picking fruit 1, still had fruit 0
        FruitPlantMetadata stored = new([true, false, false], 200f);
        FruitPlantMetadata fromB = new([false, true, false], 210f);

        FruitPlantMetadata accepted = PlantMetadataAuthority.MergeFruit(stored, fromB, false, nothingPickedLately);

        CollectionAssert.AreEqual(new[] { true, true, false }, accepted.PickedStates);
        Assert.AreEqual(210f, accepted.TimeNextFruit);
        Assert.IsTrue(PlantMetadataAuthority.SenderIsBehind(fromB, accepted), "B must hide fruit 0 too, or B picks it again.");
    }

    [TestMethod]
    public void OnlyTheDecidingPlayerGrowsFruitBack()
    {
        FruitPlantMetadata stored = new([true, true], 200f);
        FruitPlantMetadata grownBack = new([false, true], 350f);

        CollectionAssert.AreEqual(new[] { true, true }, PlantMetadataAuthority.MergeFruit(stored, grownBack, false, nothingPickedLately).PickedStates);
        CollectionAssert.AreEqual(new[] { false, true }, PlantMetadataAuthority.MergeFruit(stored, grownBack, true, nothingPickedLately).PickedStates);
    }

    [TestMethod]
    public void FruitPickedMomentsAgoIsNotGrownBackByTheDecidersOlderCopy()
    {
        FruitPlantMetadata stored = new([true, false], 200f);
        FruitPlantMetadata decidersOlderCopy = new([false, false], 100f);

        FruitPlantMetadata accepted = PlantMetadataAuthority.MergeFruit(stored, decidersOlderCopy, true, index => index == 0);

        CollectionAssert.AreEqual(new[] { true, false }, accepted.PickedStates);
        Assert.IsTrue(PlantMetadataAuthority.SenderIsBehind(decidersOlderCopy, accepted));
    }

    [TestMethod]
    public void ReportOfAnotherNumberOfFruitIsRefused()
    {
        Assert.IsNull(PlantMetadataAuthority.MergeFruit(new FruitPlantMetadata([true], 1f), new FruitPlantMetadata([true, false], 1f), true, nothingPickedLately));
    }

    [TestMethod]
    public void NewlyPickedFruitAreTheOnesThatWereNotPickedBefore()
    {
        CollectionAssert.AreEqual(new[] { 1 }, PlantMetadataAuthority.NewlyPicked(new FruitPlantMetadata([true, false], 1f), new FruitPlantMetadata([true, true], 1f)).ToArray());
        CollectionAssert.AreEqual(new[] { 0 }, PlantMetadataAuthority.NewlyPicked(null, new FruitPlantMetadata([true, false], 1f)).ToArray());
    }

    [TestMethod]
    public void HealthOnlyGoesDownUnlessTheDecidingPlayerReportsIt()
    {
        Assert.AreEqual(40f, PlantMetadataAuthority.MergeHealth(40f, 90f, false));
        Assert.AreEqual(30f, PlantMetadataAuthority.MergeHealth(40f, 30f, false));
        Assert.AreEqual(90f, PlantMetadataAuthority.MergeHealth(40f, 90f, true));
        Assert.AreEqual(90f, PlantMetadataAuthority.MergeHealth(PlantableMetadata.NO_HEALTH, 90f, false), "The first report counts.");
        Assert.AreEqual(40f, PlantMetadataAuthority.MergeHealth(40f, PlantableMetadata.NO_HEALTH, true), "A report without health keeps the kept one.");
    }

    [TestMethod]
    public void PlantKeepsTheSlotAndGrowthItWasPlantedWith()
    {
        PlantableMetadata stored = new(50f, 3, null, PlantableMetadata.NO_HEALTH);
        PlantableMetadata report = new(70f, 1, new FruitPlantMetadata([true], 5f), 80f);

        PlantableMetadata accepted = PlantMetadataAuthority.MergePlantable(stored, report, false, nothingPickedLately);

        Assert.AreEqual(50f, accepted.TimeStartGrowth);
        Assert.AreEqual(3, accepted.SlotID);
        Assert.AreEqual(80f, accepted.Health);
        CollectionAssert.AreEqual(new[] { true }, accepted.FruitPlantMetadata.PickedStates);
    }
}
