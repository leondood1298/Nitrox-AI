using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using Nitrox.Model.Platforms.Discovery.InstallationFinders.Core;
using static Nitrox.Model.Platforms.Discovery.InstallationFinders.Core.GameFinderResult;

namespace Nitrox.Model.Platforms.Discovery.InstallationFinders;

/// <summary>
/// Trying to find the path in the Heroic-Games-Launcher installation records.
/// </summary>
public sealed class HeroicGamesFinder : IGameFinder
{
    public GameFinderResult FindGame(GameInfo gameInfo)
    {
        List<string> configFoldersToTest = [];

        string appDataFolder = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string standardConfigDir = Path.Combine(appDataFolder, "heroic");
        configFoldersToTest.Add(standardConfigDir);

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            string homeFolder = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string flatpakConfigDir = Path.Combine(homeFolder, ".var", "app", "com.heroicgameslauncher.hgl", "config", "heroic");
            configFoldersToTest.Add(flatpakConfigDir);
        }

        foreach (string configFolder in configFoldersToTest)
        {
            string configFilePath = Path.Combine(configFolder, "legendaryConfig", "legendary", "installed.json");
            if (!File.Exists(configFilePath))
            {
                continue;
            }
            try
            {
                // A record can outlive the install, so only a folder that still holds the game counts.
                if (TryGetInstallPath(File.ReadAllText(configFilePath), gameInfo, out string installPath) && GameInstallationHelper.HasValidGameFolder(installPath, gameInfo))
                {
                    return Ok(installPath);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException or FormatException)
            {
                Log.Warn($"Could not read Heroic's install records at '{configFilePath}': {ex.Message}");
            }
        }

        return NotFound();
    }

    internal static bool TryGetInstallPath(string installedJson, GameInfo gameInfo, out string installPath)
    {
        JsonNode? jsonRoot = JsonNode.Parse(installedJson);
        JsonNode? installPathNode = jsonRoot?[gameInfo.EgsNamespace]?["install_path"];

        if (installPathNode != null)
        {
            installPath = installPathNode.GetValue<string>();
            return !string.IsNullOrWhiteSpace(installPath);
        }

        installPath = string.Empty;
        return false;
    }
}
