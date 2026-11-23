using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Nitrox.Server.Subnautica.Models.Logging;

/// <summary>
///     Thins out the server's log files, which roll daily and were never deleted.
/// </summary>
/// <remarks>
///     The launcher and game logs already keep ten files each; the server's are written by another logger and had no
///     limit at all.
/// </remarks>
internal static class ServerLogRetention
{
    public const string SERVER_LOG_PATTERN = "*_server_*.log";
    public static readonly TimeSpan MAX_AGE = TimeSpan.FromDays(14);
    public const long MAX_TOTAL_BYTES = 200L * 1024 * 1024;

    /// <summary>
    ///     Selects logs older than <see cref="MAX_AGE" />, then the oldest until the rest fit in
    ///     <see cref="MAX_TOTAL_BYTES" />. Logs written in the last day are always kept: one of them is being written to.
    /// </summary>
    public static IReadOnlyList<T> SelectToDelete<T>(IEnumerable<(T Item, DateTime LastWrite, long Length)> logs, DateTime now)
    {
        List<(T Item, DateTime LastWrite, long Length)> newestFirst = [..logs.OrderByDescending(log => log.LastWrite)];
        List<T> toDelete = [];
        long kept = 0;
        foreach ((T item, DateTime lastWrite, long length) in newestFirst)
        {
            TimeSpan age = now - lastWrite;
            bool recent = age < TimeSpan.FromDays(1);
            if (recent || (age <= MAX_AGE && kept + length <= MAX_TOTAL_BYTES))
            {
                kept += length;
                continue;
            }
            toDelete.Add(item);
        }
        return toDelete;
    }

    /// <returns>How many logs were deleted.</returns>
    public static int Prune(string logsDir, DateTime now)
    {
        if (!Directory.Exists(logsDir))
        {
            return 0;
        }
        IEnumerable<(FileInfo, DateTime, long)> logs = new DirectoryInfo(logsDir).EnumerateFiles(SERVER_LOG_PATTERN).Select(file => (file, file.LastWriteTime, file.Length));
        int deleted = 0;
        foreach (FileInfo file in SelectToDelete(logs, now))
        {
            try
            {
                file.Delete();
                deleted++;
            }
            catch (IOException)
            {
                // Still open in another server
            }
        }
        return deleted;
    }
}
