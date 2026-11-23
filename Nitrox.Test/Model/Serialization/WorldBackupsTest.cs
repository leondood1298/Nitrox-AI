using System.IO.Compression;
using Nitrox.Model.Serialization;
using Nitrox.Server.Subnautica.Models.Serialization.World;

namespace Nitrox.Test.Model.Serialization;

[TestClass]
public sealed class WorldBackupsTest
{
    private static readonly DateTime now = new(2026, 11, 23, 18, 0, 0);
    private string saveDir = null!;

    [TestInitialize]
    public void Setup()
    {
        saveDir = Path.Combine(Path.GetTempPath(), $"nitrox-backup-test-{Guid.NewGuid():N}", "My World");
        Directory.CreateDirectory(saveDir);
    }

    [TestCleanup]
    public void Cleanup() => Directory.Delete(Path.GetDirectoryName(saveDir)!, true);

    [TestMethod]
    public void BackupHoldsTheWorldButNoPasswordsLockOrLeftovers()
    {
        foreach (string file in new[] { "EntityData.json", "Bans.json", "server.cfg", "server.lock", "PlayerData.tmp", "EntityData_x.json.bak" })
        {
            File.WriteAllText(Path.Combine(saveDir, file), file);
        }
        Directory.CreateDirectory(Path.Combine(saveDir, ".save-previous"));
        File.WriteAllText(Path.Combine(saveDir, ".save-previous", "EntityData.json"), "old");

        string backup = WorldBackups.Create(saveDir, now);

        Path.GetFileName(backup).Should().Be("Backup - 2026-11-23 18.00.00.zip");
        using ZipArchive zip = ZipFile.OpenRead(backup);
        zip.Entries.Select(entry => entry.FullName).Should().BeEquivalentTo("EntityData.json", "Bans.json");
        File.Exists($"{backup}.tmp").Should().BeFalse();
    }

    [TestMethod]
    public void KeepsTheLastTenThenHourlyForADayThenDailyForAWeek()
    {
        // A save every two minutes for nine days
        List<(DateTime Item, DateTime Time)> backups = [];
        for (DateTime time = now; time > now.AddDays(-9); time = time.AddMinutes(-2))
        {
            backups.Add((time, time));
        }

        IReadOnlyList<DateTime> deleted = WorldBackups.SelectToDelete(backups, 10, now);
        DateTime[] kept = backups.Select(backup => backup.Item).Except(deleted).OrderByDescending(time => time).ToArray();

        kept.Take(10).Should().Equal(backups.Take(10).Select(backup => backup.Item), "the newest ten are kept as they are");
        kept.Where(time => now - time <= TimeSpan.FromHours(24)).GroupBy(time => new DateTime(time.Year, time.Month, time.Day, time.Hour, 0, 0))
            .Should().HaveCount(25, "every hour of the last day keeps one").And.OnlyContain(hour => hour.Count() <= 10);
        kept.Where(time => now - time > TimeSpan.FromHours(24)).GroupBy(time => time.Date)
            .Should().OnlyContain(day => day.Count() == 1, "older days keep one each");
        kept.Should().OnlyContain(time => now - time <= TimeSpan.FromDays(7), "nothing older than a week is kept");
        kept.Length.Should().BeLessThan(50);
    }

    [TestMethod]
    public void FewBackupsAreAllKept()
    {
        List<(string Item, DateTime Time)> backups = [("a", now.AddDays(-30)), ("b", now.AddMinutes(-2))];

        WorldBackups.SelectToDelete(backups, 10, now).Should().BeEmpty();
    }

    [TestMethod]
    public void PruneDeletesOnlyWhatRetentionDoesNotKeep()
    {
        string backupsDir = Path.Combine(saveDir, WorldBackups.FOLDER_NAME);
        Directory.CreateDirectory(backupsDir);
        File.WriteAllText(Path.Combine(backupsDir, "Backup - 2026-11-01 10.00.00.zip"), "");
        File.WriteAllText(Path.Combine(backupsDir, "Backup - 2026-11-23 17.58.00.zip"), "");
        File.WriteAllText(Path.Combine(backupsDir, "something else.zip"), "");

        IReadOnlyList<string> deleted = WorldBackups.Prune(saveDir, 1, now);

        deleted.Select(Path.GetFileName).Should().Equal("Backup - 2026-11-01 10.00.00.zip");
        File.Exists(Path.Combine(backupsDir, "something else.zip")).Should().BeTrue();
    }

    [TestMethod]
    public void RestoreKeepsTheReplacedStateAndTheSettings()
    {
        File.WriteAllText(Path.Combine(saveDir, "EntityData.json"), "backed up");
        string backup = WorldBackups.Create(saveDir, now.AddHours(-1));
        File.WriteAllText(Path.Combine(saveDir, "EntityData.json"), "current");
        File.WriteAllText(Path.Combine(saveDir, "Extra.json"), "current only");
        File.WriteAllText(Path.Combine(saveDir, "server.cfg"), "ServerPassword=secret");
        Directory.CreateDirectory(Path.Combine(saveDir, ".save-previous"));

        string? kept = WorldBackups.Restore(saveDir, backup, now);

        File.ReadAllText(Path.Combine(saveDir, "EntityData.json")).Should().Be("backed up");
        File.Exists(Path.Combine(saveDir, "Extra.json")).Should().BeFalse();
        File.ReadAllText(Path.Combine(saveDir, "server.cfg")).Should().Be("ServerPassword=secret");
        Directory.Exists(Path.Combine(saveDir, ".save-previous")).Should().BeFalse();
        using ZipArchive keptZip = ZipFile.OpenRead(kept!);
        keptZip.Entries.Select(entry => entry.FullName).Should().BeEquivalentTo("EntityData.json", "Extra.json");
    }

    [TestMethod]
    public void DamagedBackupLeavesTheWorldAlone()
    {
        File.WriteAllText(Path.Combine(saveDir, "EntityData.json"), "current");
        string broken = Path.Combine(saveDir, "broken.zip");
        File.WriteAllText(broken, "not a zip");

        Action restore = () => WorldBackups.Restore(saveDir, broken, now);

        restore.Should().Throw<InvalidDataException>();
        File.ReadAllText(Path.Combine(saveDir, "EntityData.json")).Should().Be("current");
    }

    [TestMethod]
    public void ServerWorkingFoldersAreRecognisedByTheirPrefix()
    {
        SaveGenerationStore.WorkingFolderNames.Should().OnlyContain(name => name.StartsWith(WorldBackups.SAVE_WORKING_FOLDER_PREFIX));
    }
}
