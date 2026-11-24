using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace Nitrox.Model.Helper;

/// <summary>
///     What the game writes once Nitrox has (or has not) loaded into it, so the launcher can tell the player when a game
///     it started came up without Nitrox.
/// </summary>
/// <param name="Problems">Patches that failed and similar, one sentence each.</param>
public sealed record GameStartReport(DateTime WrittenUtc, bool Loaded, IReadOnlyList<string> Problems)
{
    public const string FILE_NAME = "game-start-report.txt";
    private const string LOADED = "loaded";
    private const string FAILED = "failed";

    public static string DefaultFilePath => Path.Combine(NitroxDirectory.CachePath, FILE_NAME);

    public string Serialize() =>
        string.Join("\n", new[] { WrittenUtc.Ticks.ToString(CultureInfo.InvariantCulture), Loaded ? LOADED : FAILED }
                          .Concat(Problems.Select(problem => problem.Replace('\r', ' ').Replace('\n', ' '))));

    public static GameStartReport? Parse(string? text)
    {
        string[] lines = text?.Split('\n') ?? [];
        if (lines.Length < 2 || !long.TryParse(lines[0].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out long ticks) || ticks < DateTime.MinValue.Ticks || ticks > DateTime.MaxValue.Ticks)
        {
            return null;
        }
        string state = lines[1].Trim();
        if (state is not (LOADED or FAILED))
        {
            return null;
        }
        return new GameStartReport(new DateTime(ticks, DateTimeKind.Utc), state == LOADED, lines.Skip(2).Select(line => line.Trim()).Where(line => line.Length > 0).ToList());
    }

    /// <summary>
    ///     Writes the report whole: a reader sees the old report or the new one, never half of one.
    /// </summary>
    public void Write(string filePath)
    {
        string? folder = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(folder))
        {
            Directory.CreateDirectory(folder);
        }
        string temporary = $"{filePath}.{Guid.NewGuid():N}.tmp";
        File.WriteAllText(temporary, Serialize());
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }
        File.Move(temporary, filePath);
    }

    /// <returns>The report if one was written at or after <paramref name="sinceUtc" />, otherwise null.</returns>
    public static GameStartReport? TryReadSince(string filePath, DateTime sinceUtc)
    {
        try
        {
            GameStartReport? report = Parse(File.ReadAllText(filePath));
            return report != null && report.WrittenUtc >= sinceUtc ? report : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
