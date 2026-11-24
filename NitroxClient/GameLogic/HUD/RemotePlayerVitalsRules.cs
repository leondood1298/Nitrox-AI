using Nitrox.Model.DataStructures.GameLogic;

namespace NitroxClient.GameLogic.HUD;

/// <summary>
///     Which parts of the name tag and bars above another player are shown. The name always is; the bars only near the
///     player, and only those the player's game mode uses.
/// </summary>
public static class RemotePlayerVitalsRules
{
    /// <summary>
    ///     Further away the bars are too small to read and only clutter the view.
    /// </summary>
    public const float BARS_VISIBLE_DISTANCE = 30f;

    /// <summary>
    ///     Health and oxygen: every mode but Creative, which has neither.
    /// </summary>
    public static bool ShowsHealthAndOxygen(SubnauticaGameMode gameMode, float distance)
    {
        return gameMode != SubnauticaGameMode.CREATIVE && distance <= BARS_VISIBLE_DISTANCE;
    }

    /// <summary>
    ///     Food and water: only where the player gets hungry and thirsty, so not in Freedom or Creative.
    /// </summary>
    public static bool ShowsFoodAndWater(SubnauticaGameMode gameMode, float distance)
    {
        return gameMode is SubnauticaGameMode.SURVIVAL or SubnauticaGameMode.HARDCORE && distance <= BARS_VISIBLE_DISTANCE;
    }
}
