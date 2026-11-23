using System.Collections.Generic;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;
using Nitrox.Model.Subnautica.Packets;

namespace NitroxClient.GameLogic;

public sealed class BasePowerState
{
    private readonly Dictionary<NitroxId, PowerSourceMetadata> canonicalBySource = [];
    private readonly Dictionary<NitroxId, long> nextSequenceBySource = [];

    public BasePowerSourceUpdate CreateUpdate(NitroxId sourceId, BasePowerSourceType sourceType, float power, float fuelConsumed = 0f)
    {
        long sequence = nextSequenceBySource.TryGetValue(sourceId, out long current) && current < long.MaxValue ? current + 1 : 1;
        nextSequenceBySource[sourceId] = sequence;
        return new BasePowerSourceUpdate(sourceId, sourceType, power, sequence, fuelConsumed: fuelConsumed);
    }

    public bool TryApply(NitroxId sourceId, PowerSourceMetadata requested, out PowerSourceMetadata accepted)
    {
        if (canonicalBySource.TryGetValue(sourceId, out PowerSourceMetadata current) && requested.Revision < current.Revision)
        {
            accepted = current;
            return false;
        }
        canonicalBySource[sourceId] = requested;
        accepted = requested;
        return true;
    }

    /// <summary>
    ///     Returns the state a source should show after receiving <paramref name="incoming" />: the incoming state, or the
    ///     newer one already known when a live update overtook it (e.g. a spawn snapshot arriving after a broadcast).
    /// </summary>
    public PowerSourceMetadata Reconcile(NitroxId sourceId, PowerSourceMetadata incoming)
    {
        TryApply(sourceId, incoming, out PowerSourceMetadata newest);
        return newest;
    }

    public bool TryGet(NitroxId sourceId, out PowerSourceMetadata metadata) => canonicalBySource.TryGetValue(sourceId, out metadata);
}
