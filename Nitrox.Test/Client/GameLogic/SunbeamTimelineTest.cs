using Nitrox.Model.Subnautica.DataStructures.GameLogic;

namespace NitroxClient.GameLogic;

/// <summary>
///     A player who joins during the Sunbeam's countdown sees it, also in its last seconds, and one who joins after the
///     range check gets the shoot-down line for its own position, not the lines the other games picked.
/// </summary>
[TestClass]
public sealed class SunbeamTimelineTest
{
    private const string IN_RANGE = "PDASunbeamDestroyEventInRange";
    private const string OUT_OF_RANGE = "PDASunbeamDestroyEventOutOfRange";

    private static readonly (string Trigger, string Key, float Delay)[] steps =
    [
        ("OnPlayRadioSunbeam3", "RadioSunbeam4", 600f),
        ("OnPlayRadioSunbeam4", "PrecursorGunAimCheck", 2370f),
        ("PrecursorGunAimCheck", "PrecursorGunAim", 20f),
        ("PrecursorGunAim", "SunbeamCheckPlayerRange", 10f),
        ("SunbeamCheckPlayerRange", "SunbeamAfterTheCheck", 5f)
    ];

    [TestMethod]
    public void TheCountdownStartsTheDelayOfItsFirstGoalBeforeThatGoal()
    {
        SunbeamTimeline.TryGetCountdownStart(steps, [Scheduled("PrecursorGunAimCheck", 5000f)], out float start).Should().BeTrue();

        start.Should().Be(2630f);
    }

    [TestMethod]
    public void InTheLastSecondsTheCountdownIsFoundFromTheGoalsAfterTheFirst()
    {
        SunbeamTimeline.TryGetCountdownStart(steps, [Scheduled("SunbeamCheckPlayerRange", 5030f)], out float start).Should().BeTrue();

        start.Should().Be(2630f);
    }

    [TestMethod]
    public void NoCountdownWithoutAScheduledGoalOfIt()
    {
        // The radio message before the countdown and the goals after the range check do not belong to it
        SunbeamTimeline.TryGetCountdownStart(steps, [Scheduled("RadioSunbeam4", 100f), Scheduled("SunbeamAfterTheCheck", 5035f)], out _).Should().BeFalse();
    }

    [TestMethod]
    public void AJoinerGetsItsOwnLineWhenTheServerHasThatPick()
    {
        NitroxScheduledGoal[] scheduled = [Scheduled(IN_RANGE, 5040f), Scheduled(OUT_OF_RANGE, 5060f)];

        SunbeamTimeline.TryGetOwnDestroyLineTime(OUT_OF_RANGE, 30f, IN_RANGE, 10f, scheduled, 5035, out float time).Should().BeTrue();

        time.Should().Be(5060f);
    }

    [TestMethod]
    public void AJoinerGetsItsOwnLineAtItsOwnDelayWhenOnlyAnotherPickIsScheduled()
    {
        // The range check ran at 5030: the other game picked the line 10 seconds after it, this one comes 30 seconds after
        SunbeamTimeline.TryGetOwnDestroyLineTime(OUT_OF_RANGE, 30f, IN_RANGE, 10f, [Scheduled(IN_RANGE, 5040f)], 5035, out float time).Should().BeTrue();

        time.Should().Be(5060f);
    }

    [TestMethod]
    public void AJoinerGetsNoLineWhenItsMomentHasPassed()
    {
        SunbeamTimeline.TryGetOwnDestroyLineTime(IN_RANGE, 10f, OUT_OF_RANGE, 30f, [Scheduled(OUT_OF_RANGE, 5060f)], 5045, out _).Should().BeFalse();
    }

    [TestMethod]
    public void AJoinerGetsNoLineWhenNoGamePickedOne()
    {
        SunbeamTimeline.TryGetOwnDestroyLineTime(IN_RANGE, 10f, OUT_OF_RANGE, 30f, [Scheduled("PrecursorGunAim", 5020f)], 5000, out _).Should().BeFalse();
    }

    private static NitroxScheduledGoal Scheduled(string key, float timeExecute) => new(timeExecute, key, 0);
}
