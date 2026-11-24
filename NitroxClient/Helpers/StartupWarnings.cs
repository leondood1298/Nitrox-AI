using System.Collections.Generic;
using System.Linq;
using Nitrox.Model.Helper;

namespace NitroxClient.Helpers;

/// <summary>
///     What went wrong while Nitrox started inside the game, kept for the main menu and the launcher.
/// </summary>
public static class StartupWarnings
{
    private const int MAX_PATCHES_NAMED = 5;
    private static readonly List<string> failedPatches = [];

    public static IReadOnlyList<string> FailedPatches => failedPatches;

    public static void AddFailedPatch(string patchName, string reason) => failedPatches.Add($"{patchName}: {reason}");

    /// <param name="format">A sentence holding "{patches}" where the failed patches are named.</param>
    public static string DescribeFailedPatches(string format, IEnumerable<string> patches)
    {
        List<string> names = patches.Select(patch => patch.Split(':')[0]).ToList();
        string named = string.Join(", ", names.Take(MAX_PATCHES_NAMED));
        if (names.Count > MAX_PATCHES_NAMED)
        {
            named += $" (+{names.Count - MAX_PATCHES_NAMED})";
        }
        return format.Replace("{patches}", named);
    }

    /// <summary>
    ///     The message the main menu shows once per run, or null when there is nothing to say.
    /// </summary>
    /// <param name="patchesFailedFormat">Holds "{patches}".</param>
    /// <param name="untestedBuildFormat">Holds "{build}" and "{tested}".</param>
    public static string? BuildMainMenuMessage(IReadOnlyCollection<string> failed, int? gameBuild, GameBuildSupport support, int newestTested, string patchesFailedFormat, string untestedBuildFormat)
    {
        List<string> parts = [];
        if (support == GameBuildSupport.UNTESTED)
        {
            parts.Add(untestedBuildFormat.Replace("{build}", gameBuild?.ToString() ?? "?").Replace("{tested}", newestTested.ToString()));
        }
        if (failed.Count > 0)
        {
            parts.Add(DescribeFailedPatches(patchesFailedFormat, failed));
        }
        return parts.Count == 0 ? null : string.Join("\n\n", parts);
    }
}
