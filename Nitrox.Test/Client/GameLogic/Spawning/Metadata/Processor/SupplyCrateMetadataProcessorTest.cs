namespace NitroxClient.GameLogic.Spawning.Metadata.Processor;

/// <summary>
///     A crate another player opens while this player plays opens with its animation and sound; a crate that is
///     loaded already open, while joining or with its cell, is shown open at once and stays silent.
/// </summary>
[TestClass]
public sealed class SupplyCrateMetadataProcessorTest
{
    [TestMethod]
    public void ACrateOpenedByAnotherPlayerDuringPlayIsShownOpening()
    {
        Assert.IsTrue(SupplyCrateMetadataProcessor.IsOpenedInSight(true, false));
    }

    [TestMethod]
    public void ACrateLoadedWithItsCellIsShownAlreadyOpen()
    {
        Assert.IsFalse(SupplyCrateMetadataProcessor.IsOpenedInSight(true, true));
    }

    [TestMethod]
    public void ACrateLoadedWhileJoiningIsShownAlreadyOpen()
    {
        Assert.IsFalse(SupplyCrateMetadataProcessor.IsOpenedInSight(false, true));
        Assert.IsFalse(SupplyCrateMetadataProcessor.IsOpenedInSight(false, false));
    }
}
