using System;

namespace NitroxClient.GameLogic;

/// <summary>
///     The text of a player's ping, made by the game that shows it, in that player's language.
/// </summary>
public static class PlayerPingLabels
{
    public const string TEMPLATE_KEY = "Nitrox_PlayerPinged";

    /// <param name="template">The localized <see cref="TEMPLATE_KEY" />.</param>
    /// <param name="targetTechType">The tech type of what was pinged, empty if it has none.</param>
    /// <param name="targetName">What the sender's game called it, used when there is no tech type.</param>
    /// <param name="localize">Gives the localized name of a tech type.</param>
    public static string Build(string template, string playerName, string targetTechType, string targetName, Func<string, string> localize)
    {
        string target = string.IsNullOrEmpty(targetTechType) ? targetName ?? string.Empty : localize(targetTechType);
        return template.Replace("{PLAYER}", playerName).Replace("{TARGET}", target);
    }
}
