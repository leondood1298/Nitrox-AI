using Nitrox.Model.GameLogic.FMOD;
using NitroxClient.MonoBehaviours;

namespace NitroxClient.GameLogic.FMOD;

/// <summary>
///     Lets the others hear the local player's pain sounds, which <see cref="PlayerDamageSounds.OnTakeDamage" /> plays
///     through emitters on the local player only (<c>FMOD_StudioEventEmitter.StartEvent</c>, which Nitrox does not relay,
///     and an FMODUnity emitter). They are sent as one-shots at the player's position, which every game can play
///     without having those emitters.
/// </summary>
/// <remarks>
///     The pain events are listed in the sound whitelist with a radius but not as synced, so that the generic emitter
///     patches leave them alone and they are not sent twice.
/// </remarks>
public sealed class PlayerPainSounds(FMODSystem fmodSystem, FMODWhitelist fmodWhitelist)
{
    private readonly FMODWhitelist fmodWhitelist = fmodWhitelist;
    /// <summary>
    ///     The emitter <see cref="PlayerDamageSounds.OnTakeDamage" /> picks, in the same order as the game.
    /// </summary>
    public enum Pain
    {
        NONE,
        SMOKE,
        SURFACE,
        WITH_TANK,
        WITHOUT_TANK,
        WITHOUT_TANK_LIGHT
    }

    private const int RADIATION_DAMAGE = 12;
    private const int SMOKE_DAMAGE = 16;
    private const float HEAVY_DAMAGE = 8f;

    private readonly FMODSystem fmodSystem = fmodSystem;

    /// <summary>
    ///     Mirrors the choice in <see cref="PlayerDamageSounds.OnTakeDamage" /> (read from the game's IL). Unity-free, so
    ///     that it can be tested.
    /// </summary>
    public static Pain Choose(float damage, int damageType, bool hasPlayer, bool playerUnderwater, bool carriesTank)
    {
        if (damage <= 0f || damageType == RADIATION_DAMAGE)
        {
            return Pain.NONE;
        }
        if (damageType == SMOKE_DAMAGE)
        {
            return Pain.SMOKE;
        }
        if (hasPlayer && !playerUnderwater)
        {
            return Pain.SURFACE;
        }
        if (carriesTank)
        {
            return Pain.WITH_TANK;
        }
        return damage > HEAVY_DAMAGE ? Pain.WITHOUT_TANK : Pain.WITHOUT_TANK_LIGHT;
    }

    /// <summary>
    ///     Called right before the game plays the pain sound; sends the one it is about to start.
    /// </summary>
    public void BeforePain(PlayerDamageSounds sounds, DamageInfo damageInfo)
    {
        Player player = Player.main;
        bool hasPlayer = player;
        Pain pain = Choose(damageInfo.damage, (int)damageInfo.type, hasPlayer, hasPlayer && player.isUnderwater.value,
                           Inventory.Get() && Inventory.Get().GetPickupCount(TechType.Tank) > 0);

        string eventPath = pain switch
        {
            // A custom emitter restarts on every hit
            Pain.SMOKE when sounds.painSmoke && sounds.painSmoke.asset => sounds.painSmoke.asset.path,
            // The others only start when not already playing, as in the game
            Pain.SURFACE => PathIfIdle(sounds.painWithoutMask),
            Pain.WITH_TANK => PathIfIdle(sounds.painWithTank),
            Pain.WITHOUT_TANK => PathIfIdle(sounds.painWithoutTank),
            Pain.WITHOUT_TANK_LIGHT when sounds.painWithoutTankLight && !sounds.painWithoutTankLight.IsPlaying() => sounds.painWithoutTankLight.Event,
            _ => null
        };
        if (string.IsNullOrEmpty(eventPath) || !hasPlayer)
        {
            return;
        }
        // The server only passes on sounds the whitelist gives a radius
        if (!fmodWhitelist.TryGetSoundData(eventPath, out _))
        {
            Log.WarnOnce($"[{nameof(PlayerPainSounds)}] {eventPath} has no entry in the sound whitelist");
            return;
        }
        fmodSystem.SendAssetPlay(eventPath, player.transform.position.ToDto(), 1f);
    }

    private static string PathIfIdle(FMOD_StudioEventEmitter emitter)
    {
        if (!emitter || !emitter.asset || emitter.GetIsPlaying())
        {
            return null;
        }
        return emitter.asset.path;
    }
}
