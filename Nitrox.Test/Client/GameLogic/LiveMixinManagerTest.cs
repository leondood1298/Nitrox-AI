using NitroxClient.GameLogic;

namespace Nitrox.Test.Client.GameLogic;

[TestClass]
public sealed class LiveMixinManagerTest
{
    [TestMethod]
    public void OnlyAWildPlantThatYieldsWhileAliveIsRemovedForEveryoneWhenCutToDeath()
    {
        LiveMixinManager.RemovesHarvestableForEveryone(false, false, HarvestType.DamageAlive).Should().BeTrue();
        LiveMixinManager.RemovesHarvestableForEveryone(true, false, HarvestType.DamageAlive).Should().BeFalse("creatures keep their corpse path");
        LiveMixinManager.RemovesHarvestableForEveryone(false, true, HarvestType.DamageAlive).Should().BeFalse("its scripts bring it back to life");
        LiveMixinManager.RemovesHarvestableForEveryone(false, false, HarvestType.DamageDead).Should().BeFalse("it still yields once dead");
        LiveMixinManager.RemovesHarvestableForEveryone(false, false, HarvestType.None).Should().BeFalse();
    }
}
