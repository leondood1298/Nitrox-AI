using System.Collections.Generic;
using Nitrox.Model.Constants;
using Nitrox.Model.Helper;

namespace Nitrox.Model.Platforms.Store;

/// <summary>
///     The environment a game started by the launcher gets.
/// </summary>
/// <remarks>
///     The game's code file stays patched between runs; Nitrox loads only when the game is told where the launcher is.
///     A single-player start passes a null launcher path, so the variable is taken away and the game runs vanilla.
/// </remarks>
public static class GameStartEnvironment
{
    public static (string Key, string? Value)[] For(string? launcherPath) =>
    [
        (NitroxUser.LAUNCHER_PATH_ENV_KEY, launcherPath),
        (NitroxConstants.HOST_HOME_ENV_VAR_NAME, NitroxDirectory.HomePath),
        (NitroxDirectory.DATA_PATH_ENV_VAR_NAME, NitroxDirectory.UserDataPath)
    ];

    public static void ApplyTo(IDictionary<string, string?> environment, string? launcherPath)
    {
        foreach ((string key, string? value) in For(launcherPath))
        {
            if (value == null)
            {
                environment.Remove(key);
            }
            else
            {
                environment[key] = value;
            }
        }
    }
}
