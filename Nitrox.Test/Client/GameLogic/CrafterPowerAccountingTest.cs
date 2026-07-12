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
}
