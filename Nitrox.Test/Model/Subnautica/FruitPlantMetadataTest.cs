using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;

namespace Nitrox.Test.Model.Subnautica;

[TestClass]
public sealed class FruitPlantMetadataTest
{
    [TestMethod]
    public void PresentFruitAreTheOnesNotPickedInTheOrderOfThePlant()
    {
        CollectionAssert.AreEqual(new[] { 1, 3 }, FruitPlantMetadata.PresentIndices([true, false, true, false]));
        CollectionAssert.AreEqual(Array.Empty<int>(), FruitPlantMetadata.PresentIndices([true, true]));
        CollectionAssert.AreEqual(Array.Empty<int>(), FruitPlantMetadata.PresentIndices(null));
    }
}
