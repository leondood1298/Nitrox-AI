using System;
using System.Collections.Generic;
using System.Linq;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;

namespace Nitrox.Server.Subnautica.Models.GameLogic.Players;

/// <summary>
///     Each player's colour and visibility of pings (beacons, vehicles, signals, other players) are their own and kept in
///     their saved player data. The key is whatever the game sent, so it is bounded here; and the key of a beacon or
///     vehicle that is gone would otherwise stay in the player's data for good.
/// </summary>
internal static class PingPreferenceAuthority
{
    /// <summary>
    ///     A ping key is a Nitrox id (36 characters) or a story signal's description key, which are short.
    /// </summary>
    public const int MAX_KEY_LENGTH = 128;

    /// <summary>
    ///     Far above the pings a world has, so that only a misbehaving client reaches it.
    /// </summary>
    public const int MAX_ENTRIES = 2048;

    /// <returns>Whether the preference was stored.</returns>
    public static bool TryAccept(IDictionary<string, PingInstancePreference> preferences, string? key, bool visible, int color)
    {
        if (string.IsNullOrEmpty(key) || key.Length > MAX_KEY_LENGTH)
        {
            return false;
        }
        if (!preferences.ContainsKey(key) && preferences.Count >= MAX_ENTRIES)
        {
            return false;
        }
        preferences[key] = new PingInstancePreference(color, visible);
        return true;
    }

    /// <summary>
    ///     Removes the preferences of entities that no longer exist. Keys that are no Nitrox id (story signals) are kept.
    /// </summary>
    /// <returns>How many preferences were removed.</returns>
    public static int RemoveGone(IDictionary<string, PingInstancePreference> preferences, Func<NitroxId, bool> exists)
    {
        List<string> gone = preferences.Keys.Where(key => Guid.TryParse(key, out Guid guid) && !exists(new NitroxId(guid))).ToList();
        foreach (string key in gone)
        {
            preferences.Remove(key);
        }
        return gone.Count;
    }
}
