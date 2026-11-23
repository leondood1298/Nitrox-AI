using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;
using NitroxClient.GameLogic.Spawning.Metadata.Extractor.Abstract;
using UnityEngine;

namespace NitroxClient.GameLogic.Spawning.Metadata.Extractor;

public class SandSharkMetadataExtractor : EntityMetadataExtractor<SandShark, SandSharkMetadata>
{
    public override SandSharkMetadata Extract(SandShark sandShark)
    {
        // A Sand Shark raised in an Alien Containment keeps its growth in WaterParkCreatureMetadata, its one metadata slot
        if (sandShark.GetComponent<WaterParkCreature>() || !sandShark.TryGetComponent(out BurryInSand burryInSand))
        {
            return null;
        }
        // Only kinematic while it lies still in the sand: BurryInSand.burried is only cleared when the shark goes for something else
        bool buried = burryInSand.burried && sandShark.TryGetComponent(out Rigidbody rigidbody) && rigidbody.isKinematic;
        return new(buried, burryInSand.burryPosition.ToDto(), burryInSand.floorAlign.ToDto(), Resolve<CreatureInfections>().GetDecidedAmount(sandShark));
    }
}
