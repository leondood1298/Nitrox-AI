using NitroxClient.GameLogic;

namespace Nitrox.Test.Client.GameLogic;

[TestClass]
public sealed class BaseSpotLightsTest
{
    [TestMethod]
    public void TheSimulatingGameChoosesTheTargetAndTellsTheOthers()
    {
        BaseSpotLights.Decide(true, true).Should().Be(SpotLightTargeting.CHOOSE_AND_SHARE);
    }

    [TestMethod]
    public void AnyOtherGameFollowsWhatItIsTold()
    {
        BaseSpotLights.Decide(true, false).Should().Be(SpotLightTargeting.FOLLOW);
    }

    [TestMethod]
    public void ASpotlightNobodyElseKnowsActsAsInTheGame()
    {
        BaseSpotLights.Decide(false, false).Should().Be(SpotLightTargeting.CHOOSE_LOCALLY);
    }
}
