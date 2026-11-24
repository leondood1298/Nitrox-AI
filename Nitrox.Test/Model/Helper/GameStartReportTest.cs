namespace Nitrox.Model.Helper;

[TestClass]
public class GameStartReportTest
{
    [TestMethod]
    public void AReportReadsBackAsWritten()
    {
        GameStartReport report = new(new DateTime(2026, 11, 23, 20, 0, 0, DateTimeKind.Utc), true, ["Patch A failed:\nline two", "Patch B failed."]);

        GameStartReport? read = GameStartReport.Parse(report.Serialize());

        read!.WrittenUtc.Should().Be(report.WrittenUtc);
        read.Loaded.Should().BeTrue();
        read.Problems.Should().Equal("Patch A failed: line two", "Patch B failed.");
    }

    [TestMethod]
    public void AnythingElseIsNoReport()
    {
        GameStartReport.Parse(null).Should().BeNull();
        GameStartReport.Parse("").Should().BeNull();
        GameStartReport.Parse("12345").Should().BeNull();
        GameStartReport.Parse("abc\nloaded").Should().BeNull();
        GameStartReport.Parse("12345\nmaybe").Should().BeNull();
    }

    [TestMethod]
    public void OnlyAReportFromThisStartCounts()
    {
        string file = Path.Combine(Path.GetTempPath(), $"GameStartReportTest_{Guid.NewGuid():N}.txt");
        try
        {
            DateTime written = DateTime.UtcNow;
            new GameStartReport(written, false, ["Broken."]).Write(file);

            GameStartReport.TryReadSince(file, written.AddSeconds(-1))!.Loaded.Should().BeFalse();
            GameStartReport.TryReadSince(file, written.AddSeconds(1)).Should().BeNull();
            GameStartReport.TryReadSince(file + ".missing", written).Should().BeNull();
        }
        finally
        {
            File.Delete(file);
        }
    }
}
