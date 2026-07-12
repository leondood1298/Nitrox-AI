using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Bases;
using Nitrox.Server.Subnautica.Models.GameLogic;
using Nitrox.Server.Subnautica.Models.GameLogic.Entities;
using Nitrox.Server.Subnautica.Models.Packets.Core;

namespace Nitrox.Server.Subnautica.Models.Packets.Processors;

internal sealed class EntityMetadataUpdateProcessor(PlayerManager playerManager, EntityRegistry entityRegistry, SimulationOwnershipData simulationOwnershipData, ILogger<EntityMetadataUpdateProcessor> logger) : IAuthPacketProcessor<EntityMetadataUpdate>
{
    private readonly PlayerManager playerManager = playerManager;
    private readonly EntityRegistry entityRegistry = entityRegistry;
    private readonly ILogger<EntityMetadataUpdateProcessor> logger = logger;

    public async Task Process(AuthProcessorContext context, EntityMetadataUpdate packet)
    {
        if (!entityRegistry.TryGetEntityById(packet.Id, out Entity entity))
        {
            logger.ZLogError($"Entity metadata {packet.NewValue.GetType()} updated on an entity unknown to the server {packet.Id}");
            return;
        }

        EntityMetadata acceptedMetadata = packet.NewValue;
        if (TryProcessMetadata(context.Sender, entity, ref acceptedMetadata))
        {
            if (entity is MapRoomEntity mapRoom && acceptedMetadata is CrafterMetadata crafterMetadata)
            {
                mapRoom.FabricatorMetadata = crafterMetadata;
            }
            else if (acceptedMetadata is not MapRoomMetadata)
            {
                entity.Metadata = acceptedMetadata;
            }
            EntityMetadataUpdate acceptedUpdate = new(packet.Id, acceptedMetadata);
            await SendUpdateToVisiblePlayersAsync(context, acceptedUpdate, entity, acceptedMetadata is MapRoomMetadata);
        }
    }

    private async Task SendUpdateToVisiblePlayersAsync(AuthProcessorContext context, EntityMetadataUpdate packet, Entity entity, bool includeSender)
    {
        foreach (Player player in playerManager.GetConnectedPlayers())
        {
            bool updateVisibleToPlayer = player.CanSee(entity);
            if ((includeSender || player != context.Sender) && updateVisibleToPlayer)
            {
                await context.SendAsync(packet, player.SessionId);
            }
        }
    }

    private bool TryProcessMetadata(Player sendingPlayer, Entity entity, ref EntityMetadata metadata)
    {
        if (metadata is MapRoomMetadata requestedMapRoomMetadata)
        {
            if (entity is not MapRoomEntity mapRoom)
            {
                logger.ZLogWarning($"Player {sendingPlayer.Name} tried applying Map Room metadata to non-Map Room entity {entity.Id}");
                return false;
            }

            if (!sendingPlayer.CanSee(entity))
            {
                logger.ZLogWarning($"Player {sendingPlayer.Name} tried updating Map Room {entity.Id} while it was not visible");
                return false;
            }

            if (MapRoomMetadataAuthority.IsProgressUpdate(entity.Metadata as MapRoomMetadata, requestedMapRoomMetadata) && simulationOwnershipData.GetPlayerForLock(entity.Id) != sendingPlayer)
            {
                logger.ZLogWarning($"Player {sendingPlayer.Name} tried updating scan progress for Map Room {entity.Id} without simulation ownership");
                return false;
            }

            if (!MapRoomMetadataAuthority.TryAcceptAndApply(mapRoom, requestedMapRoomMetadata, out MapRoomMetadata acceptedMapRoomMetadata))
            {
                logger.ZLogDebug($"Rejected stale or duplicate Map Room metadata for {entity.Id}: requested {requestedMapRoomMetadata}, current {entity.Metadata}");
                return false;
            }

            metadata = acceptedMapRoomMetadata;
            return true;
        }

        if (metadata is CrafterMetadata && entity is MapRoomEntity)
        {
            if (simulationOwnershipData.GetPlayerForLock(entity.Id) != sendingPlayer)
            {
                logger.ZLogWarning($"Player {sendingPlayer.Name} tried updating Scanner Room fabricator {entity.Id} without simulation ownership");
                return false;
            }
            return true;
        }

        if (metadata is EscapePodMetadata requestedEscapePodMetadata)
        {
            if (entity is not EscapePodEntity)
            {
                logger.ZLogWarning($"Player {sendingPlayer.Name} tried applying Escape Pod metadata to non-Escape Pod entity {entity.Id}");
                return false;
            }
            metadata = EscapePodMetadataAuthority.Merge(entity.Metadata as EscapePodMetadata, requestedEscapePodMetadata);
            return true;
        }

        return metadata switch
        {
            PlayerMetadata playerMetadata => ProcessPlayerMetadata(sendingPlayer, entity, playerMetadata),

            // Allow metadata updates from any player by default
            _ => true
        };
    }

    private bool ProcessPlayerMetadata(Player sendingPlayer, Entity entity, PlayerMetadata metadata)
    {
        if (sendingPlayer.GameObjectId == entity.Id)
        {
            sendingPlayer.EquippedItems.Clear();
            foreach (PlayerMetadata.EquippedItem item in metadata.EquippedItems)
            {
                sendingPlayer.EquippedItems.Add(item.Slot, item.Id);
            }

            return true;
        }

        logger.ZLogWarningOnce($"Player {sendingPlayer.Name} tried updating metadata of another player's entity {entity.Id}");
        return false;
    }
}
