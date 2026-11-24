using NitroxClient.GameLogic;

namespace Nitrox.Test.Client.GameLogic;

[TestClass]
public sealed class PickablePlantsTest
{
    [TestMethod]
    public void PartsThatDestroyThemselvesWhenPickedAreNotTrackedAsFruit()
    {
        PickablePlants.IsTrackedPart(false).Should().BeTrue();
        PickablePlants.IsTrackedPart(true).Should().BeFalse("they are removed for everybody with their object");
    }
}
