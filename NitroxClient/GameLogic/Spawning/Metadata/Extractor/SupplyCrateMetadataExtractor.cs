using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;
using NitroxClient.GameLogic.Spawning.Metadata.Extractor.Abstract;

namespace NitroxClient.GameLogic.Spawning.Metadata.Extractor;

public class SupplyCrateMetadataExtractor : EntityMetadataExtractor<SupplyCrate, SupplyCrateMetadata>
{
    public override SupplyCrateMetadata Extract(SupplyCrate crate)
    {
        // A crate that has to be cut open has its Sealed on the same object (SupplyCrate.Start takes it from there)
        if (crate.TryGetComponent(out Sealed sealedCrate))
        {
            return new(crate.open, sealedCrate._sealed, sealedCrate.openedAmount);
        }

        return new(crate.open, false, 0);
    }
}
