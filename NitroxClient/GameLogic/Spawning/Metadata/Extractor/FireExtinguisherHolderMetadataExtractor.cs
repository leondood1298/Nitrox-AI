using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;
using NitroxClient.GameLogic.Spawning.Metadata.Extractor.Abstract;
using NitroxClient.MonoBehaviours;

namespace NitroxClient.GameLogic.Spawning.Metadata.Extractor;

public class FireExtinguisherHolderMetadataExtractor : EntityMetadataExtractor<FireExtinguisherHolder, FireExtinguisherHolderMetadata>
{
    public override FireExtinguisherHolderMetadata Extract(FireExtinguisherHolder entity)
    {
        NitroxId? storedTankId = entity.TryGetNitroxId(out NitroxId holderId) ? Resolve<FireExtinguisherHolders>().GetStoredTank(holderId) : null;
        return new(entity.hasTank, entity.fuel, storedTankId);
    }
}
