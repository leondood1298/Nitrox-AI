using Nitrox.Model.GameLogic.FMOD;
using NitroxClient.GameLogic;
using NitroxClient.GameLogic.FMOD;
using UnityEngine;

namespace NitroxClient.MonoBehaviours;

/// <summary>
///     Sounds of another player's body that its game plays only for its own player and does not send: the splash when it
///     goes through the surface, made here from that player's movement, and the fading of the engine of a Seaglide it
///     holds, which runs with its propeller.
/// </summary>
public class RemotePlayerSounds : MonoBehaviour
{
    private const string SEAGLIDE_RPM_SOUND_PATH = "event:/sub/seamoth/seaglide_loop_rpm";
    private const float FALLBACK_RADIUS = 10f;

    private RemotePlayer remotePlayer;
    private string splashSoundPath;
    private float splashRadius = FALLBACK_RADIUS;
    private float seaglideRadius = FALLBACK_RADIUS;
    private float previousY;
    private float lastSplashTime = float.MinValue;

    public void Init(RemotePlayer player, FMODWhitelist whitelist)
    {
        remotePlayer = player;
        previousY = transform.position.y;

        // The splash the game plays for its own player
        SwimWaterSplash localSplash = Player.main ? Player.main.GetComponentInChildren<SwimWaterSplash>(true) : null;
        if (localSplash && localSplash.splashUnderwaterSound && localSplash.splashUnderwaterSound.asset)
        {
            splashSoundPath = localSplash.splashUnderwaterSound.asset.path;
            if (whitelist.TryGetSoundData(splashSoundPath, out SoundData splashData) && splashData.Radius > 0)
            {
                splashRadius = splashData.Radius;
            }
        }
        if (whitelist.TryGetSoundData(SEAGLIDE_RPM_SOUND_PATH, out SoundData seaglideData) && seaglideData.Radius > 0)
        {
            seaglideRadius = seaglideData.Radius;
        }
    }

    public void Update()
    {
        if (remotePlayer == null)
        {
            return;
        }
        float y = transform.position.y;
        bool inside = remotePlayer.SubRoot || remotePlayer.Vehicle || remotePlayer.EscapePod;
        if (!string.IsNullOrEmpty(splashSoundPath) && RemotePlayerSoundRules.ShouldSplash(previousY, y, inside, Time.time - lastSplashTime))
        {
            lastSplashTime = Time.time;
            FMODEmitterController.PlayEventOneShot(splashSoundPath, splashRadius, transform.position);
        }
        previousY = y;
    }

    public void LateUpdate()
    {
        // The engine runs with the propeller (RemotePlayerToolLook, which replaces Seaglide.Update for this copy)
        Seaglide seaglide = remotePlayer?.HeldTool as Seaglide;
        if (!seaglide || !seaglide.engineRPMManager || !Player.main)
        {
            return;
        }
        // The engine events are non-positional: faded by distance, as for the other players' vehicles
        float volume = FMODSystem.CalculateVolume(transform.position, Player.main.transform.position, seaglideRadius, 1f);
        EngineRpmSFXManager engine = seaglide.engineRPMManager;
        if (engine.engineRpmSFX)
        {
            engine.engineRpmSFX.GetEventInstance().setVolume(volume);
        }
        if (engine.engineRevUp)
        {
            engine.engineRevUp.GetEventInstance().setVolume(volume);
        }
    }
}
