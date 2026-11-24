using System.Collections.Generic;
using FMOD.Studio;
using NitroxClient.Communication;
using NitroxClient.Communication.Abstract;
using NitroxClient.GameLogic.PlayerLogic;
using NitroxClient.MonoBehaviours;
using Nitrox.Model.DataStructures;
using Nitrox.Model.DataStructures.Unity;
using Nitrox.Model.GameLogic.FMOD;
using Nitrox.Model.Subnautica.Packets;
using UnityEngine;

namespace NitroxClient.GameLogic.FMOD;

public class FMODSystem
{
    private static readonly Dictionary<string, EventKind> eventKinds = [];
    private readonly IPacketSender packetSender;

    public FMODSystem(IPacketSender packetSender)
    {
        this.packetSender = packetSender;
    }

    /// <inheritdoc cref="FMODSoundSuppressor"/>
    public static FMODSoundSuppressor SuppressSubnauticaSounds() => FMODSoundSuppressor.Suppress();

    /// <summary>
    /// Suppresses sending any sound packet
    /// </summary>
    public static PacketSuppressor<FMODAssetPacket, FMODEventInstancePacket, FMODCustomEmitterPacket, FootstepPacket, FMODStudioEmitterPacket> SuppressSendingSounds()
    {
        return PacketSuppressor<FMODAssetPacket, FMODEventInstancePacket, FMODCustomEmitterPacket, FootstepPacket, FMODStudioEmitterPacket>.Suppress();
    }

    /// <inheritdoc cref="SoundHelper.CalculateVolume"/>
    public static float CalculateVolume(Vector3 p1, Vector3 p2, float radius, float volume)
    {
        float distance = Vector3.Distance(p1, p2);
        if (distance > radius)
        {
            return 0;
        }

        return SoundHelper.CalculateVolume(distance, radius, volume);
    }

    /// <summary>
    ///     Whether the event is positional and whether it ends by itself, read once from the loaded banks.
    /// </summary>
    public static EventKind GetEventKind(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return default;
        }
        if (eventKinds.TryGetValue(path, out EventKind kind))
        {
            return kind;
        }
        kind = default;
        if (FMODUWE.GetEventDescription(path, out EventDescription description) == global::FMOD.RESULT.OK)
        {
            description.is3D(out bool is3D);
            description.isOneshot(out bool isOneshot);
            description.getMaximumDistance(out float maxDistance);
            kind = new EventKind(true, is3D, isOneshot, maxDistance);
        }
        eventKinds[path] = kind;
        return kind;
    }

    /// <summary>
    ///     The volume of a sound this game plays because it repeats what another player did: a positional event already
    ///     fades with distance, a non-positional one is faded over its whitelist radius, so that it is not heard at full
    ///     volume across the world.
    /// </summary>
    public static bool TryGetReplayedVolume(FMODWhitelist whitelist, string path, Vector3 soundPosition, float volume, out float replayedVolume)
    {
        replayedVolume = volume;
        if (!Player.main || !whitelist.TryGetSoundData(path, out SoundData soundData))
        {
            return false;
        }
        FMODSystem.EventKind kind = GetEventKind(path);
        if (!SoundHelper.FadesByReplayDistance(kind.Exists, kind.Is3D, soundData.Radius))
        {
            return false;
        }
        replayedVolume = CalculateVolume(soundPosition, Player.main.transform.position, soundData.Radius, volume);
        return true;
    }

    /// <summary>
    ///     A sound of another player's body or of what it holds, played by this game's copy: that player's game sends its
    ///     own, so this one must not be sent in its name.
    /// </summary>
    public static bool IsOnRemotePlayer(Component source) => source && source.GetComponentInParent<RemotePlayerIdentifier>();

    /// <summary>
    ///     Sends that a studio emitter on an entity started (<see cref="FMOD_StudioEventEmitter.PlayUI" /> or
    ///     <see cref="FMOD_StudioEventEmitter.StartEvent" />).
    /// </summary>
    public void SendStudioEmitterStarted(FMOD_StudioEventEmitter emitter, FMODWhitelist whitelist)
    {
        if (!emitter.asset || !whitelist.IsWhitelisted(emitter.asset.path) || IsOnRemotePlayer(emitter))
        {
            return;
        }
        if (!emitter.TryGetComponentInParent(out NitroxEntity nitroxEntity, true))
        {
            Log.WarnOnce($"[{nameof(FMODSystem)}] No NitroxEntity found for {emitter.asset.path} at {emitter.GetFullHierarchyPath()}");
            return;
        }
        SendStudioEmitterPlay(nitroxEntity.Id, emitter.asset.path, !GetEventKind(emitter.asset.path).IsOneshot);
    }

    public void SendAssetPlay(string path, NitroxVector3 position, float volume) => packetSender.Send(new FMODAssetPacket(path, position, volume));

    public void SendCustomEmitterPlay(NitroxId id, string assetPath, bool looping) => packetSender.Send(new FMODCustomEmitterPacket(id, assetPath, true, looping));
    public void SendCustomEmitterStop(NitroxId id, string assetPath) => packetSender.Send(new FMODCustomEmitterPacket(id, assetPath, false, false));

    public void SendStudioEmitterPlay(NitroxId id, string assetPath, bool looping) => packetSender.Send(new FMODStudioEmitterPacket(id, assetPath, true, false, looping));
    public void SendStudioEmitterStop(NitroxId id, string assetPath, bool allowFadeout) => packetSender.Send(new FMODStudioEmitterPacket(id, assetPath, false, allowFadeout, false));

    public void SendEventInstancePlay(NitroxId id, string assetPath, NitroxVector3 position, float volume, bool looping) => packetSender.Send(new FMODEventInstancePacket(id, true, assetPath, position, volume, looping));
    public void SendEventInstanceStop(NitroxId id, string assetPath, NitroxVector3 position) => packetSender.Send(new FMODEventInstancePacket(id, false, assetPath, position, 0f, false));

    public readonly struct EventKind
    {
        public bool Exists { get; }
        public bool Is3D { get; }
        public bool IsOneshot { get; }
        public float MaxDistance { get; }

        public EventKind(bool exists, bool is3D, bool isOneshot, float maxDistance)
        {
            Exists = exists;
            Is3D = is3D;
            IsOneshot = isOneshot;
            MaxDistance = maxDistance;
        }
    }
}
