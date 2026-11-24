using System.Collections.Generic;
using System.Linq;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;
using Nitrox.Server.Subnautica.Models.GameLogic;
using Nitrox.Server.Subnautica.Models.GameLogic.Entities;
using Nitrox.Server.Subnautica.Models.Packets.Core;

namespace Nitrox.Server.Subnautica.Models.Packets.Processors;

internal sealed class EntitySpawnedByClientProcessor(PlayerManager playerManager, EntityRegistry entityRegistry, WorldEntityManager worldEntityManager, EntitySimulation entitySimulation,
    ScannerRoomManager scannerRoomManager, ItemClaims itemClaims)
    : IAuthPacketProcessor<EntitySpawnedByClient>
{
    private readonly PlayerManager playerManager = playerManager;
    private readonly EntityRegistry entityRegistry = entityRegistry;
    private readonly WorldEntityManager worldEntityManager = worldEntityManager;
    private readonly EntitySimulation entitySimulation = entitySimulation;
    private readonly ItemClaims itemClaims = itemClaims;

    public async Task Process(AuthProcessorContext context, EntitySpawnedByClient packet)
    {
        Entity entity = packet.Entity;

        if (!await IsClaimWonAsync(context, packet))
        {
            return;
        }

        // If the entity already exists in the registry, it is fine to update.  This is a normal case as the player
        // may have an item in their inventory (that the registry knows about) then wants to spawn it into the world.
        MapRoomCameraMetadata? cameraState = scannerRoomManager.KeepCameraStateOnReplacement(entity);
        entityRegistry.AddOrUpdate(entity);
        if (cameraState != null)
        {
            // The others are sent the entity with this state. The client that made the entity has to be told as well:
            // the number of a new camera is the server's to give.
            await context.ReplyAsync(new EntityMetadataUpdate(entity.Id, cameraState));
        }

        List<SimulatedEntity> simulatedEntities = null;
        if (entity is WorldEntity worldEntity)
        {
            worldEntityManager.TrackEntityInTheWorld(worldEntity);

            if (packet.RequireSimulation)
            {
                simulatedEntities = [.. entitySimulation.AssignEntityToPlayer(worldEntity, context.Sender, true)];
                SimulationOwnershipChange ownershipChangePacket = new(simulatedEntities);
                await context.SendToOthersAsync(ownershipChangePacket);
                // The client that made the entity is told too: otherwise it does not know that it simulates it, and a
                // creature it made (a CrashHome's crashfish, a hatched baby) would neither act nor send its position.
                List<SimulatedEntity> sendersEntities = simulatedEntities.Where(simulated => simulated.SessionId == context.Sender.SessionId).ToList();
                if (sendersEntities.Count > 0)
                {
                    await context.ReplyAsync(new SimulationOwnershipChange(sendersEntities));
                }
            }
        }

        SpawnEntities spawnEntities = new(entity, simulatedEntities, packet.RequireRespawn);
        foreach (Player player in playerManager.GetConnectedPlayers())
        {
            bool isOtherPlayer = player != context.Sender;
            if (isOtherPlayer && player.CanSee(entity))
            {
                await context.SendAsync(spawnEntities, player.SessionId);
            }
        }
    }

    /// <summary>
    ///     Decides between two games that did the same thing at the same moment (see <see cref="ItemClaims" />). The loser
    ///     is corrected.
    /// </summary>
    /// <returns>True if the entity is to be kept.</returns>
    private async Task<bool> IsClaimWonAsync(AuthProcessorContext context, EntitySpawnedByClient packet)
    {
        Entity entity = packet.Entity;

        // An item made from a source (a fruit picked, an outcrop broken) that another game got first
        if (packet.Source != null && !itemClaims.TryClaimSource(packet.Source, context.Sender, out string? winnerName, out bool reserved))
        {
            await ItemClaimCorrections.RemoveMadeItemAsync(context, entity, winnerName, reserved);
            return false;
        }

        // An item another player holds is dropped, installed or moved by a game that has not heard of it yet
        entityRegistry.TryGetEntityById(entity.Id, out Entity? current);
        if (current != null && itemClaims.TryGetOtherHolder(current, context.Sender, out string? holderName))
        {
            await ItemClaimCorrections.PutBackAsync(context, current, holderName);
            return false;
        }

        // One slot holds one item: two players who filled the same slot at the same moment
        string? occupant = null;
        bool slotFree = entity switch
        {
            InstalledModuleEntity module => itemClaims.TryTakeSlot(module, context.Sender, out occupant),
            InstalledBatteryEntity battery => itemClaims.TryTakeBatterySlot(battery, out occupant),
            _ => true
        };
        if (!slotFree)
        {
            if (current != null)
            {
                await ItemClaimCorrections.PutBackAsync(context, current, occupant!);
            }
            else
            {
                await ItemClaimCorrections.RemoveMadeItemAsync(context, entity, occupant!, false);
            }
            return false;
        }
        return true;
    }
}
