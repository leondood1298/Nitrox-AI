using System.IO;
using System.Threading.Tasks;
using Nitrox.Model.Platforms.OS.Shared;

namespace Nitrox.Model.Platforms.Store;

public static class Standalone
{
    /// <param name="launcherPath">The launcher folder for a multiplayer start, null for a single-player start.</param>
    public static Task<ProcessEx> StartGameAsync(string pathToGameExe, string launchArguments, string? launcherPath)
    {
        return Task.FromResult(
            ProcessEx.Start(
                pathToGameExe,
                GameStartEnvironment.For(launcherPath),
                Path.GetDirectoryName(pathToGameExe),
                launchArguments
            )
        );
    }
}
