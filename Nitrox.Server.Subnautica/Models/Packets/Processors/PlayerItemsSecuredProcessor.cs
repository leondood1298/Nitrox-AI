using System.Linq;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities;
using Nitrox.Server.Subnautica.Models.GameLogic.Entities;
using Nitrox.Server.Subnautica.Models.Packets.Core;

namespace Nitrox.Server.Subnautica.Models.Packets.Processors;

/// <summary>
///     Keeps that the items a player carries are secured, so that they are safe on death after a rejoin too.
/// </summary>
internal sealed class PlayerItemsSecuredProcessor(EntityRegistry entityRegistry) : IAuthPacketProcessor<PlayerItemsSecured>
{
    private readonly EntityRegistry entityRegistry = entityRegistry;

    public Task Process(AuthProcessorContext context, PlayerItemsSecured packet)
    {
        if (entityRegistry.TryGetEntityById(context.Sender.GameObjectId, out Entity? player))
        {
            foreach (InventoryItemEntity item in player.ChildEntities.OfType<InventoryItemEntity>())
            {
                item.DestroyOnDeath = false;
            }
        }
        return Task.CompletedTask;
    }
}
