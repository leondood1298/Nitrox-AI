using Nitrox.Server.Subnautica.Models.Logging;

namespace Nitrox.Test.Server.Logging;

[TestClass]
public sealed class ServerLogRetentionTest
{
    private const long MEGABYTE = 1024 * 1024;
    private static readonly DateTime now = new(2026, 11, 23, 18, 0, 0);

    [TestMethod]
    public void LogsOlderThanTwoWeeksGo()
    {
        (string, DateTime, long)[] logs = [("today", now.AddHours(-1), MEGABYTE), ("last week", now.AddDays(-7), MEGABYTE), ("last month", now.AddDays(-30), MEGABYTE)];

        ServerLogRetention.SelectToDelete(logs, now).Should().Equal("last month");
    }

    [TestMethod]
    public void OldestGoFirstWhenTheyTakeTooMuchSpace()
    {
        (string, DateTime, long)[] logs = [("today", now.AddHours(-2), 150 * MEGABYTE), ("yesterday", now.AddDays(-1.5), 40 * MEGABYTE), ("before", now.AddDays(-3), 40 * MEGABYTE)];

        ServerLogRetention.SelectToDelete(logs, now).Should().Equal("before");
    }

    [TestMethod]
    public void TodaysLogIsKeptWhateverItsSize()
    {
        (string, DateTime, long)[] logs = [("today", now.AddMinutes(-5), 500 * MEGABYTE)];

        ServerLogRetention.SelectToDelete(logs, now).Should().BeEmpty();
    }

    [TestMethod]
    public void PruneLeavesOtherLogsAlone()
    {
        string logsDir = Path.Combine(Path.GetTempPath(), $"nitrox-logs-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(logsDir);
        try
        {
            string oldServerLog = Path.Combine(logsDir, "2026-10-01_server_My World_000.log");
            string oldGameLog = Path.Combine(logsDir, "game-20261001.log");
            File.WriteAllText(oldServerLog, "");
            File.WriteAllText(oldGameLog, "");
            File.SetLastWriteTime(oldServerLog, now.AddDays(-50));
            File.SetLastWriteTime(oldGameLog, now.AddDays(-50));

            ServerLogRetention.Prune(logsDir, now).Should().Be(1);

            File.Exists(oldServerLog).Should().BeFalse();
            File.Exists(oldGameLog).Should().BeTrue("the game and launcher logs have their own limit");
        }
        finally
        {
            Directory.Delete(logsDir, true);
        }
    }
}
