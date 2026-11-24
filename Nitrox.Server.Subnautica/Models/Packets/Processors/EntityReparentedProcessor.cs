using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Server.Subnautica.Models.GameLogic;
using Nitrox.Server.Subnautica.Models.GameLogic.Entities;
using Nitrox.Server.Subnautica.Models.Packets.Core;

namespace Nitrox.Server.Subnautica.Models.Packets.Processors;

internal sealed class EntityReparentedProcessor(EntityRegistry entityRegistry, ItemClaims itemClaims, ILogger<EntityReparentedProcessor> logger) : IAuthPacketProcessor<EntityReparented>
{
    private readonly EntityRegistry entityRegistry = entityRegistry;
    private readonly ItemClaims itemClaims = itemClaims;
    private readonly ILogger<EntityReparentedProcessor> logger = logger;

    public async Task Process(AuthProcessorContext context, EntityReparented packet)
    {
        if (!entityRegistry.TryGetEntityById(packet.Id, out Entity entity))
        {
            logger.ZLogError($"Couldn't find entity for {packet.Id}");
            return;
        }
        if (!entityRegistry.TryGetEntityById(packet.NewParentId, out Entity parentEntity))
        {
            logger.ZLogError($"Couldn't find parent entity for {packet.NewParentId}");
            return;
        }

        // Two players who took the same item out of one locker at the same moment: the first keeps it, the other's game
        // is told where it is.
        if (entity.ParentId != null && itemClaims.TryGetOtherHolder(entity, context.Sender, out string? holderName))
        {
            await context.ReplyAsync(new EntityReparented(entity.Id, entity.ParentId));
            await context.ReplyAsync(new ItemClaimLost(entity.Id, entity.TechType, holderName, false, false));
            return;
        }

        entityRegistry.ReparentEntity(packet.Id, packet.NewParentId);
        await context.SendToOthersAsync(packet);
    }
}
