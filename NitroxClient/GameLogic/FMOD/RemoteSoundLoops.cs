using System.Collections.Generic;
using Nitrox.Model.DataStructures;

namespace NitroxClient.GameLogic.FMOD;

/// <summary>
///     The looping sounds other players started on entities, also those this game has not loaded. An entity that loads
///     later (the player arrives in the area) starts its loops from here, so they are heard as by everyone else.
/// </summary>
/// <remarks>Unity-free so that it can be tested.</remarks>
public sealed class RemoteSoundLoops
{
    private readonly Dictionary<NitroxId, Dictionary<string, float>> volumesByEntity = [];

    public void Started(NitroxId id, string assetPath, float volume)
    {
        if (!volumesByEntity.TryGetValue(id, out Dictionary<string, float> volumes))
        {
            volumesByEntity[id] = volumes = [];
        }
        volumes[assetPath] = volume;
    }

    public void Stopped(NitroxId id, string assetPath)
    {
        if (volumesByEntity.TryGetValue(id, out Dictionary<string, float> volumes) && volumes.Remove(assetPath) && volumes.Count == 0)
        {
            volumesByEntity.Remove(id);
        }
    }

    public bool TryGetVolume(NitroxId id, string assetPath, out float volume)
    {
        volume = 0f;
        return volumesByEntity.TryGetValue(id, out Dictionary<string, float> volumes) && volumes.TryGetValue(assetPath, out volume);
    }

    public void Clear() => volumesByEntity.Clear();
}
