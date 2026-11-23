using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Bases;
using Nitrox.Server.Subnautica.Models.GameLogic;
using Nitrox.Server.Subnautica.Models.GameLogic.Entities;
using Nitrox.Server.Subnautica.Models.Packets.Core;

namespace Nitrox.Server.Subnautica.Models.Packets.Processors;

internal sealed class EntityMetadataUpdateProcessor(PlayerManager playerManager, EntityRegistry entityRegistry, SimulationOwnershipData simulationOwnershipData, ScannerRoomManager scannerRoomManager, BasePowerManager basePowerManager, ILogger<EntityMetadataUpdateProcessor> logger) : IAuthPacketProcessor<EntityMetadataUpdate>
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
            if (packet.NewValue is MapRoomCameraMetadata)
            {
                // The player who simulated a camera can report its battery once more while the camera's removal is on
                // its way to them.
                scannerRoomManager.RejectMetadataForUnknownCamera(context.Sender, packet.Id);
                return;
            }
            logger.ZLogError($"Entity metadata {packet.NewValue.GetType()} updated on an entity unknown to the server {packet.Id}");
            return;
        }

        if ((GateEscapePodMetadata(context.Sender, entity, packet.NewValue) ??
             GatePowerMetadata(context.Sender, entity, packet.NewValue) ??
             GateScannerRoomMetadata(context.Sender, entity, packet.NewValue) ??
             GateCreatureMetadata(context.Sender, entity, packet.NewValue)) is { } result)
        {
            await SendResultAsync(context, entity, result);
            return;
        }

        EntityMetadata acceptedMetadata = packet.NewValue;
        if (TryProcessMetadata(context.Sender, entity, ref acceptedMetadata))
        {
            entity.Metadata = acceptedMetadata;
            await SendUpdateToVisiblePlayersAsync(context, new EntityMetadataUpdate(packet.Id, acceptedMetadata), entity, false);
        }
    }

    /// <summary>
    ///     A pod's repair state is its only metadata. Any other kind would take its place, and the pod and its radio would
    ///     come back broken for the next player who joins and after a restart.
    /// </summary>
    /// <returns>Null when the update is no replacement of a pod's repair state.</returns>
    private MetadataUpdateResult? GateEscapePodMetadata(Player sender, Entity entity, EntityMetadata metadata)
    {
        if (entity is not EscapePodEntity || metadata is EscapePodMetadata)
        {
            return null;
        }
        logger.ZLogWarning($"Player {sender.Name} tried replacing the repair state of escape pod {entity.Id} with {metadata.GetType().Name}");
        return MetadataUpdateResult.Reject;
    }

    /// <summary>
    ///     The arms for base power: each hands the update to <see cref="BasePowerManager" />, which stores what it accepts.
    /// </summary>
    /// <returns>Null when the update has nothing to do with base power.</returns>
    private MetadataUpdateResult? GatePowerMetadata(Player sender, Entity entity, EntityMetadata metadata) => metadata switch
    {
        PowerSourceMetadata powerState => basePowerManager.AcceptState(sender, entity, powerState),
        ThermalPlantMetadata temperatureReport => basePowerManager.AcceptTemperature(entity, temperatureReport),
        // A Lifepod cell holds nothing but its power state, also before its first report
        _ when entity.Metadata is PowerSourceMetadata || entity is EscapePodPowerCellEntity => basePowerManager.RejectReplacement(sender, entity, metadata),
        _ => null
    };

    /// <summary>
    ///     The arms for Scanner Rooms: each hands the update to <see cref="ScannerRoomManager" />, which stores what it accepts.
    /// </summary>
    /// <returns>Null when the update has nothing to do with Scanner Rooms.</returns>
    private MetadataUpdateResult? GateScannerRoomMetadata(Player sender, Entity entity, EntityMetadata metadata) => metadata switch
    {
        MapRoomMetadata scanState => scannerRoomManager.AcceptRoomMetadata(sender, entity, scanState),
        MapRoomCameraMetadata cameraState => scannerRoomManager.TryAcceptCameraMetadata(sender, entity, cameraState),
        CrafterMetadata when entity is MapRoomEntity room => scannerRoomManager.RejectFabricatorStateForRoom(sender, room),
        _ when ScannerRoomManager.HoldsScannerRoomState(entity) => scannerRoomManager.RejectReplacement(sender, entity, metadata),
        _ => null
    };

    /// <summary>
    ///     A creature's infection is merged into the creature metadata it already has (see <see cref="CreatureMetadataAuthority" />).
    ///     The other players get an infection update as it was sent, other creature metadata as it was merged.
    /// </summary>
    /// <returns>Null when the update is no creature metadata.</returns>
    private MetadataUpdateResult? GateCreatureMetadata(Player sender, Entity entity, EntityMetadata metadata)
    {
        if (metadata is not CreatureMetadata requested)
        {
            return null;
        }
        if (requested is CreatureInfectionMetadata && !CreatureMetadataAuthority.IsFromSimulatingPlayer(simulationOwnershipData.GetPlayerForLock(entity.Id), sender))
        {
            return MetadataUpdateResult.Reject;
        }
        if (CreatureMetadataAuthority.Merge(entity.Metadata, requested) is not { } accepted)
        {
            return MetadataUpdateResult.Reject;
        }
        entity.Metadata = accepted;
        // Other creature metadata is sent as merged (a Sea Emperor baby's drops only add up)
        return MetadataUpdateResult.Accept(requested is CreatureInfectionMetadata ? requested : accepted);
    }

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

        if (metadata is CyclopsMetadata requestedCyclops)
        {
            Player? simulator = simulationOwnershipData.GetPlayerForLock(entity.Id);
            metadata = CyclopsMetadataAuthority.Merge(entity.Metadata as CyclopsMetadata, requestedCyclops, simulator == null || simulator == sendingPlayer);
            return true;
        }

        return metadata switch
        {
            PlayerMetadata playerMetadata => ProcessPlayerMetadata(sendingPlayer, entity, playerMetadata),
            MedicalCabinetMetadata => ProcessMedicalCabinetMetadata(sendingPlayer, entity),
            PlayerTimeCapsuleMetadata => ProcessPlayerTimeCapsuleMetadata(sendingPlayer, entity),

            // Allow metadata updates from any player by default
            _ => true
        };
    }

    /// <summary>
    ///     Any player may click a cabinet or see its medkit appear. Two players who take the medkit at the same moment may
    ///     both get one; the server does not decide between them.
    /// </summary>
    private bool ProcessMedicalCabinetMetadata(Player sendingPlayer, Entity entity)
    {
        // The Lifepod's cabinet and a cabinet built in a base both carry the cabinet's tech type.
        if (entity.TechType?.Name == "MedicalCabinet")
        {
            return true;
        }
        logger.ZLogWarningOnce($"Player {sendingPlayer.Name} tried applying medical cabinet metadata to {entity.TechType?.Name ?? "None"} entity {entity.Id}");
        return false;
    }

    /// <summary>
    ///     Any player may write the text of a rocket's time capsule, but only on the capsule's entity: on the rocket it would
    ///     take the place of the rocket's stages and preflight checks.
    /// </summary>
    private bool ProcessPlayerTimeCapsuleMetadata(Player sendingPlayer, Entity entity)
    {
        if (entity is PlayerTimeCapsuleEntity)
        {
            return true;
        }
        logger.ZLogWarningOnce($"Player {sendingPlayer.Name} tried applying time capsule metadata to {entity.GetType().Name} {entity.Id}");
        return false;
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
