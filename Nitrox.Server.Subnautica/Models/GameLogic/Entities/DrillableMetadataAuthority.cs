using System;
using System.Linq;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;

namespace Nitrox.Server.Subnautica.Models.GameLogic.Entities;

/// <summary>
///     Merges the chunk health of a large resource deposit drilled by several Prawn suits. Each game drills its own copy
///     and reports all chunks, so a report can carry a chunk as it was before another player broke it. Taken as sent, it
///     made the broken chunk come back for everybody, to be drilled again for more resources.
///     <list type="bullet">
///         <item>Each chunk keeps the lowest health reported: drilling only takes health away.</item>
///         <item>The one way back up is the game restoring a fully drilled deposit (the ion cube deposit in the Primary
///         Containment Facility, a child of the facility): every chunk whole again.</item>
///     </list>
/// </summary>
internal static class DrillableMetadataAuthority
{
    /// <returns>Null when the report cannot be merged (chunk count differs).</returns>
    public static DrillableMetadata? Merge(DrillableMetadata? stored, DrillableMetadata requested, bool mayBeRestored)
    {
        if (stored?.ChunkHealth == null || requested.ChunkHealth == null)
        {
            return requested;
        }
        if (stored.ChunkHealth.Length != requested.ChunkHealth.Length)
        {
            return null;
        }
        if (mayBeRestored && stored.ChunkHealth.All(health => health <= 0f) && requested.ChunkHealth.All(health => health > 0f))
        {
            return requested;
        }

        float[] merged = new float[stored.ChunkHealth.Length];
        for (int i = 0; i < merged.Length; i++)
        {
            merged[i] = Math.Min(stored.ChunkHealth[i], requested.ChunkHealth[i]);
        }
        return new DrillableMetadata(merged, Math.Max(stored.TimeLastDrilled, requested.TimeLastDrilled));
    }

    /// <returns>True when the sender's copy shows a chunk healthier than the merged state, so it must be given the merged one.</returns>
    public static bool SenderIsBehind(DrillableMetadata requested, DrillableMetadata accepted)
    {
        if (requested.ChunkHealth == null || accepted.ChunkHealth == null)
        {
            return false;
        }
        for (int i = 0; i < requested.ChunkHealth.Length; i++)
        {
            if (requested.ChunkHealth[i] > accepted.ChunkHealth[i])
            {
                return true;
            }
        }
        return false;
    }
}
