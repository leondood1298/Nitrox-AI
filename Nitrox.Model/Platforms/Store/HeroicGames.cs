using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Nitrox.Model.Constants;
using Nitrox.Model.Helper;
using Nitrox.Model.Platforms.Discovery.InstallationFinders;
using Nitrox.Model.Platforms.Discovery.InstallationFinders.Core;
using Nitrox.Model.Platforms.Discovery.Models;
using Nitrox.Model.Platforms.OS.Shared;
using Nitrox.Model.Platforms.Store.Interfaces;

namespace Nitrox.Model.Platforms.Store;

public sealed class HeroicGames : IGamePlatform
{
    public string Name => "Heroic Games Launcher";
    public Platform Platform => Platform.HEROIC;

    public bool OwnsGame(string gameDirectory)
    {
        HeroicGamesFinder finder = new();

        // TODO: Limit "FindGame" calls on last folder name of the path.

        // Heroic Games has no files inside or in the parent of the game directory
        GameFinderResult? resultSubnautica = finder.FindGame(GameInfo.Subnautica);
        if (resultSubnautica.IsOk && PathEquals(gameDirectory, resultSubnautica.Path))
        {
            return true;
        }

        GameFinderResult? resultBelowZero = finder.FindGame(GameInfo.SubnauticaBelowZero);
        if (resultBelowZero.IsOk && PathEquals(gameDirectory, resultBelowZero.Path))
        {
            return true;
        }

        return false;

        static string PathNormalize(string path) => Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        static bool PathEquals(string path1, string path2) => string.Equals(PathNormalize(path1), PathNormalize(path2), StringComparison.Ordinal);
    }

    /// <param name="launcherPath">The launcher folder for a multiplayer start, null for a single-player start.</param>
    public static async Task<ProcessEx> StartGameAsync(string egsNamespace, string launchArguments, string? launcherPath)
    {
        string launchUri = BuildLaunchUri(egsNamespace, launchArguments, launcherPath, RuntimeInformation.IsOSPlatform(OSPlatform.Windows));

        // Heroic starts the game itself, so nothing is passed through this process's environment: Windows refuses
        // environment variables together with UseShellExecute, which the URI needs (upstream issue 2834).
        ProcessStartInfo startInfo = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? new ProcessStartInfo(launchUri) { UseShellExecute = true }
            : new ProcessStartInfo("xdg-open", $"\"{launchUri}\"");

        Log.Info($"Starting game with heroic uri: {launchUri}");
        return await Task.FromResult(ProcessEx.From(startInfo));
    }

    /// <summary>
    ///     Every argument is its own escaped "arg" parameter, so paths with spaces, '&amp;' or '#' survive.
    /// </summary>
    internal static string BuildLaunchUri(string egsNamespace, string launchArguments, string? launcherPath, bool isWindows)
    {
        StringBuilder launchCmd = new("heroic://launch?runner=legendary");
        launchCmd.Append("&appName=").Append(Uri.EscapeDataString(egsNamespace));

        List<string> arguments = [];
        if (launcherPath != null)
        {
            // The game runs inside Wine off Windows, where the host's root is drive Z:
            arguments.Add("--nitrox");
            arguments.Add(isWindows ? launcherPath : $"Z:{launcherPath.Replace('/', '\\')}");
        }
        arguments.AddRange(GameLaunchArguments.Split(launchArguments));

        foreach (string argument in arguments)
        {
            launchCmd.Append("&arg=").Append(Uri.EscapeDataString(argument));
        }
        return launchCmd.ToString();
    }
}
