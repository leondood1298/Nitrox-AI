using Nitrox.Model.Core;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities;
using Nitrox.Server.Subnautica.Models.GameLogic;
using Nitrox.Server.Subnautica.Models.GameLogic.Entities;
using Nitrox.Server.Subnautica.Models.Packets.Core;

namespace Nitrox.Server.Subnautica.Models.Packets.Processors;

internal sealed class PickupItemPacketProcessor(EntityRegistry entityRegistry, WorldEntityManager worldEntityManager,
    SimulationOwnershipData simulationOwnershipData, ScannerRoomManager scannerRoomManager, ItemClaims itemClaims)
    : IAuthPacketProcessor<PickupItem>
{
    private readonly EntityRegistry entityRegistry = entityRegistry;
    private readonly WorldEntityManager worldEntityManager = worldEntityManager;
    private readonly SimulationOwnershipData simulationOwnershipData = simulationOwnershipData;
    private readonly ItemClaims itemClaims = itemClaims;

    public async Task Process(AuthProcessorContext context, PickupItem packet)
    {
        NitroxId id = packet.Item.Id;

        // Two games that took the same thing at the same moment: the first to tell the server gets it
        if (packet.Source != null && !itemClaims.TryClaimSource(packet.Source, context.Sender, out string? winnerName, out bool reserved))
        {
            await ItemClaimCorrections.RemoveMadeItemAsync(context, packet.Item, winnerName, reserved);
            return;
        }
        if (entityRegistry.TryGetEntityById(id, out Entity? current) && itemClaims.TryGetOtherHolder(current, context.Sender, out string? holderName))
        {
            await ItemClaimCorrections.PutBackAsync(context, current, holderName);
            return;
        }

        // A picked up Scanner Room camera is handled there in full, any other item leaves the scan results it was in.
        if (await scannerRoomManager.EntityPickedUpAsync(context.Sender, packet.Item))
        {
            return;
        }

        if (simulationOwnershipData.RevokeOwnerOfId(id))
        {
            SimulationOwnershipChange simulationOwnershipChange = new(id, SessionId.SERVER_ID, SimulationLockType.TRANSIENT);
            await context.SendToAllAsync(simulationOwnershipChange);
        }

        StopTrackingExistingWorldEntity(id);

        entityRegistry.AddOrUpdate(packet.Item);

        // Have other players respawn the item inside the inventory.
        await context.SendToOthersAsync(new SpawnEntities(packet.Item, forceRespawn: true));
    }

    private void StopTrackingExistingWorldEntity(NitroxId id)
    {
        Optional<Entity> entity = entityRegistry.GetEntityById(id);

        if (entity is { HasValue: true, Value: WorldEntity worldEntity })
        {
            // Do not track this entity in the open world anymore.
            worldEntityManager.StopTrackingEntity(worldEntity);
        }
    }
}
