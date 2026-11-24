using NitroxClient.GameLogic;

namespace Nitrox.Test.Client.GameLogic;

[TestClass]
public sealed class PlantersTest
{
    [TestMethod]
    public void SeedOfAnotherPlayerWaitsWhileThisGamesOwnSeedHoldsTheSlot()
    {
        Planters.CanTakeSlot(false, false).Should().BeTrue();
        Planters.CanTakeSlot(true, true).Should().BeTrue();
        Planters.CanTakeSlot(true, false).Should().BeFalse();
    }
}
