using System.Collections.Generic;
using System.Linq;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;

namespace Nitrox.Server.Subnautica.Models.GameLogic.Entities;

/// <summary>
///     One slot of a planter holds one plant. Two players who plant in the same free slot at the same moment both
///     choose it: the first seed to reach the server keeps it, the later one is moved by its game (see
///     <c>PlanterSlotTaken</c>), so that the saved world never holds two seeds for one slot.
/// </summary>
internal static class PlanterSlots
{
    /// <param name="planterChildren">What the planter holds on the server.</param>
    /// <param name="planted">A seed that a game reports planted in the planter.</param>
    /// <returns>True when another seed holds the slot <paramref name="planted" /> was planted in.</returns>
    public static bool IsTaken(IEnumerable<Entity> planterChildren, Entity planted)
    {
        if (planted is not InventoryItemEntity || planted.Metadata is not PlantableMetadata { SlotID: >= 0 } plantedMetadata)
        {
            return false;
        }
        return planterChildren.Any(child => child.Id != planted.Id && child.Metadata is PlantableMetadata other && other.SlotID == plantedMetadata.SlotID);
    }
}
