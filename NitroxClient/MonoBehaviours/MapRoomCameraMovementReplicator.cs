using FMOD.Studio;
using Nitrox.Model.GameLogic.FMOD;
using Nitrox.Model.Subnautica.Packets;
using UnityEngine;

namespace NitroxClient.MonoBehaviours;

/// <summary>
///     Moves a Scanner Room camera that another player flies, and lets it be heard. The game plays a camera's idle
///     loop and its engine in the game of the player who controls it. Here nobody controls it, so its own sound
///     emitters are played from what the camera does, without packets, like the engine of a vehicle another player
///     pilots (see <see cref="Vehicles.SeamothMovementReplicator" />).
/// </summary>
public class MapRoomCameraMovementReplicator : MovementReplicator
{
    /// <summary>
    ///     The speed in metres per second above which the game runs the engine sound of a camera a player flies, see
    ///     <see cref="MapRoomCamera.HandleInput" />.
    /// </summary>
    internal const float ENGINE_SOUND_SPEED = 2f;

    private MapRoomCamera mapRoomCamera;
    private float idleSoundRadius;
    private float engineSoundRadius;

    public new void Awake()
    {
        base.Awake();
        mapRoomCamera = GetComponent<MapRoomCamera>();

        FMODWhitelist whitelist = this.Resolve<FMODWhitelist>();
        whitelist.TryGetSoundData(mapRoomCamera.droneIdle.asset.path, out SoundData idleSound);
        idleSoundRadius = idleSound.Radius;
        whitelist.TryGetSoundData(mapRoomCamera.engineSound.asset.path, out SoundData engineSound);
        engineSoundRadius = engineSound.Radius;
        // The pilot is at the camera. Everybody else has to hear it from where it is now.
        mapRoomCamera.droneIdle.followParent = true;
        mapRoomCamera.engineSound.followParent = true;

        // The game starts this loop when a player takes control of the camera, and stops it when they let go.
        mapRoomCamera.droneIdle.Play();
        UpdateSoundVolumes();
    }

    public new void Update()
    {
        Vector3 positionBefore = transform.position;
        base.Update();

        if (EngineRuns(positionBefore, transform.position, Time.deltaTime))
        {
            mapRoomCamera.engineSound.Play();
        }
        else
        {
            mapRoomCamera.engineSound.Stop();
        }
        UpdateSoundVolumes();
    }

    public new void OnDestroy()
    {
        base.OnDestroy();
        if (!mapRoomCamera)
        {
            return;
        }
        // This player can have taken control of the camera in the frame in which the other player let go. Then the
        // game has started the sounds for them, and they stay.
        if (!mapRoomCamera.active)
        {
            // At once: a sound that fades out would do so at the full volume set below.
            mapRoomCamera.droneIdle.Stop(STOP_MODE.IMMEDIATE);
            mapRoomCamera.engineSound.Stop(STOP_MODE.IMMEDIATE);
        }
        // Whoever flies the camera from this game next hears it as the game plays it.
        SetVolume(mapRoomCamera.droneIdle, 1f);
        SetVolume(mapRoomCamera.engineSound, 1f);
    }

    public override void ApplyNewMovementData(MovementData newMovementData)
    {
    }

    /// <summary>
    ///     Whether the engine of a camera that got from one place to the other in this time is heard. The game asks the
    ///     camera's rigidbody for its speed, which says nothing here, where the camera is put where its pilot reports it.
    /// </summary>
    internal static bool EngineRuns(Vector3 positionBefore, Vector3 positionAfter, float seconds) =>
        seconds > 0f && Vector3.Distance(positionBefore, positionAfter) / seconds > ENGINE_SOUND_SPEED;

    private void UpdateSoundVolumes()
    {
        float distanceToPlayer = Vector3.Distance(Player.main.transform.position, transform.position);
        SetVolume(mapRoomCamera.droneIdle, SoundHelper.CalculateVolume(distanceToPlayer, idleSoundRadius, 1f));
        SetVolume(mapRoomCamera.engineSound, SoundHelper.CalculateVolume(distanceToPlayer, engineSoundRadius, 1f));
    }

    private static void SetVolume(FMOD_CustomEmitter emitter, float volume)
    {
        EventInstance sound = emitter.GetEventInstance();
        if (sound.hasHandle())
        {
            sound.setVolume(volume);
        }
    }
}
