using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities;
using Nitrox.Server.Subnautica.Models.GameLogic;
using Nitrox.Server.Subnautica.Models.GameLogic.Entities;
using Nitrox.Server.Subnautica.Models.Packets.Core;

namespace Nitrox.Server.Subnautica.Models.Packets.Processors;

internal sealed class EntityDestroyedPacketProcessor(PlayerManager playerManager, EntitySimulation entitySimulation,
    WorldEntityManager worldEntityManager, ScannerRoomManager scannerRoomManager, EntityRegistry entityRegistry, ItemClaims itemClaims)
    : IAuthPacketProcessor<EntityDestroyed>
{
    private readonly PlayerManager playerManager = playerManager;
    private readonly EntitySimulation entitySimulation = entitySimulation;
    private readonly WorldEntityManager worldEntityManager = worldEntityManager;
    private readonly EntityRegistry entityRegistry = entityRegistry;
    private readonly ItemClaims itemClaims = itemClaims;

    public async Task Process(AuthProcessorContext context, EntityDestroyed packet)
    {
        // A player whose game ate or used an item that another player got first would destroy that player's item
        if (entityRegistry.TryGetEntityById(packet.Id, out Entity? held) && itemClaims.TryGetOtherHolder(held, context.Sender, out string? holderName))
        {
            await ItemClaimCorrections.PutBackAsync(context, held, holderName);
            return;
        }

        // First, so players hear that a camera lost its controller and dock before they hear that it is gone.
        await scannerRoomManager.EntityRemovedAsync(context.Sender, packet.Id);
        entitySimulation.EntityDestroyed(packet.Id);

        if (worldEntityManager.TryDestroyEntity(packet.Id, out Entity? entity))
        {
            if (entity is VehicleEntity vehicleEntity)
            {
                worldEntityManager.MovePlayerChildrenToRoot(vehicleEntity);
            }

            foreach (Player player in playerManager.GetConnectedPlayers())
            {
                bool isOtherPlayer = player != context.Sender;
                if (isOtherPlayer && player.CanSee(entity))
                {
                    await context.SendAsync(packet, player.SessionId);
                }
            }
        }
    }
}
