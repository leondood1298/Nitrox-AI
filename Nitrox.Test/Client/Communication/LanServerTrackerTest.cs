using System.Net;
using NitroxClient.Communication;

namespace Nitrox.Test.Client.Communication;

[TestClass]
public sealed class LanServerTrackerTest
{
    private static readonly DateTime start = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
    private static readonly IPEndPoint server = new(IPAddress.Parse("192.168.1.20"), 11000);

    [TestMethod]
    public void AServerIsNewOnlyAtItsFirstAnswer()
    {
        LanServerTracker tracker = new();
        tracker.Answered(server, start).Should().BeTrue();
        tracker.Answered(new IPEndPoint(IPAddress.Parse("192.168.1.20"), 11000), start.AddSeconds(3)).Should().BeFalse();
    }

    [TestMethod]
    public void AServerThatKeepsAnsweringStays()
    {
        LanServerTracker tracker = new();
        tracker.Answered(server, start);
        tracker.Answered(server, start.AddSeconds(9));

        tracker.TakeSilent(start.AddSeconds(15)).Should().BeEmpty();
    }

    [TestMethod]
    public void AServerThatStoppedAnsweringIsDropped()
    {
        LanServerTracker tracker = new();
        tracker.Answered(server, start);

        tracker.TakeSilent(start + LanServerTracker.EXPIRY + TimeSpan.FromSeconds(1)).Should().Equal(server);
        tracker.Known.Should().BeEmpty();
        tracker.Answered(server, start.AddMinutes(1)).Should().BeTrue("a server that comes back is found again");
    }

    [TestMethod]
    public void TimeWithTheListClosedDoesNotDropAServer()
    {
        LanServerTracker tracker = new();
        tracker.Answered(server, start);

        tracker.RestartWaiting(start.AddMinutes(5));

        tracker.TakeSilent(start.AddMinutes(5).AddSeconds(2)).Should().BeEmpty();
    }
}
