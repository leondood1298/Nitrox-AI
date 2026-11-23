namespace NitroxClient.GameLogic;

/// <summary>
///     A time skip acts on every Aurora warning and explosion step it passes, in order, as the game does when its clock
///     runs through them.
/// </summary>
[TestClass]
public sealed class AuroraTimelineTest
{
    private const float WARNING = 1000f;
    private const float COUNTDOWN = 2000f;

    [TestMethod]
    public void ASkipOverAllWarningsAndTheExplosionStepsThroughEachInOrder()
    {
        List<double> steps = AuroraTimeline.GetStepsPassed(900, 2100, WARNING, COUNTDOWN);

        steps.Select(step => (float)step).Should().Equal(
            AuroraTimeline.JustAbove(1200f),
            AuroraTimeline.JustAbove(1500f),
            AuroraTimeline.JustAbove(1800f),
            AuroraTimeline.JustAbove(2000f),
            AuroraTimeline.JustAbove(2024f),
            AuroraTimeline.JustAbove(2025f),
            AuroraTimeline.JustAbove(2027f));
    }

    [TestMethod]
    public void ASkipActsOnlyOnWhatItPasses()
    {
        List<double> steps = AuroraTimeline.GetStepsPassed(1300, 1900, WARNING, COUNTDOWN);

        steps.Select(step => (float)step).Should().Equal(AuroraTimeline.JustAbove(1500f), AuroraTimeline.JustAbove(1800f));
    }

    [TestMethod]
    public void ASkipAfterTheExplosionActsOnNothing()
    {
        AuroraTimeline.GetStepsPassed(2100, 3300, WARNING, COUNTDOWN).Should().BeEmpty();
    }

    [TestMethod]
    public void EachStepIsTakenAsPassedByTheGameButNotTheNextTime()
    {
        // ScalarMonitor.JustWentAbove compares single precision times; late game times have coarse ones
        const float LATE = 140000.5f;

        float step = AuroraTimeline.JustAbove(LATE);

        step.Should().BeGreaterThan(LATE);
        step.Should().BeLessThan(LATE + 1f);
    }
}
