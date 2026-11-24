using Nitrox.Model.GameLogic.FMOD;
using NitroxClient.GameLogic;
using NitroxClient.GameLogic.FMOD;
using UnityEngine;

namespace NitroxClient.MonoBehaviours;

/// <summary>
///     Sounds of another player's body that its game plays only for its own player and does not send: the splash when it
///     goes through the surface, and the engine of a Seaglide it holds. They are made here from that player's movement,
///     as the vehicles' engines are.
/// </summary>
public class RemotePlayerSounds : MonoBehaviour
{
    private const string SEAGLIDE_RPM_SOUND_PATH = "event:/sub/seamoth/seaglide_loop_rpm";
    private const float FALLBACK_RADIUS = 10f;
    private const float SEAGLIDE_SEARCH_INTERVAL = 0.5f;

    private RemotePlayer remotePlayer;
    private string splashSoundPath;
    private float splashRadius = FALLBACK_RADIUS;
    private float seaglideRadius = FALLBACK_RADIUS;
    private float previousY;
    private float lastSplashTime = float.MinValue;
    private Seaglide seaglide;
    private float nextSeaglideSearchTime;

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

        UpdateSeaglide(y < RemotePlayerSoundRules.SURFACE_LEVEL && !inside);
    }

    private void UpdateSeaglide(bool underwater)
    {
        if (!seaglide || !seaglide.gameObject.activeInHierarchy)
        {
            seaglide = null;
            // Looking through the body every frame is wasted while the player holds something else
            if (Time.time < nextSeaglideSearchTime)
            {
                return;
            }
            nextSeaglideSearchTime = Time.time + SEAGLIDE_SEARCH_INTERVAL;
            seaglide = GetComponentInChildren<Seaglide>();
            if (!seaglide)
            {
                return;
            }
        }
        float speed = remotePlayer.AnimationController ? remotePlayer.AnimationController.Velocity.magnitude : 0f;
        if (RemotePlayerSoundRules.SeaglideRuns(speed, underwater) && seaglide.engineRPMManager)
        {
            // What Seaglide.Update does for the player using it
            seaglide.engineRPMManager.AccelerateInput(1f);
        }
    }

    public void LateUpdate()
    {
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
