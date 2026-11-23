using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;
using NitroxClient.GameLogic.Spawning.Metadata.Processor.Abstract;
using UnityEngine;

namespace NitroxClient.GameLogic.Spawning.Metadata.Processor;

public class CyclopsDamagePointMetadataProcessor : EntityMetadataProcessor<CyclopsDamagePointMetadata>
{
    public override void ProcessMetadata(GameObject gameObject, CyclopsDamagePointMetadata metadata)
    {
        CyclopsDamagePoint damagePoint = gameObject.RequireComponent<CyclopsDamagePoint>();
        Resolve<LiveMixinManager>().SyncRemoteHealth(damagePoint.liveMixin, metadata.Health);
    }
}
