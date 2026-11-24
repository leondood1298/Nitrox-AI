using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Nitrox.Model.Platforms.Discovery.InstallationFinders.Core;
using static Nitrox.Model.Platforms.Discovery.InstallationFinders.Core.GameFinderResult;

namespace Nitrox.Model.Platforms.Discovery.InstallationFinders;

/// <summary>
/// MS Store games are stored under <c>[DRIVE]:\XboxGames\[GAME]\Content\</c> by default.
/// It's likely we could read the choosen path from <c>C:\Program Files\WindowsApps</c> but we're unable to read store settings from those folders.
/// </summary>
public sealed class MicrosoftFinder : IGameFinder
{
    public GameFinderResult FindGame(GameInfo gameInfo)
    {
        List<string> candidates = CandidatePaths(GetDriveRoots(), gameInfo.Name).ToList();
        foreach (string path in candidates)
        {
            if (GameInstallationHelper.HasValidGameFolder(path, gameInfo))
            {
                return Ok(path);
            }
        }

        return Error($"Game installation directory was not found in {string.Join(", ", candidates)}. Please enter the path to the '{gameInfo.Name}' installation");
    }

    /// <summary>
    ///     The store's default folder on every drive. The drive root keeps its separator: "C:" alone would mean the current
    ///     folder of drive C.
    /// </summary>
    internal static IEnumerable<string> CandidatePaths(IEnumerable<string> driveRoots, string gameName) =>
        driveRoots.Select(root => Path.Combine(root, "XboxGames", gameName, "Content"));

    internal static IEnumerable<string> GetDriveRoots()
    {
        try
        {
            return DriveInfo.GetDrives().Where(drive => drive.DriveType == DriveType.Fixed && drive.IsReady).Select(drive => drive.RootDirectory.FullName).ToList();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return [Path.GetPathRoot(Environment.GetFolderPath(Environment.SpecialFolder.Windows)) ?? @"C:\"];
        }
    }
}
