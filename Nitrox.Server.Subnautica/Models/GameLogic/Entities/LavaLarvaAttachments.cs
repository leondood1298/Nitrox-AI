using System.Collections.Generic;
using System.Linq;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;

namespace Nitrox.Server.Subnautica.Models.GameLogic.Entities;

/// <summary>
///     A Lava Larva's stored state names the vehicle it is attached to (<see cref="LavaLarvaMetadata" />). A vehicle can go
///     while the larva stays (destroyed, or its larva's update lost with the game that sent it); a larva must not load
///     attached to a vehicle that is gone.
/// </summary>
internal static class LavaLarvaAttachments
{
    /// <summary>
    ///     Called once the saved world is in the entity registry and before any player can connect.
    /// </summary>
    /// <returns>The larvae that were let go.</returns>
    public static List<Entity> DetachFromMissingVehicles(EntityRegistry entityRegistry)
    {
        List<Entity> detached = [];
        foreach (Entity larva in entityRegistry.GetAllEntities().Where(entity => entity.Metadata is LavaLarvaMetadata { Attached: true }))
        {
            LavaLarvaMetadata state = (LavaLarvaMetadata)larva.Metadata;
            if (state.VehicleId == null || !entityRegistry.TryGetEntityById(state.VehicleId, out Entity _))
            {
                larva.Metadata = state.Detached();
                detached.Add(larva);
            }
        }
        return detached;
    }
}
