using System.Collections.Generic;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;
using Nitrox.Model.Subnautica.Packets;
using NitroxClient.Communication;
using NitroxClient.GameLogic.FMOD;
using NitroxClient.GameLogic.Spawning.Metadata.Processor;

namespace NitroxClient.GameLogic;

/// <summary>
///     The repair state of the Lifepod as the server last sent it. The end of the intro damages the radio and sets the
///     pod's damaged effects and lighting locally, whatever the server says; the stored state is put back afterwards.
/// </summary>
public class EscapePodRepairState
{
    private readonly Dictionary<NitroxId, EscapePodMetadata> latestByPod = [];

    public void Remember(NitroxId podId, EscapePodMetadata metadata)
    {
        latestByPod[podId] = metadata;
    }

    public bool TryGetLatest(NitroxId podId, out EscapePodMetadata metadata)
    {
        return latestByPod.TryGetValue(podId, out metadata);
    }

    /// <summary>
    ///     Puts the pod and its radio back into the state the server last sent, after the game's intro code changed them.
    /// </summary>
    public void ApplyAfterIntro()
    {
        EscapePod pod = EscapePod.main;
        if (!pod || !pod.TryGetNitroxId(out NitroxId podId) || !TryGetLatest(podId, out EscapePodMetadata metadata))
        {
            Log.Warn($"[{nameof(EscapePodRepairState)}] No repair state of the Lifepod is known to apply after the intro");
            return;
        }
        if (!pod.radioSpawner.spawnedObj || !pod.radioSpawner.spawnedObj.TryGetComponent(out Radio radio))
        {
            Log.Error($"[{nameof(EscapePodRepairState)}] Could not get the Radio of the Lifepod");
            return;
        }

        // The state comes from the server; sent back, it would be a repair this player never made.
        using PacketSuppressor<EntityMetadataUpdate> packetSuppressor = PacketSuppressor<EntityMetadataUpdate>.Suppress();
        using FMODSoundSuppressor soundSuppressor = FMODSystem.SuppressSubnauticaSounds();
        EscapePodMetadataProcessor.ProcessInitialSyncMetadata(pod, radio, metadata);
    }
}
