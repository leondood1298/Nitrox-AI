using NitroxClient.GameLogic;

namespace Nitrox.Test.Client.GameLogic;

[TestClass]
public sealed class ContainmentRulesTest
{
    [TestMethod]
    public void OnlyThePlayerWhoSimulatesTheContainmentHatchesItsEggs()
    {
        ContainmentRules.DecidesHere(true, true).Should().BeTrue();
        ContainmentRules.DecidesHere(true, false).Should().BeFalse("the other games wait for the simulating player's hatch, or the egg vanished there with no creature");
    }

    [TestMethod]
    public void UnsyncedContainmentDecidesInEveryGame()
    {
        ContainmentRules.DecidesHere(false, false).Should().BeTrue();
    }
}
