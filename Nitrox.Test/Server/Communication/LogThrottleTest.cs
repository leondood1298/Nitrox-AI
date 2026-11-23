using Nitrox.Server.Subnautica.Models.Communication;

namespace Nitrox.Test.Server.Communication;

[TestClass]
public sealed class LogThrottleTest
{
    [TestMethod]
    public void LetsAFewLinesThroughPerWindowAndCountsTheRest()
    {
        LogThrottle throttle = new(2, TimeSpan.FromMinutes(1));
        DateTimeOffset start = DateTimeOffset.UnixEpoch;

        bool[] logged = Enumerable.Range(0, 5).Select(i => throttle.ShouldLog(start.AddSeconds(i), out _)).ToArray();
        bool nextWindow = throttle.ShouldLog(start.AddMinutes(2), out int suppressed);

        logged.Should().Equal(true, true, false, false, false);
        nextWindow.Should().BeTrue();
        suppressed.Should().Be(3);
    }
}
