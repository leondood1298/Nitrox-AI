using System;
using Nitrox.Model.GameLogic.FMOD;
using UnityEngine;

namespace NitroxClient.GameLogic.FMOD;

/// <summary>
///     Sounds played from animation events (<see cref="AnimationSound" />, <see cref="GenericPlaySound" />).
///     <list type="bullet">
///         <item>The local player's own animations (first-person tools, held items): sent as usual.</item>
///         <item>
///             The body of another player and the tool in its hand, animated here from that player's state: a sound the
///             whitelist sends is that player's game's to send, so this copy stays silent and it is heard once; any other
///             it plays here, as nobody sends it.
///         </item>
///         <item>World objects (a moonpool docking a vehicle, a door), animated in every game because their state is synced: each game plays the sound itself.</item>
///     </list>
/// </summary>
public static class WorldAnimationSounds
{
    /// <returns>The scope to end once the sound is played, or null for the local player's own animation.</returns>
    public static IDisposable Begin(Component source, FMODAsset asset, FMODWhitelist whitelist)
    {
        if (!source)
        {
            return null;
        }
        bool localPlayers = Player.main && source.transform.IsChildOf(Player.main.transform);
        return Decide(localPlayers, FMODSystem.IsOnRemotePlayer(source), asset && whitelist.IsWhitelisted(asset.path)) switch
        {
            Handling.SILENT => new DelayedSilentReplay.Silence(),
            Handling.REPLAYED => ReplayedSoundScope.Enter(),
            _ => null
        };
    }

    public enum Handling
    {
        /// <summary>Played and sent as the game does.</summary>
        AS_USUAL,

        /// <summary>Played here, not sent, a non-positional one faded by distance.</summary>
        REPLAYED,

        /// <summary>Not played here: the other player's game sends it.</summary>
        SILENT
    }

    public static Handling Decide(bool onLocalPlayer, bool onRemotePlayer, bool whitelisted)
    {
        if (onLocalPlayer)
        {
            return Handling.AS_USUAL;
        }
        return onRemotePlayer && whitelisted ? Handling.SILENT : Handling.REPLAYED;
    }
}
