using Nitrox.Server.Subnautica.Models.Administration;

namespace Nitrox.Test.Server.GameLogic;

/// <summary>
///     The join password and /login could be guessed as fast as a script could send.
/// </summary>
[TestClass]
public sealed class GuessLimiterTest
{
    [TestMethod]
    public void AFewTyposCostNothing()
    {
        GuessLimiter limiter = new(new FakeTimeProvider());
        for (int i = 0; i < GuessLimiter.FREE_FAILURES - 1; i++)
        {
            limiter.RecordFailure("address:1");
        }

        limiter.GetRemainingWait("address:1").Should().Be(TimeSpan.Zero);
    }

    [TestMethod]
    public void TheWaitGrowsWithEveryFailure()
    {
        FakeTimeProvider time = new();
        GuessLimiter limiter = new(time);
        for (int i = 0; i < GuessLimiter.FREE_FAILURES; i++)
        {
            limiter.RecordFailure("address:1", "session:1");
        }
        TimeSpan first = limiter.GetRemainingWait("address:1");
        time.Advance(first);
        limiter.RecordFailure("address:1", "session:2");

        first.Should().Be(GuessLimiter.FIRST_WAIT);
        limiter.GetRemainingWait("address:1").Should().Be(GuessLimiter.FIRST_WAIT * 2);
        limiter.GetRemainingWait("session:2").Should().Be(TimeSpan.Zero, "a new connection is counted on its own, and held back by its address");
        GuessLimiter.GetWait(100).Should().Be(GuessLimiter.LONGEST_WAIT);
    }

    [TestMethod]
    public void TheRightPasswordClearsTheCount()
    {
        GuessLimiter limiter = new(new FakeTimeProvider());
        for (int i = 0; i < GuessLimiter.FREE_FAILURES; i++)
        {
            limiter.RecordFailure("address:1");
        }

        limiter.RecordSuccess("address:1");

        limiter.GetRemainingWait("address:1").Should().Be(TimeSpan.Zero);
    }

    [TestMethod]
    public void PasswordsAreComparedWhole()
    {
        SecretComparison.AreEqual("secret", "secret").Should().BeTrue();
        SecretComparison.AreEqual("secre", "secret").Should().BeFalse();
        SecretComparison.AreEqual(null, "").Should().BeTrue();
    }

    private sealed class FakeTimeProvider : TimeProvider
    {
        private DateTimeOffset now = DateTimeOffset.UnixEpoch;

        public override DateTimeOffset GetUtcNow() => now;

        public void Advance(TimeSpan duration) => now += duration;
    }
}
