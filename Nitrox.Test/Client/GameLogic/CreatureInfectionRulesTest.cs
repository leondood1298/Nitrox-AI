using Nitrox.Model.DataStructures;
using NitroxClient.GameLogic;

namespace Nitrox.Test.Client.GameLogic;

[TestClass]
public sealed class CreatureInfectionRulesTest
{
    /// <summary>
    ///     Creature.ProcessInfection infects a creature when its roll is below this.
    /// </summary>
    private const float GAME_INFECTION_CHANCE = 0.05f;

    [TestMethod]
    public void SameCreatureAlwaysGetsTheSameRoll()
    {
        NitroxId id = new("0f8a3c52-9d1e-4b7a-8e33-5c2b1a7d9e04");

        CreatureInfectionRules.Roll(id).Should().Be(CreatureInfectionRules.Roll(new NitroxId("0f8a3c52-9d1e-4b7a-8e33-5c2b1a7d9e04")));
    }

    [TestMethod]
    public void RollIsFixedAcrossRunsForAGivenId()
    {
        // A value that changed would make creatures of existing worlds change their infection after an update
        float roll = CreatureInfectionRules.Roll(new NitroxId("00000000-0000-0000-0000-000000000001"));

        roll.Should().BeInRange(0f, 1f);
        CreatureInfectionRules.Roll(new NitroxId("00000000-0000-0000-0000-000000000001")).Should().Be(roll);
        CreatureInfectionRules.Roll(new NitroxId("00000000-0000-0000-0000-000000000002")).Should().NotBe(roll);
    }

    [TestMethod]
    public void AboutOneCreatureInTwentyIsInfected()
    {
        const int COUNT = 20000;
        int infected = 0;
        NitroxId id = new("6b1f0e2a-0000-4000-8000-000000000000");
        for (int i = 0; i < COUNT; i++)
        {
            float roll = CreatureInfectionRules.Roll(id);
            roll.Should().BeGreaterThanOrEqualTo(0f).And.BeLessThan(1f);
            if (roll < GAME_INFECTION_CHANCE)
            {
                infected++;
            }
            // Consecutive ids, as the server's generator hands out
            id = id.Increment();
        }

        ((float)infected / COUNT).Should().BeApproximately(GAME_INFECTION_CHANCE, 0.005f);
    }

    [TestMethod]
    public void RollThatNeverInfectsIsAboveTheGamesChance()
    {
        CreatureInfectionRules.NEVER_INFECTS.Should().BeGreaterThanOrEqualTo(GAME_INFECTION_CHANCE);
    }

    [TestMethod]
    public void OnlyTheSimulatingPlayerOrAReceivedValueChangesInfection()
    {
        CreatureInfectionRules.MayWrite(true, false).Should().BeTrue();
        CreatureInfectionRules.MayWrite(false, true).Should().BeTrue();
        CreatureInfectionRules.MayWrite(false, false).Should().BeFalse();
    }

    [TestMethod]
    [DataRow(1f, 0f)]
    [DataRow(0f, 1f)]
    [DataRow(1f, 0.1f)]
    [DataRow(0.1f, 0.5f)]
    public void CureInfectionAndLargeStepsAreSentAtOnce(float previous, float current)
    {
        CreatureInfectionRules.IsImmediateChange(previous, current).Should().BeTrue();
    }

    [TestMethod]
    public void HealingByAHeroPeeperEveryFrameIsThrottled()
    {
        CreatureInfectionRules.IsImmediateChange(0.8f, 0.79f).Should().BeFalse();
    }
}
