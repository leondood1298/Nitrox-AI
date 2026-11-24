using Nitrox.Model.DataStructures.GameLogic;
using NitroxClient.GameLogic.HUD;

namespace Nitrox.Test.Client.GameLogic.HUD;

[TestClass]
public sealed class RemotePlayerVitalsRulesTest
{
    [TestMethod]
    public void ASurvivalPlayerNearbyShowsAllBars()
    {
        RemotePlayerVitalsRules.ShowsHealthAndOxygen(SubnauticaGameMode.SURVIVAL, 5f).Should().BeTrue();
        RemotePlayerVitalsRules.ShowsFoodAndWater(SubnauticaGameMode.HARDCORE, 5f).Should().BeTrue();
    }

    [TestMethod]
    public void AFreedomPlayerShowsNoFoodOrWater()
    {
        RemotePlayerVitalsRules.ShowsHealthAndOxygen(SubnauticaGameMode.FREEDOM, 5f).Should().BeTrue();
        RemotePlayerVitalsRules.ShowsFoodAndWater(SubnauticaGameMode.FREEDOM, 5f).Should().BeFalse();
    }

    [TestMethod]
    public void ACreativePlayerShowsNoBars()
    {
        RemotePlayerVitalsRules.ShowsHealthAndOxygen(SubnauticaGameMode.CREATIVE, 5f).Should().BeFalse();
        RemotePlayerVitalsRules.ShowsFoodAndWater(SubnauticaGameMode.CREATIVE, 5f).Should().BeFalse();
    }

    [TestMethod]
    public void AFarPlayerShowsNoBars()
    {
        float far = RemotePlayerVitalsRules.BARS_VISIBLE_DISTANCE + 1f;

        RemotePlayerVitalsRules.ShowsHealthAndOxygen(SubnauticaGameMode.SURVIVAL, far).Should().BeFalse();
        RemotePlayerVitalsRules.ShowsFoodAndWater(SubnauticaGameMode.SURVIVAL, far).Should().BeFalse();
    }
}
