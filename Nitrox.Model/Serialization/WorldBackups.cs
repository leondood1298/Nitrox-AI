using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Nitrox.Model.Constants;

namespace Nitrox.Model.Serialization;

/// <summary>
///     Backups of a world: "backups/Backup - {time}.zip" in the world folder, made by the server after a save and by the
///     launcher before it restores one.
/// </summary>
public static class WorldBackups
{
    public const string FOLDER_NAME = "backups";
    public const string FILE_PREFIX = "Backup - ";

    /// <summary>
    ///     Not world state: the settings hold the server and admin passwords, and the lock belongs to a running server.
    /// </summary>
    private static readonly string[] excludedFileNames = ["server.cfg", "server.lock"];

    /// <summary>
    ///     Left behind by a write that was cut short.
    /// </summary>
    private static readonly string[] excludedExtensions = [".tmp", ".bak"];

    /// <summary>
    ///     Whether a file in the world folder belongs in a backup. Only files directly in the world folder are world state;
    ///     its subfolders hold backups and the save's own working copies.
    /// </summary>
    public static bool IsWorldFile(string fileName) =>
        !excludedFileNames.Contains(fileName, StringComparer.OrdinalIgnoreCase) &&
        !excludedExtensions.Contains(Path.GetExtension(fileName), StringComparer.OrdinalIgnoreCase);

    public static IEnumerable<string> GetWorldFiles(string saveDir) =>
        Directory.EnumerateFiles(saveDir).Where(path => IsWorldFile(Path.GetFileName(path)));

    /// <summary>
    ///     Zips the world's files straight into a new backup. The zip only gets its name once it is complete, so a backup
    ///     cut short is never mistaken for one.
    /// </summary>
    /// <returns>The backup's path.</returns>
    public static string Create(string saveDir, DateTime time)
    {
        string backupsDir = Path.Combine(saveDir, FOLDER_NAME);
        Directory.CreateDirectory(backupsDir);
        string backupPath = Path.Combine(backupsDir, $"{FILE_PREFIX}{time.ToString(PersistanceConstants.BACKUP_DATE_TIME_FORMAT, CultureInfo.InvariantCulture)}.zip");
        string partialPath = $"{backupPath}.tmp";
        using (FileStream stream = new(partialPath, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            using (ZipArchive zip = new(stream, ZipArchiveMode.Create, true))
            {
                foreach (string file in GetWorldFiles(saveDir))
                {
                    ZipArchiveEntry entry = zip.CreateEntry(Path.GetFileName(file));
                    using Stream source = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read);
                    using Stream target = entry.Open();
                    source.CopyTo(target);
                }
            }
            stream.Flush(true);
        }
        if (File.Exists(backupPath))
        {
            File.Delete(backupPath);
        }
        File.Move(partialPath, backupPath);
        return backupPath;
    }

    /// <summary>
    ///     Replaces the world's files with those of a backup, after keeping the current ones as a backup of their own.
    /// </summary>
    /// <remarks>
    ///     Settings are left as they are: newer backups do not hold them, and an older backup would bring back old passwords.
    ///     The save's working folders go, so that nothing from before can be taken for a newer or a fallback generation.
    /// </remarks>
    /// <returns>The backup of the state that was replaced, or null when the world had no files yet.</returns>
    public static string? Restore(string saveDir, string backupPath, DateTime now)
    {
        // Read the backup first: a damaged one must not cost the current files
        using ZipArchive zip = ZipFile.OpenRead(backupPath);
        ZipArchiveEntry[] entries = zip.Entries.Where(entry => entry.FullName == entry.Name && entry.Name != "" && IsWorldFile(entry.Name)).ToArray();
        if (entries.Length == 0)
        {
            throw new InvalidDataException($"The backup \"{Path.GetFileName(backupPath)}\" holds no world files.");
        }

        string? safetyBackup = GetWorldFiles(saveDir).Any() ? Create(saveDir, now) : null;
        foreach (string file in GetWorldFiles(saveDir).ToArray())
        {
            File.Delete(file);
        }
        foreach (string folder in Directory.EnumerateDirectories(saveDir, $"{SAVE_WORKING_FOLDER_PREFIX}*").ToArray())
        {
            Directory.Delete(folder, true);
        }
        foreach (ZipArchiveEntry entry in entries)
        {
            entry.ExtractToFile(Path.Combine(saveDir, entry.Name), true);
        }
        return safetyBackup;
    }

