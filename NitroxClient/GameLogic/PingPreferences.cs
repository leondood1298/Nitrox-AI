using System.Collections.Generic;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;

namespace NitroxClient.GameLogic;

/// <summary>
///     This player's colour and visibility of pings, as the server keeps them. Filled from the join snapshot and updated
///     with every change this game sends: a ping that is enabled again (a beacon thrown, an item moved by another player)
///     gets them applied anew, and with only the snapshot it went back to how it was when the player joined.
/// </summary>
/// <remarks>
///     One per session (lifetime scope), so nothing carries into the next world.
/// </remarks>
public class PingPreferences
{
    private readonly Dictionary<string, PingInstancePreference> preferences = [];

    public void Load(IDictionary<string, PingInstancePreference> fromServer)
    {
        preferences.Clear();
        foreach (KeyValuePair<string, PingInstancePreference> preference in fromServer)
        {
            preferences[preference.Key] = new PingInstancePreference(preference.Value.Color, preference.Value.Visible);
        }
    }

    public bool TryGet(string key, out PingInstancePreference preference) => preferences.TryGetValue(key, out preference);

    /// <summary>
    ///     Called with every preference this game sends to the server.
    /// </summary>
    public void Remember(string key, bool visible, int color)
    {
        preferences[key] = new PingInstancePreference(color, visible);
    }
}
