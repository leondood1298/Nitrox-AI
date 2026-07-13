using System.Collections.Generic;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;
using Nitrox.Model.Subnautica.Packets;

namespace NitroxClient.GameLogic;

public sealed class BasePowerState
{
    private readonly Dictionary<NitroxId, PowerSourceMetadata> canonicalBySource = [];
    private readonly Dictionary<NitroxId, long> nextSequenceBySource = [];

    public BasePowerSourceUpdate CreateUpdate(NitroxId sourceId, BasePowerSourceType sourceType, float power)
    {
        long sequence = nextSequenceBySource.TryGetValue(sourceId, out long current) && current < long.MaxValue ? current + 1 : 1;
        nextSequenceBySource[sourceId] = sequence;
        return new BasePowerSourceUpdate(sourceId, sourceType, power, sequence);
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

    public bool TryGet(NitroxId sourceId, out PowerSourceMetadata metadata) => canonicalBySource.TryGetValue(sourceId, out metadata);
}
