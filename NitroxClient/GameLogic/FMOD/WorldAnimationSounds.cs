using UnityEngine;

namespace NitroxClient.GameLogic.FMOD;

/// <summary>
///     Sounds played from animation events (<see cref="AnimationSound" />, <see cref="GenericPlaySound" />). The animations
///     of world objects run in every game because their state is synced, so each game plays the sound itself; only the
///     local player's own animations (first-person tools, held items) are sent as usual.
/// </summary>
public static class WorldAnimationSounds
{
    /// <returns>The scope to end once the sound is played, or null for the local player's own animation.</returns>
    public static ReplayedSoundScope? Begin(Component source)
    {
        if (!source || (Player.main && source.transform.IsChildOf(Player.main.transform)))
        {
            return null;
        }
        return ReplayedSoundScope.Enter();
    }
}
