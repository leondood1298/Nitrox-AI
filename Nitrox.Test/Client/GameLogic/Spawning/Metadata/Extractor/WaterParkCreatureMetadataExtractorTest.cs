using NitroxClient.GameLogic.Spawning.Metadata.Extractor;

namespace Nitrox.Test.Client.GameLogic.Spawning.Metadata.Extractor;

[TestClass]
public sealed class WaterParkCreatureMetadataExtractorTest
{
    [TestMethod]
    public void DeadContainmentFishKeepsItsDecayInsteadOfItsGrowth()
    {
        WaterParkCreatureMetadataExtractor.HasGrowthToKeep(0.5f, true).Should().BeTrue();
        WaterParkCreatureMetadataExtractor.HasGrowthToKeep(0.5f, false).Should().BeFalse();
        WaterParkCreatureMetadataExtractor.HasGrowthToKeep(-1f, true).Should().BeFalse();
    }
}
