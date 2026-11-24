using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;
using NitroxClient.GameLogic.Spawning.Metadata.Extractor.Abstract;
using UnityEngine;

namespace NitroxClient.GameLogic.Spawning.Metadata.Extractor;

public class WarpedInCreatureMetadataExtractor : EntityMetadataExtractor<WarpedInCreature, WarpedInCreatureMetadata>
{
    public override WarpedInCreatureMetadata Extract(WarpedInCreature warpedInCreature)
    {
        if (!DayNightCycle.main || warpedInCreature.warpOutTime <= 0f)
        {
            return null;
        }
        double warpOutTime = SharedCreatureSpawns.GetWarpOutGameTime(warpedInCreature.warpOutTime, Time.time, DayNightCycle.main.timePassed);
        return new(warpOutTime, Resolve<CreatureInfections>().GetDecidedAmount(warpedInCreature));
    }
}
