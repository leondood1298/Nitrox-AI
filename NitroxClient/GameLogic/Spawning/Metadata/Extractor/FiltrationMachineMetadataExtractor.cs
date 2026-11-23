using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;
using NitroxClient.GameLogic.Spawning.Metadata.Extractor.Abstract;

namespace NitroxClient.GameLogic.Spawning.Metadata.Extractor;

public class FiltrationMachineMetadataExtractor : EntityMetadataExtractor<FiltrationMachine, FiltrationMachineMetadata>
{
    public override FiltrationMachineMetadata Extract(FiltrationMachine entity)
    {
        return FiltrationMachines.GetState(entity);
    }
}
