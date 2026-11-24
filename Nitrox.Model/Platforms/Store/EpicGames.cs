using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Nitrox.Model.Constants;
using Nitrox.Model.Helper;
using Nitrox.Model.Platforms.Discovery.Models;
using Nitrox.Model.Platforms.OS.Shared;
using Nitrox.Model.Platforms.Store.Interfaces;

namespace Nitrox.Model.Platforms.Store;

public sealed class EpicGames : IGamePlatform
{
    public string Name => "Epic Games Store";
    public Platform Platform => Platform.EPIC;

    public bool OwnsGame(string gameDirectory)
    {
        string path = Path.Combine(gameDirectory, ".egstore");
        
        try
        {
            return Directory.EnumerateFiles(path, "*.manifest", SearchOption.TopDirectoryOnly).Any();
        }
        catch (Exception ex)
        {
            Log.Error(ex);
            return false;
        }
    }

    /// <param name="launcherPath">The launcher folder for a multiplayer start, null for a single-player start.</param>
    public static async Task<ProcessEx> StartGameAsync(string pathToGameExe, string launchArguments, string? launcherPath)
    {
        // Normally should call StartPlatformAsync first. But Subnautica will start without EGS.
        return await Task.FromResult(
            ProcessEx.Start(
                pathToGameExe,
                GameStartEnvironment.For(launcherPath),
                Path.GetDirectoryName(pathToGameExe),
                $"-EpicPortal -epicuserid=0 {launchArguments}")
        );
    }
}
