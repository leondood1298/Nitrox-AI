namespace NitroxClient.GameLogic;

[TestClass]
public sealed class BasePowerSourcesTest
{
    [TestMethod]
    public void SourceSimulatedByCreativePlayerStillGenerates()
    {
        // The Survival players of the base need what the reactor makes, whoever happens to simulate it.
        Assert.IsTrue(BasePowerSources.MustGenerateWithoutPowerRequirement(localModeRequiresPower: false, simulatedLocally: true, amount: 0.07f));
    }

    [TestMethod]
    public void GameHandlesEverythingForPlayersWhoRequirePower()
    {
        Assert.IsFalse(BasePowerSources.MustGenerateWithoutPowerRequirement(localModeRequiresPower: true, simulatedLocally: true, amount: 0.07f));
        Assert.IsFalse(BasePowerSources.MustGenerateWithoutPowerRequirement(localModeRequiresPower: true, simulatedLocally: false, amount: 0.07f));
    }

    [TestMethod]
    [DataRow(-5f)]
    [DataRow(0f)]
    public void CreativePlayersOwnDrawsStayFree(float amount)
    {
        // Nothing the Creative player's own game draws may cost power: that is what the mode means for them.
        Assert.IsFalse(BasePowerSources.MustGenerateWithoutPowerRequirement(localModeRequiresPower: false, simulatedLocally: true, amount));
    }

    [TestMethod]
    public void CreativePlayerDoesNotGenerateForSourcesSomeoneElseSimulates()
    {
        Assert.IsFalse(BasePowerSources.MustGenerateWithoutPowerRequirement(localModeRequiresPower: false, simulatedLocally: false, amount: 0.07f));
    }

    [TestMethod]
    public void SourceIsSimulatedByThePlayerHoldingItsLock()
    {
        Assert.IsTrue(BasePowerSources.CanSimulate(hasId: true, simulatedLocally: true, rebuildingBases: false));
        Assert.IsFalse(BasePowerSources.CanSimulate(hasId: true, simulatedLocally: false, rebuildingBases: false));
    }

    [TestMethod]
    public void SourceIsNotSimulatedWhileItsBaseIsBeingRebuilt()
    {
        // During a resync a nuclear reactor exists before its rods do. Run in that state it would wipe its fuel
        // progress and take the same amount off its stored power, and the owner would broadcast both.
        Assert.IsFalse(BasePowerSources.CanSimulate(hasId: true, simulatedLocally: true, rebuildingBases: true));
    }

    [TestMethod]
    public void SourceWithoutAnIdBehavesAsInTheGame()
    {
        Assert.IsTrue(BasePowerSources.CanSimulate(hasId: false, simulatedLocally: false, rebuildingBases: false));
        Assert.IsTrue(BasePowerSources.CanSimulate(hasId: false, simulatedLocally: false, rebuildingBases: true));
    }

    [TestMethod]
    [DataRow(40f, 75f, 2f, 42f)]
    [DataRow(74.5f, 75f, 2f, 75f)]
    [DataRow(75f, 75f, 2f, 75f)]
    public void GeneratingFillsUpToCapacity(float power, float maxPower, float amount, float expected)
    {
        Assert.AreEqual(expected, BasePowerSources.LevelAfterGenerating(power, maxPower, amount));
    }

    [TestMethod]
    [DataRow(40f, 5f, 35f)]
    [DataRow(3f, 5f, 0f)]
    [DataRow(0f, 5f, 0f)]
    public void DrawByAnotherPlayerStopsAtEmpty(float power, float amount, float expected)
    {
        Assert.AreEqual(expected, BasePowerSources.LevelAfterDraw(power, amount));
    }
}
