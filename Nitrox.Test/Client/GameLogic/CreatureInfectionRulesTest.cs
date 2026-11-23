using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;
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

    [TestMethod]
    public void SamePeeperAlwaysGetsTheSameBornHeroRoll()
    {
        NitroxId id = new("0f8a3c52-9d1e-4b7a-8e33-5c2b1a7d9e04");

        CreatureInfectionRules.BornHeroRoll(id).Should().Be(CreatureInfectionRules.BornHeroRoll(new NitroxId("0f8a3c52-9d1e-4b7a-8e33-5c2b1a7d9e04")));
    }

    [TestMethod]
    public void AboutOnePeeperInTwentyIsBornAHeroIndependentlyOfItsInfection()
    {
        const int COUNT = 20000;
        int heroes = 0;
        int infectedHeroes = 0;
        NitroxId id = new("6b1f0e2a-0000-4000-8000-000000000000");
        for (int i = 0; i < COUNT; i++)
        {
            float heroRoll = CreatureInfectionRules.BornHeroRoll(id);
            heroRoll.Should().BeGreaterThanOrEqualTo(0f).And.BeLessThan(1f);
            if (heroRoll < CreatureInfectionRules.BORN_HERO_CHANCE)
            {
                heroes++;
                if (CreatureInfectionRules.Roll(id) < GAME_INFECTION_CHANCE)
                {
                    infectedHeroes++;
                }
            }
            id = id.Increment();
        }

        ((float)heroes / COUNT).Should().BeApproximately(CreatureInfectionRules.BORN_HERO_CHANCE, 0.005f);
        // The same roll for both would make every infected peeper a hero, and BecomeHero cures it
        ((float)infectedHeroes / heroes).Should().BeLessThan(0.15f);
    }

    [TestMethod]
    public void RollThatNeverInfectsNeverMakesAHero()
    {
        CreatureInfectionRules.NEVER_INFECTS.Should().BeGreaterThanOrEqualTo(CreatureInfectionRules.BORN_HERO_CHANCE);
    }

    [TestMethod]
    public void PeeperNobodyMadeAHeroMayBeBornAHero()
    {
        CreatureInfectionRules.MayBornHeroRecharge(CreatureMetadata.NO_ENZYME, 1000f, 120f).Should().BeTrue();
    }

    [TestMethod]
    public void BornHeroRechargesOnlyLongAfterItsEnzymeRanOut()
    {
        CreatureInfectionRules.MayBornHeroRecharge(1060f, 1000f, 120f).Should().BeFalse();
        CreatureInfectionRules.MayBornHeroRecharge(1060f, 1100f, 120f).Should().BeFalse();
        CreatureInfectionRules.MayBornHeroRecharge(1060f, 1180f, 120f).Should().BeTrue();
    }

    [TestMethod]
    public void PeeperWithLastingEnzymeIsNotRecharged()
    {
        CreatureInfectionRules.MayBornHeroRecharge(CreatureMetadata.LASTING_ENZYME, 1000f, 120f).Should().BeFalse();
    }

    [TestMethod]
    public void EnzymeRunsOutAfterWhatIsLeft()
    {
        CreatureInfectionRules.GetEnzymeEndTime(1000f, 60f, false).Should().Be(1060f);
        CreatureInfectionRules.GetEnzymeEndTime(1000f, 0f, false).Should().Be(CreatureMetadata.NO_ENZYME);
    }

    [TestMethod]
    public void EnzymeInThePrisonAquariumLasts()
    {
        CreatureInfectionRules.GetEnzymeEndTime(1000f, 60f, true).Should().Be(CreatureMetadata.LASTING_ENZYME);
        CreatureInfectionRules.GetEnzymeLeft(CreatureMetadata.LASTING_ENZYME, 1000f, 60f).Should().Be(60f);
    }

    [TestMethod]
    [DataRow(1060f, 1000f, 60f)]
    [DataRow(1060f, 1030f, 30f)]
    [DataRow(1060f, 1100f, 0f)]
    [DataRow(5000f, 1000f, 60f)]
    public void PlayerWhoGetsAHeroPeeperGivesItTheEnzymeThatIsLeft(float enzymeEndTime, float now, float expectedLeft)
    {
        CreatureInfectionRules.GetEnzymeLeft(enzymeEndTime, now, 60f).Should().Be(expectedLeft);
    }
}
