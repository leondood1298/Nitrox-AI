using System;
using System.IO;
using System.Threading.Tasks;
using Nitrox.Model.Helper;
using Nitrox.Model.Platforms.Discovery.Models;
using Nitrox.Model.Platforms.OS.Shared;
using Nitrox.Model.Platforms.Store.Interfaces;

namespace Nitrox.Model.Platforms.Store;

public sealed class MSStore : IGamePlatform
{
    public string Name => "Microsoft Store";
    public Platform Platform => Platform.MICROSOFT;

    public bool OwnsGame(string gameDirectory)
    {
        bool isLocalAppData = Path.GetFullPath(gameDirectory).StartsWith(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Packages"), StringComparison.InvariantCultureIgnoreCase);
        return isLocalAppData || File.Exists(Path.Combine(gameDirectory, "appxmanifest.xml"));
    }

    /// <param name="launcherPath">The launcher folder for a multiplayer start, null for a single-player start.</param>
    public static async Task<ProcessEx> StartGameAsync(string pathToGameExe, string launchArguments, string? launcherPath)
    {
        string nitroxArgument = launcherPath == null ? "" : $"--nitrox \"{launcherPath}\" ";
        return await Task.FromResult(
            ProcessEx.Start(
                @"C:\Windows\System32\cmd.exe",
                GameStartEnvironment.For(launcherPath),
                Path.GetDirectoryName(pathToGameExe),
                $"""/C start /b "" "{pathToGameExe}" {nitroxArgument}{launchArguments}""",
                createWindow: false)
        );
    }
}
