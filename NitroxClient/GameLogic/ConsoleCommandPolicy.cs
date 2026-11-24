using System;
using System.Collections.Generic;
using Nitrox.Model.DataStructures.GameLogic;

namespace NitroxClient.GameLogic;

/// <summary>
///     Who may use the game's developer console in multiplayer, and which of its commands are refused because they change
///     only this player's game and would split the shared world.
/// </summary>
/// <remarks>
///     Enforced by this game only: the server checks the commands it is sent (time, game mode, Aurora, Sunbeam, fast
///     hatch and grow), not what a modified game does with the console.
/// </remarks>
public static class ConsoleCommandPolicy
{
    /// <summary>
    ///     The rank the console needs. The commands it sends to the server need admin too, so a lower rank would only get
    ///     refusals.
    /// </summary>
    public const Perms REQUIRED_PERMS = Perms.ADMIN;

    /// <summary>
    ///     Commands that change only this game: terrain, world objects, doors, story events, time, base damage and loot that
    ///     no other player would see, or that would be undone by the server.
    /// </summary>
    private static readonly HashSet<string> REFUSED = new(StringComparer.OrdinalIgnoreCase)
    {
        "autobuildbase",
        "collect",
        "create",
        "crush",
        "damagebase",
        "damagesub",
        "daynight",
        "dig",
        "dummy",
        "endsession",
        "entreset",
        "fixleaks",
        "flood",
        "forcerocketready",
        "gamereset",
        "infectionreveal",
        "killent",
        "lavashell",
        "pcannon",
        "playerinfection",
        "pulseradio",
        "randomloot",
        "randomsupplyloot",
        "rebuildbase",
        "schedule",
        "setwpcage",
        "spawnloot",
        "spawnperf",
        "unlockdoors"
    };

    /// <summary>
    ///     Whether the console may be opened: the server's "allow commands" switch holds only below <see cref="REQUIRED_PERMS" />.
    /// </summary>
    public static bool IsConsoleAllowed(bool disabledByServer, Perms perms) => !disabledByServer || perms >= REQUIRED_PERMS;

    public static bool MaySubmit(Perms perms) => perms >= REQUIRED_PERMS;

    /// <param name="line">The console line as typed, with its arguments.</param>
    public static bool IsRefused(string line) => REFUSED.Contains(GetCommandName(line));

    private static string GetCommandName(string line)
    {
        string trimmed = (line ?? "").Trim();
        int end = trimmed.IndexOfAny([' ', '\t']);
        return end == -1 ? trimmed : trimmed.Substring(0, end);
    }
}
