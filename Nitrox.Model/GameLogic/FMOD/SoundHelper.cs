using Nitrox.Model.Helper;

namespace Nitrox.Model.GameLogic.FMOD;

public static class SoundHelper
{
    /// <summary>
    ///     Volume calculation based on <paramref name="distance"/>, <paramref name="radius"/> and max <paramref name="volume"/> (not 100% realistic)
    /// </summary>
    public static float CalculateVolume(float distance, float radius, float volume)
    {
        if (distance >= 0f && distance < radius)
        {
            return Mathf.Clamp01((1f - distance / radius) * volume);
        }
        return 0f;
    }

    /// <summary>
    ///     Whether a sound a game plays because it repeats another player's action must be faded by its distance to the
    ///     action. A positional event fades by itself; a non-positional one would play at full volume anywhere in the world,
    ///     so it is faded over its whitelist radius. One without a radius (a voice notification, an interface sound) is the
    ///     player's own and is left as it is.
    /// </summary>
    public static bool FadesByReplayDistance(bool eventExists, bool is3D, float whitelistRadius) => eventExists && !is3D && whitelistRadius > 0f;
}
