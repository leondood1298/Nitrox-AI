using System.Collections.Generic;
using FMOD.Studio;
using FMODUnity;
using STOP_MODE = FMOD.Studio.STOP_MODE;
using Nitrox.Model.DataStructures;
using Nitrox.Model.GameLogic.FMOD;
using NitroxClient.GameLogic.FMOD;
using UnityEngine;

namespace NitroxClient.MonoBehaviours;

/// <summary>
///     Plays on this game's copy of an entity the sounds another player's game sends for it. A positional (3D) event is
///     played by the entity's own emitter, so that it follows the entity and fades as the event is made to. A
///     non-positional (2D) event is played as an instance of its own and faded every frame by the distance to the entity.
///     Whatever is started here is stopped when the entity is hidden or destroyed.
/// </summary>
[DisallowMultipleComponent]
public class FMODEmitterController : MonoBehaviour
{
    private readonly Dictionary<string, Registered> emitters = new();
    private readonly Dictionary<string, Flat> flatSounds = new();
    private readonly HashSet<string> startedStudioEmitters = new();
    private readonly List<string> finished = new();

    /// <summary>
    /// When <see cref="GameObject"/>s are copied their Start()/Awake() functions don't get called again.
    /// So the FMOD Start patch that tried to locate a <see cref="NitroxEntity"/> won't find one and will error later.
    /// This function can late register missed FMOD MonoBehaviours
    /// </summary>
    public void LateRegisterEmitter()
    {
        FMODWhitelist whitelist = this.Resolve<FMODWhitelist>();
        foreach (MonoBehaviour behaviour in gameObject.GetComponentsInChildren<MonoBehaviour>(true))
        {
            switch (behaviour)
            {
                case FMOD_CustomEmitter customEmitter when customEmitter.asset && whitelist.IsWhitelisted(customEmitter.asset.path, out float radius):
                    AddEmitter(customEmitter.asset.path, customEmitter, radius);
                    break;
                case FMOD_StudioEventEmitter studioEmitter when studioEmitter.asset && whitelist.IsWhitelisted(studioEmitter.asset.path, out float radius):
                    AddEmitter(studioEmitter.asset.path, studioEmitter, radius);
                    break;
            }
        }
    }

    public void AddEmitter(string path, FMOD_CustomEmitter customEmitter, float radius) => Register(path, new Registered(customEmitter, null, radius));

    public void AddEmitter(string path, FMOD_StudioEventEmitter studioEmitter, float radius) => Register(path, new Registered(null, studioEmitter, radius));

    private void Register(string path, Registered registered)
    {
        if (emitters.ContainsKey(path))
        {
            return;
        }
        emitters.Add(path, registered);

        // The entity loaded while another player's loop on it plays: it is heard from now on, as everyone else hears it
        if (gameObject.TryGetNitroxId(out NitroxId id) && this.Resolve<RemoteSoundLoops>().TryGetVolume(id, path, out float volume))
        {
            Play(path, volume);
        }
    }

    public void Play(string path, float volume)
    {
        if (!emitters.TryGetValue(path, out Registered registered) || !registered.Alive)
        {
            Log.WarnOnce($"[{nameof(FMODEmitterController)}] {gameObject.name} has no emitter for {path}");
            return;
        }

        // Sent by another player: this game must not send it back
        using (FMODSystem.SuppressSendingSounds())
        {
            FMODSystem.EventKind kind = FMODSystem.GetEventKind(path);
            if (!kind.Is3D)
            {
                PlayFlat(path, registered, volume);
                return;
            }

            EventInstance evt;
            if (registered.Custom)
            {
                registered.Custom.Play();
                evt = registered.Custom.GetEventInstance();
            }
            else
            {
                registered.Studio.PlayUI();
                startedStudioEmitters.Add(path);
                evt = registered.Studio.evt;
            }
            // Only a radius the whitelist widens on purpose (the Cyclops horn) changes the event's own falloff
            if (registered.Radius > kind.MaxDistance && evt.isValid())
            {
                evt.setProperty(EVENT_PROPERTY.MAXIMUM_DISTANCE, registered.Radius);
            }
        }
    }

    public void Stop(string path, bool allowFadeout)
    {
        if (!emitters.TryGetValue(path, out Registered registered))
        {
            return;
        }

        using (FMODSystem.SuppressSendingSounds())
        {
            if (flatSounds.TryGetValue(path, out Flat flat))
            {
                flatSounds.Remove(path);
                flat.Instance.stop(allowFadeout ? STOP_MODE.ALLOWFADEOUT : STOP_MODE.IMMEDIATE);
                flat.Instance.release();
                // The game's looping emitter plays its closing sound when it stops; this instance is not that emitter
                if (registered.Custom is FMOD_CustomLoopingEmitter looping && looping && looping.assetStop)
                {
                    PlayEventOneShot(looping.assetStop.path, registered.Radius, transform.position, flat.Volume);
                }
            }
            else if (registered.Custom)
            {
                registered.Custom.Stop();
            }
            else if (registered.Studio && startedStudioEmitters.Remove(path))
            {
                registered.Studio.Stop(allowFadeout);
            }
        }
    }

