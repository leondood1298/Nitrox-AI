using NitroxClient.GameLogic;

namespace Nitrox.Test.Client.GameLogic;

[TestClass]
public sealed class SeaEmperorBabyRulesTest
{
    private const float X = 243.4f;
    private const float Y = -1585.43f;
    private const float Z = -307.53f;

    [TestMethod]
    public void EveryPlayerNamesTheSameBabyTheSameWay()
    {
        SeaEmperorBabyRules.GetBabyId(X, Y, Z, 2).Should().Be(SeaEmperorBabyRules.GetBabyId(X, Y, Z, 2));
        SeaEmperorBabyRules.GetCureBallId(X, Y, Z, 2).Should().Be(SeaEmperorBabyRules.GetCureBallId(X, Y, Z, 2));
        SeaEmperorBabyRules.GetClaimId(X, Y, Z).Should().Be(SeaEmperorBabyRules.GetClaimId(X, Y, Z));
    }

    [TestMethod]
    public void TinyDifferencesInTheSpawnersPositionGiveTheSameIds()
    {
        SeaEmperorBabyRules.GetBabyId(X + 0.01f, Y - 0.01f, Z, 0).Should().Be(SeaEmperorBabyRules.GetBabyId(X, Y, Z, 0));
    }

    [TestMethod]
    public void EachSpawnPointGetsItsOwnBabyAndBall()
    {
        SeaEmperorBabyRules.GetBabyId(X, Y, Z, 0).Should().NotBe(SeaEmperorBabyRules.GetBabyId(X, Y, Z, 1));
        SeaEmperorBabyRules.GetBabyId(X, Y, Z, 0).Should().NotBe(SeaEmperorBabyRules.GetCureBallId(X, Y, Z, 0));
        SeaEmperorBabyRules.GetBabyId(X, Y, Z, 0).Should().NotBe(SeaEmperorBabyRules.GetClaimId(X, Y, Z));
    }

    [TestMethod]
    public void AnotherSpawnerMakesOtherBabies()
    {
        SeaEmperorBabyRules.GetBabyId(X + 100f, Y, Z, 0).Should().NotBe(SeaEmperorBabyRules.GetBabyId(X, Y, Z, 0));
    }

    [TestMethod]
    public void OutsideBabyStopsDroppingAtItsLimit()
    {
        SeaEmperorBabyRules.StillDrops(0, SeaEmperorBabyRules.OUTSIDE_DROP_LIMIT).Should().BeTrue();
        SeaEmperorBabyRules.StillDrops(4, SeaEmperorBabyRules.OUTSIDE_DROP_LIMIT).Should().BeTrue();
        SeaEmperorBabyRules.StillDrops(5, SeaEmperorBabyRules.OUTSIDE_DROP_LIMIT).Should().BeFalse();
    }

    [TestMethod]
    public void BabyWithoutALimitKeepsDropping()
    {
        // DropEnzymes.spawnLimit is -1 for the babies in the prison
        SeaEmperorBabyRules.StillDrops(100, -1).Should().BeTrue();
    }
}
