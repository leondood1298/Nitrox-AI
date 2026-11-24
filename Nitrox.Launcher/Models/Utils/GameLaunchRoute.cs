using System;

namespace Nitrox.Launcher.Models.Utils;

/// <summary>
///     Decides whether a Steam game is started through Steam or by running the game's exe directly.
/// </summary>
internal static class GameLaunchRoute
{
    /// <param name="instantLaunchPlayerCount">Players of a development instant launch, 0 when not instant launching.</param>
    /// <param name="isSteamsOwnInstall">False when the chosen install is not the one Steam has registered for the game.</param>
    public static bool ShouldSkipSteam(string launchArguments, bool bigPictureMode, bool multipleInstancesAllowed, bool gameAlreadyRunning, int instantLaunchPlayerCount, bool isSteamsOwnInstall)
    {
        // Steam starts games by app id, so it would start its own install instead of the chosen one.
        if (!isSteamsOwnInstall)
        {
            return true;
        }
        if (instantLaunchPlayerCount > 1)
        {
            return true;
        }
        // Steam does not start a second copy of a running game.
        if (multipleInstancesAllowed && gameAlreadyRunning)
        {
            return true;
        }
        if (bigPictureMode || instantLaunchPlayerCount == 1)
        {
            return false;
        }
        if (launchArguments.Contains("-vrmode none", StringComparison.OrdinalIgnoreCase))
        {
            return multipleInstancesAllowed;
        }
        // VR Mode. Can only work if NOT going through Steam as it will always add '-vrmode none' due to hard coded default Steam "launch option" args. See: https://steamdb.info/app/264710/config/
        return launchArguments.Contains("-vrmode", StringComparison.OrdinalIgnoreCase);
    }
}
