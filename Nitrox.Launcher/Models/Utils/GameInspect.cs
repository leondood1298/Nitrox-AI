using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Nitrox.Launcher.Models.Services;
using Nitrox.Launcher.ViewModels;
using Nitrox.Model.Core;
using Nitrox.Model.Helper;
using Nitrox.Model.Logger;
using Nitrox.Model.Platforms.OS.Shared;

namespace Nitrox.Launcher.Models.Utils;

internal static class GameInspect
{
    /// <summary>
    ///     Blocks game builds older than Nitrox supports (including the legacy branch) and warns, but allows, builds newer
    ///     than any Nitrox was tested against or builds whose number can't be read.
    /// </summary>
    /// <returns>True if the game must not be started with Nitrox.</returns>
    public static async Task<bool> IsOutdatedGameAndNotify(string gameInstallPath, DialogService? dialogService = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(gameInstallPath);

        string gameDataFolder = Path.Combine(gameInstallPath, GameInfo.Subnautica.DataFolder);
        string gameVersionFile = GameBuild.GetVersionFilePath(gameDataFolder);
        int? gameVersion = GameBuild.TryRead(gameDataFolder);
        GameBuildSupport support = GameBuild.Classify(gameVersion);
        Log.Info($"Game build {gameVersion?.ToString() ?? "unknown"} is {support}");
        switch (support)
        {
            case GameBuildSupport.OUTDATED:
                if (dialogService != null)
                {
                    await dialogService.ShowAsync<DialogBoxViewModel>(model =>
                    {
                        model.Title = "Outdated Game Detected";
                        model.Description =
                            $"Nitrox does not support the older {GameInfo.Subnautica.FullName} game version of {gameVersion}. Please update your game to the latest version (on Steam, leave any beta or legacy branch in the game's properties).{Environment.NewLine}{Environment.NewLine}Minimum game version: {NitroxEnvironment.GameMinimumVersion}{Environment.NewLine}Version file location:{Environment.NewLine}{gameVersionFile}{Environment.NewLine}";
                        model.ButtonOptions = ButtonOptions.Ok;
                    });
                }
                return true;
            case GameBuildSupport.UNTESTED:
                LauncherNotifier.Warning($"Your {GameInfo.Subnautica.FullName} build {gameVersion} is newer than any Nitrox was tested with ({NitroxEnvironment.GameNewestTestedVersion}). It starts anyway, but parts of multiplayer may not work until Nitrox is updated.");
                return false;
            case GameBuildSupport.UNKNOWN:
                LauncherNotifier.Warning($"Nitrox could not read the game's version from '{gameVersionFile}'. It starts anyway; if multiplayer misbehaves, let your store check the game files.");
                return false;
            default:
                return false;
        }
    }

    /// <summary>
    ///     Checks game is running and if it is, warns. Does nothing in development mode for debugging purposes.
    /// </summary>
    public static bool WarnIfGameProcessExists(GameInfo game)
    {
        if (!ProcessEx.ProcessExists(game.ExeName))
        {
            return false;
        }
        LauncherNotifier.Warning($"An instance of {game.FullName} is already running");
        return true;
    }

    public static void WarnIfBepInExMods(string gamePath)
    {
        if (string.IsNullOrWhiteSpace(gamePath) || !Directory.Exists(gamePath))
        {
            return;
        }
        string bepRoot = Path.Combine(gamePath, "BepInEx");
        if (!Directory.Exists(bepRoot))
        {
            return;
        }

        int modDllCount = GetDllPaths(Path.Combine(bepRoot, "plugins")).Count();
        modDllCount += GetDllPaths(Path.Combine(bepRoot, "patchers")).Count();
        if (modDllCount > 0)
        {
            Log.Warn($"BepInEx plugins detected: {modDllCount}");
            LauncherNotifier.Warning($"BepInEx mod(s) were detected ({modDllCount}). Nitrox multiplayer does not support mods and they may cause instability.");
        }

        static IEnumerable<string> GetDllPaths(string path)
        {
            try
            {
                return Directory.EnumerateFiles(path, "*.dll", SearchOption.AllDirectories);
            }
            catch (IOException)
            {
                return [];
            }
        }
    }
}