    /// <summary>
    ///     Start of the names of the folders the server keeps in the world folder while saving.
    /// </summary>
    public const string SAVE_WORKING_FOLDER_PREFIX = ".save-";

    /// <summary>
    ///     Deletes the backups <see cref="SelectToDelete{T}" /> does not keep.
    /// </summary>
    /// <returns>The deleted backups.</returns>
    public static IReadOnlyList<string> Prune(string saveDir, int keepLast, DateTime now)
    {
        string backupsDir = Path.Combine(saveDir, FOLDER_NAME);
        if (!Directory.Exists(backupsDir))
        {
            return [];
        }
        List<(string Path, DateTime Time)> backups = [];
        foreach (string path in Directory.EnumerateFiles(backupsDir, "*.zip"))
        {
            if (TryGetTime(path, out DateTime time))
            {
                backups.Add((path, time));
            }
        }
        IReadOnlyList<string> toDelete = SelectToDelete(backups, keepLast, now);
        foreach (string path in toDelete)
        {
            File.Delete(path);
        }
        return toDelete;
    }

    /// <summary>
    ///     Keeps the newest <paramref name="keepLast" /> backups, then the newest of each hour of the last 24 hours, then the
    ///     newest of each day of the last 7 days, and selects the rest.
    /// </summary>
    /// <remarks>
    ///     A save every two minutes with only the last ten kept covered twenty minutes: damage noticed later was in every
    ///     backup already.
    /// </remarks>
    public static IReadOnlyList<T> SelectToDelete<T>(IReadOnlyList<(T Item, DateTime Time)> backups, int keepLast, DateTime now)
    {
        HashSet<DateTime> hoursKept = [];
        HashSet<DateTime> daysKept = [];
        List<T> toDelete = [];
        int kept = 0;
        foreach ((T item, DateTime time) in backups.OrderByDescending(backup => backup.Time))
        {
            DateTime hour = new(time.Year, time.Month, time.Day, time.Hour, 0, 0, time.Kind);
            DateTime day = time.Date;
            TimeSpan age = now - time;
            bool keep = kept < keepLast ||
                        (age <= TimeSpan.FromHours(24) && !hoursKept.Contains(hour)) ||
                        (age <= TimeSpan.FromDays(7) && !daysKept.Contains(day));
            if (keep)
            {
                kept++;
                hoursKept.Add(hour);
                daysKept.Add(day);
            }
            else
            {
                toDelete.Add(item);
            }
        }
        return toDelete;
    }

    /// <summary>
    ///     Whether a save made at <paramref name="now" /> is to be backed up: once <paramref name="interval" /> has passed
    ///     since the newest backup. A save every two minutes used to zip the whole world every two minutes, about 32 MB
    ///     for a well explored one, and kept up to some forty of them.
    /// </summary>
    public static bool IsBackupDue(string saveDir, DateTime now, TimeSpan interval) => IsBackupDue(NewestTime(saveDir), now, interval);

    internal static bool IsBackupDue(DateTime? newestBackup, DateTime now, TimeSpan interval) =>
        newestBackup is not { } newest || interval <= TimeSpan.Zero || now - newest >= interval || newest > now;

    /// <summary>
    ///     When the newest backup was made, or null if there is none.
    /// </summary>
    public static DateTime? NewestTime(string saveDir)
    {
        string backupsDir = Path.Combine(saveDir, FOLDER_NAME);
        if (!Directory.Exists(backupsDir))
        {
            return null;
        }
        DateTime? newest = null;
        foreach (string path in Directory.EnumerateFiles(backupsDir, "*.zip"))
        {
            if (TryGetTime(path, out DateTime time) && (newest == null || time > newest))
            {
                newest = time;
            }
        }
        return newest;
    }

    /// <summary>
    ///     The time in a backup's name, else when it was written.
    /// </summary>
    public static bool TryGetTime(string path, out DateTime time)
    {
        string name = Path.GetFileNameWithoutExtension(path);
        if (!name.StartsWith(FILE_PREFIX, StringComparison.Ordinal))
        {
            time = default;
            return false;
        }
        if (!DateTime.TryParseExact(name.Substring(FILE_PREFIX.Length), PersistanceConstants.BACKUP_DATE_TIME_FORMAT, CultureInfo.InvariantCulture, DateTimeStyles.None, out time))
        {
            time = File.GetLastWriteTime(path);
        }
        return true;
    }
}
