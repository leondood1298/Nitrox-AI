using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;
using NitroxClient.GameLogic.Spawning.Metadata.Extractor.Abstract;

namespace NitroxClient.GameLogic.Spawning.Metadata.Extractor;

/// <summary>
///     Only the babies that appeared outside after leaving the prison have lasting state.
/// </summary>
public class SeaEmperorBabyMetadataExtractor : EntityMetadataExtractor<SeaEmperorBaby, SeaEmperorBabyMetadata>
{
    public override SeaEmperorBabyMetadata Extract(SeaEmperorBaby baby)
    {
        return Resolve<SeaEmperorBabies>().Extract(baby);
    }
}
