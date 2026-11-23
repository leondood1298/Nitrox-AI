using Nitrox.Model.DataStructures;
using NitroxClient.GameLogic.Spawning.Metadata.Processor;

namespace Nitrox.Test.Client.GameLogic;

[TestClass]
public sealed class CrafterPowerAccountingTest
{
    [TestMethod]
    public void SameCraftConsumesPowerOnlyOnce()
    {
        NitroxId crafterId = new();

        Assert.IsTrue(CrafterPowerAccounting.TryAccount(crafterId, 10f, true));
        Assert.IsFalse(CrafterPowerAccounting.TryAccount(crafterId, 10f, true));
        Assert.IsTrue(CrafterPowerAccounting.TryAccount(crafterId, 11f, true));
    }

    [TestMethod]
    public void LocalCraftMarkerSuppressesMetadataReplay()
    {
        NitroxId crafterId = new();
        CrafterPowerAccounting.MarkAccounted(crafterId, 20f);

        Assert.IsFalse(CrafterPowerAccounting.TryAccount(crafterId, 20f, true));
    }

    [TestMethod]
    public void InitialSyncNeverConsumesAndMarksReplayAccounted()
    {
        NitroxId crafterId = new();

        Assert.IsFalse(CrafterPowerAccounting.TryAccount(crafterId, 30f, false));
        Assert.IsFalse(CrafterPowerAccounting.TryAccount(crafterId, 30f, true));
    }

    [TestMethod]
    public void PickupByAnotherPlayerDoesNotChargeTheCraftAgain()
    {
        // Player A crafts four titanium. Player B takes the first one, which broadcasts a crafter update carrying
        // B's own local start time and no duration. A third client must not pay for the craft a second time.
        NitroxId crafterId = new();
        const float crafterStartTime = 100f;
        const float pickerLocalStartTime = 100.4f;

        Assert.IsTrue(CrafterPowerAccounting.ShouldConsume(crafterId, crafterStartTime, 3f, true));
        Assert.IsFalse(CrafterPowerAccounting.ShouldConsume(crafterId, pickerLocalStartTime, 0f, true));
        Assert.IsFalse(CrafterPowerAccounting.ShouldConsume(crafterId, pickerLocalStartTime, 0f, true));

        Assert.IsTrue(CrafterPowerAccounting.ShouldConsume(crafterId, 200f, 3f, true), "The next real craft still costs power.");
    }
}
