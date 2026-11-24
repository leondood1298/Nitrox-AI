using System.IO;
using System.Threading.Tasks;
using Nitrox.Model.Constants;
using Nitrox.Model.Helper;
using Nitrox.Model.Platforms.Discovery.Models;
using Nitrox.Model.Platforms.OS.Shared;
using Nitrox.Model.Platforms.Store.Interfaces;

namespace Nitrox.Model.Platforms.Store;

public sealed class Discord : IGamePlatform
{
    public string Name => nameof(Discord);
    public Platform Platform => Platform.DISCORD;

    public bool OwnsGame(string gameDirectory)
    {
        return File.Exists(
            Path.Combine(Path.GetDirectoryName(Path.GetFullPath(gameDirectory)) ?? "..", "journal.sqlite")
        );
    }

    /// <param name="launcherPath">The launcher folder for a multiplayer start, null for a single-player start.</param>
    public static async Task<ProcessEx> StartGameAsync(string pathToGameExe, string launchArguments, string? launcherPath)
    {
        return await Task.FromResult(
            ProcessEx.Start(
                pathToGameExe,
                GameStartEnvironment.For(launcherPath),
                Path.GetDirectoryName(pathToGameExe),
                launchArguments
            )
        );
    }
}
