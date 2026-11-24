using NitroxClient.GameLogic.Spawning.Metadata.Extractor;

namespace Nitrox.Test.Client.GameLogic.Spawning.Metadata.Extractor;

[TestClass]
public sealed class PlantableMetadataExtractorTest
{
    [TestMethod]
    public void CarriedPlantableFoodKeepsItsDecayInsteadOfItsGrowth()
    {
        PlantableMetadataExtractor.HasStateToKeep(true).Should().BeTrue();
        PlantableMetadataExtractor.HasStateToKeep(false).Should().BeFalse();
    }
}
