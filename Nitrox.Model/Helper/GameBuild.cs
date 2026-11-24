using System;
using System.IO;
using Nitrox.Model.Core;

namespace Nitrox.Model.Helper;

public enum GameBuildSupport
{
    /// <summary>The build number could not be read (a missing or unreadable version file, as on very old or unusual installs).</summary>
    UNKNOWN,
    OUTDATED,
    SUPPORTED,
    /// <summary>Newer than any build Nitrox was tested against: allowed, with a warning.</summary>
    UNTESTED
}

/// <summary>
///     Reads and judges the game's build number, which the game keeps in a small text file next to its data.
/// </summary>
public static class GameBuild
{
    /// <param name="gameDataFolder">The game's data folder (Subnautica_Data, or Resources/Data on macOS).</param>
    public static string GetVersionFilePath(string gameDataFolder) => Path.Combine(gameDataFolder, "StreamingAssets", "SNUnmanagedData", "plastic_status.ignore");

    public static int? Parse(string? text) => int.TryParse(text?.Trim(), out int build) && build > 0 ? build : null;

    public static int? TryRead(string gameDataFolder)
    {
        try
        {
            return Parse(File.ReadAllText(GetVersionFilePath(gameDataFolder)));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    public static GameBuildSupport Classify(int? build, int minimum, int newestTested) =>
        build switch
        {
            null => GameBuildSupport.UNKNOWN,
            _ when build < minimum => GameBuildSupport.OUTDATED,
            _ when build > newestTested => GameBuildSupport.UNTESTED,
            _ => GameBuildSupport.SUPPORTED
        };

    public static GameBuildSupport Classify(int? build) => Classify(build, NitroxEnvironment.GameMinimumVersion, NitroxEnvironment.GameNewestTestedVersion);
}