    private void PlayFlat(string path, Registered registered, float volume)
    {
        if (flatSounds.TryGetValue(path, out Flat previous))
        {
            previous.Instance.stop(STOP_MODE.IMMEDIATE);
            previous.Instance.release();
        }
        EventInstance evt = FMODUWE.GetEventImpl(path);
        if (!evt.isValid())
        {
            flatSounds.Remove(path);
            return;
        }
        Flat flat = new(evt, volume, registered.Radius);
        evt.setVolume(GetFlatVolume(flat));
        evt.start();
        flatSounds[path] = flat;

        if (registered.Custom is FMOD_CustomLoopingEmitter looping && looping && looping.assetStart)
        {
            PlayEventOneShot(looping.assetStart.path, registered.Radius, transform.position, volume);
        }
    }

    private float GetFlatVolume(Flat flat)
    {
        return Player.main ? FMODSystem.CalculateVolume(transform.position, Player.main.transform.position, flat.Radius, flat.Volume) : 0f;
    }

    public void Update()
    {
        if (flatSounds.Count == 0)
        {
            return;
        }
        finished.Clear();
        foreach (KeyValuePair<string, Flat> pair in flatSounds)
        {
            pair.Value.Instance.getPlaybackState(out PLAYBACK_STATE state);
            if (state == PLAYBACK_STATE.STOPPED)
            {
                finished.Add(pair.Key);
                continue;
            }
            // A non-positional sound only fades by volume, so it follows the distance while it plays
            pair.Value.Instance.setVolume(GetFlatVolume(pair.Value));
        }
        foreach (string path in finished)
        {
            flatSounds[path].Instance.release();
            flatSounds.Remove(path);
        }
    }

    public void OnDisable() => StopEverythingStarted();

    public void OnDestroy() => StopEverythingStarted();

    /// <summary>
    ///     The game stops a playing emitter of its own when its object is hidden, but not the instances started here nor a
    ///     studio emitter, so a loop of an entity that is put away, unloaded or destroyed would play on.
    /// </summary>
    private void StopEverythingStarted()
    {
        foreach (Flat flat in flatSounds.Values)
        {
            flat.Instance.stop(STOP_MODE.IMMEDIATE);
            flat.Instance.release();
        }
        flatSounds.Clear();

        using (FMODSystem.SuppressSendingSounds())
        {
            foreach (string path in startedStudioEmitters)
            {
                if (emitters.TryGetValue(path, out Registered registered) && registered.Studio)
                {
                    registered.Studio.Stop(false);
                }
            }
        }
        startedStudioEmitters.Clear();
    }

    /// <summary>
    ///     Plays a sound sent without an entity. Its volume is reduced here only: a positional event by its own falloff, a
    ///     non-positional one by its distance over the whitelist radius.
    /// </summary>
    public static void PlayEventOneShot(string path, float radius, Vector3 origin, float volume = 1f)
    {
        FMODSystem.EventKind kind = FMODSystem.GetEventKind(path);
        EventInstance evt = FMODUWE.GetEventImpl(path);
        if (!evt.isValid())
        {
            return;
        }

        if (kind.Is3D)
        {
            evt.setVolume(volume);
            if (radius > kind.MaxDistance)
            {
                evt.setProperty(EVENT_PROPERTY.MAXIMUM_DISTANCE, radius);
            }
        }
        else
        {
            evt.setVolume(Player.main ? FMODSystem.CalculateVolume(origin, Player.main.transform.position, radius, volume) : 0f);
        }

        evt.set3DAttributes(origin.To3DAttributes());
        evt.start();
        evt.release();
    }

    private sealed class Registered
    {
        public FMOD_CustomEmitter Custom { get; }
        public FMOD_StudioEventEmitter Studio { get; }
        public float Radius { get; }
        public bool Alive => Custom || Studio;

        public Registered(FMOD_CustomEmitter custom, FMOD_StudioEventEmitter studio, float radius)
        {
            Custom = custom;
            Studio = studio;
            Radius = radius;
        }
    }

    private sealed class Flat
    {
        public EventInstance Instance { get; }
        public float Volume { get; }
        public float Radius { get; }

        public Flat(EventInstance instance, float volume, float radius)
        {
            Instance = instance;
            Volume = volume;
            Radius = radius;
        }
    }
}
