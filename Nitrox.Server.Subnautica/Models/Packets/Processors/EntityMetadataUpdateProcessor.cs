using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Bases;
using Nitrox.Server.Subnautica.Models.GameLogic;
using Nitrox.Server.Subnautica.Models.GameLogic.Entities;
using Nitrox.Server.Subnautica.Models.Packets.Core;

namespace Nitrox.Server.Subnautica.Models.Packets.Processors;

internal sealed class EntityMetadataUpdateProcessor(PlayerManager playerManager, EntityRegistry entityRegistry, SimulationOwnershipData simulationOwnershipData, ScannerRoomDiagnostics scannerRoomDiagnostics, BasePowerManager basePowerManager, ILogger<EntityMetadataUpdateProcessor> logger) : IAuthPacketProcessor<EntityMetadataUpdate>
{
    private readonly PlayerManager playerManager = playerManager;
    private readonly EntityRegistry entityRegistry = entityRegistry;
    private readonly ILogger<EntityMetadataUpdateProcessor> logger = logger;

    public async Task Process(AuthProcessorContext context, EntityMetadataUpdate packet)
    {
        if (!entityRegistry.TryGetEntityById(packet.Id, out Entity entity))
        {
            if (packet.NewValue is PowerSourceMetadata)
            {
                // A client that simulates a source the server does not have sends this up to once a second. The base
                // power diagnostics thin out the repeats; an error per packet would not.
                basePowerManager.RejectUnknownSource(context.Sender, packet.Id);
                return;
            }
            logger.ZLogError($"Entity metadata {packet.NewValue.GetType()} updated on an entity unknown to the server {packet.Id}");
            return;
        }

        if (GatePowerMetadata(context.Sender, entity, packet.NewValue) is { } powerResult)
        {
            await SendResultAsync(context, entity, powerResult);
            return;
        }

        MapRoomMetadata? previousMapRoomMetadata = entity is MapRoomEntity ? entity.Metadata as MapRoomMetadata : null;
        CrafterMetadata? previousCrafterMetadata = entity is MapRoomEntity existingMapRoom ? existingMapRoom.FabricatorMetadata : null;
        EntityMetadata acceptedMetadata = packet.NewValue;
        if (TryProcessMetadata(context.Sender, entity, ref acceptedMetadata))
        {
            if (entity is MapRoomEntity mapRoom && acceptedMetadata is CrafterMetadata crafterMetadata)
            {
                lock (mapRoom)
                {
                    mapRoom.FabricatorMetadata = crafterMetadata;
                }
            }
            else if (acceptedMetadata is not MapRoomMetadata)
            {
                entity.Metadata = acceptedMetadata;
            }
            EntityMetadataUpdate acceptedUpdate = new(packet.Id, acceptedMetadata);
            if (entity is MapRoomEntity diagnosticRoom && acceptedMetadata is MapRoomMetadata acceptedMapRoomMetadata &&
                (previousMapRoomMetadata == null || !Equals(previousMapRoomMetadata.TypeToScan, acceptedMapRoomMetadata.TypeToScan) || previousMapRoomMetadata.Generation != acceptedMapRoomMetadata.Generation))
            {
                scannerRoomDiagnostics.RecordAccepted("scan_target", diagnosticRoom, sessionId: context.Sender.SessionId,
                    reason: acceptedMapRoomMetadata.TypeToScan?.Name ?? "none");
            }
            else if (entity is MapRoomEntity craftingRoom && acceptedMetadata is CrafterMetadata acceptedCrafterMetadata &&
                     (previousCrafterMetadata == null || !Equals(previousCrafterMetadata.TechType, acceptedCrafterMetadata.TechType) || previousCrafterMetadata.StartTime != acceptedCrafterMetadata.StartTime))
            {
                scannerRoomDiagnostics.RecordAccepted("fabricator", craftingRoom, sessionId: context.Sender.SessionId,
                    reason: acceptedCrafterMetadata.TechType?.Name ?? "none");
            }
            await SendUpdateToVisiblePlayersAsync(context, acceptedUpdate, entity, acceptedMetadata is MapRoomMetadata);
        }
        else if (packet.NewValue is MapRoomMetadata && entity is MapRoomEntity refusedMapRoom)
        {
            // The sender has already applied its request locally: its Scanner Room shows the target or progress it
            // asked for. Nothing else would tell it that the room is in another state, and with the generation and
            // revision it holds every later request would be refused as stale too. It is sent the room's real state.
            MapRoomMetadata canonicalMetadata;
            lock (refusedMapRoom)
            {
                canonicalMetadata = refusedMapRoom.Metadata as MapRoomMetadata ?? new MapRoomMetadata(NitroxTechType.None, 0);
            }
            await context.ReplyAsync(new EntityMetadataUpdate(packet.Id, canonicalMetadata));
        }
    }

    /// <summary>
    ///     The arms for base power: each hands the update to <see cref="BasePowerManager" />, which stores what it accepts.
    /// </summary>
    /// <returns>Null when the update has nothing to do with base power.</returns>
    private MetadataUpdateResult? GatePowerMetadata(Player sender, Entity entity, EntityMetadata metadata) => metadata switch
    {
        PowerSourceMetadata powerState => basePowerManager.AcceptState(sender, entity, powerState),
        ThermalPlantMetadata temperatureReport => basePowerManager.AcceptTemperature(entity, temperatureReport),
        _ when entity.Metadata is PowerSourceMetadata => basePowerManager.RejectReplacement(sender, entity, metadata),
        _ => null
    };

    /// <summary>
    ///     Sends what a manager decided about an update. The manager has already stored what it accepted.
    /// </summary>
    private async Task SendResultAsync(AuthProcessorContext context, Entity entity, MetadataUpdateResult result)
    {
        switch (result.Outcome)
        {
            case MetadataUpdateOutcome.ACCEPT:
                await SendUpdateToVisiblePlayersAsync(context, new EntityMetadataUpdate(entity.Id, result.Metadata), entity, false);
                break;
            case MetadataUpdateOutcome.ACCEPT_AND_ECHO:
                await SendUpdateToVisiblePlayersAsync(context, new EntityMetadataUpdate(entity.Id, result.Metadata), entity, true);
                break;
            case MetadataUpdateOutcome.REJECT_AND_CORRECT:
                await context.ReplyAsync(new EntityMetadataUpdate(entity.Id, result.Metadata));
                break;
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
                scannerRoomDiagnostics.RecordRejected("scan_progress", mapRoom, sessionId: sendingPlayer.SessionId, reason: "non_owner");
                return false;
            }

            if (!MapRoomMetadataAuthority.TryAcceptAndApply(mapRoom, requestedMapRoomMetadata, out MapRoomMetadata acceptedMapRoomMetadata))
            {
                logger.ZLogDebug($"Rejected stale or duplicate Map Room metadata for {entity.Id}: requested {requestedMapRoomMetadata}, current {entity.Metadata}");
                scannerRoomDiagnostics.RecordStaleScanRequest(mapRoom, sendingPlayer.SessionId, requestedMapRoomMetadata);
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

        if (metadata is MapRoomCameraMetadata)
        {
            // Only the camera light and component packets may change this, after their ownership checks.
            logger.ZLogWarning($"Player {sendingPlayer.Name} tried updating camera {entity.Id} state through generic metadata");
            return false;
        }

        if (metadata is CyclopsMetadata requestedCyclops)
        {
            Player? simulator = simulationOwnershipData.GetPlayerForLock(entity.Id);
            metadata = CyclopsMetadataAuthority.Merge(entity.Metadata as CyclopsMetadata, requestedCyclops, simulator == null || simulator == sendingPlayer);
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
