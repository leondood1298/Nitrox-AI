namespace NitroxClient.GameLogic;

/// <summary>
///     Restoring the Aurora takes back the goals that followed from its warnings and explosion, found in the game's
///     compound goals.
/// </summary>
[TestClass]
public sealed class AuroraFollowUpsTest
{
    private static readonly string[] auroraGoals = ["Story_AuroraWarning4", "Story_AuroraExplosion"];

    private static readonly (string Key, string[] Preconditions)[] compoundGoals =
    [
        ("RadioAfterExplosion", ["Story_AuroraExplosion"]),
        ("RadioAfterThatOneWasPlayed", ["OnPlayRadioAfterExplosion"]),
        ("GoalNeedingExplosionAndScan", ["Story_AuroraExplosion", "Goal_Scanner"]),
        ("GoalOfSomethingElse", ["Goal_Scanner"])
    ];

    [TestMethod]
    public void TheGoalsThatFollowFromTheExplosionAreFoundThroughEachOther()
    {
        List<string> followers = AuroraFollowUps.Find(auroraGoals, compoundGoals);

        followers.Should().BeEquivalentTo(
        [
            "RadioAfterExplosion", "OnPlayRadioAfterExplosion",
            "RadioAfterThatOneWasPlayed", "OnPlayRadioAfterThatOneWasPlayed",
            "GoalNeedingExplosionAndScan", "OnPlayGoalNeedingExplosionAndScan"
        ]);
    }

    [TestMethod]
    public void GoalsThatDoNotFollowFromTheAuroraAreKept()
    {
        AuroraFollowUps.Find(auroraGoals, compoundGoals).Should().NotContain(["GoalOfSomethingElse", "Story_AuroraExplosion"]);
    }

    [TestMethod]
    public void AFollowerWaitsForTheNextExplosionAgain()
    {
        HashSet<string> takenBack = ["Story_AuroraExplosion", "RadioAfterExplosion"];

        AuroraFollowUps.MustTrackAgain("RadioAfterExplosion", ["Story_AuroraExplosion"], takenBack).Should().BeTrue();
        AuroraFollowUps.MustTrackAgain("GoalOfSomethingElse", ["Goal_Scanner"], takenBack).Should().BeFalse();
    }
}
