using Nitrox.Model.Core;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Bases;
using Nitrox.Model.Subnautica.Packets;
using Nitrox.Server.Subnautica.Models.GameLogic;
using Nitrox.Server.Subnautica.Models.GameLogic.Entities;
using Nitrox.Server.Subnautica.Models.Packets.Core;

namespace Nitrox.Server.Subnautica.Models.Packets.Processors;

internal sealed class PickupItemPacketProcessor(EntityRegistry entityRegistry, WorldEntityManager worldEntityManager, SimulationOwnershipData simulationOwnershipData)
    : IAuthPacketProcessor<PickupItem>
{
    private readonly EntityRegistry entityRegistry = entityRegistry;
    private readonly WorldEntityManager worldEntityManager = worldEntityManager;
    private readonly SimulationOwnershipData simulationOwnershipData = simulationOwnershipData;

    public async Task Process(AuthProcessorContext context, PickupItem packet)
    {
        NitroxId id = packet.Item.Id;
        if (simulationOwnershipData.RevokeOwnerOfId(id))
        {
            SimulationOwnershipChange simulationOwnershipChange = new(id, SessionId.SERVER_ID, SimulationLockType.TRANSIENT);
            await context.SendToAllAsync(simulationOwnershipChange);
        }

        StopTrackingExistingWorldEntity(id);

        entityRegistry.AddOrUpdate(packet.Item);

        foreach (MapRoomEntity mapRoom in entityRegistry.GetEntities<MapRoomEntity>())
        {
            MapRoomCameraDock? undock = null;
            lock (mapRoom)
            {
                if (mapRoom.TryClearDockedCamera(id, out int dockingIndex))
                {
                    MapRoomCameraRecord? record = mapRoom.GetCameraRecord(id);
                    undock = new MapRoomCameraDock(id, mapRoom.Id, dockingIndex, mapRoom.DockingRevision, true, true, false,
                        record?.CameraNumber ?? 0, record?.LightOn ?? false, record?.LightRevision ?? 0,
                        record?.Energy ?? 100f, record?.Health ?? 100f, record?.ComponentRevision ?? 0);
                }
            }
            if (undock != null)
            {
                await context.SendToAllAsync(undock);
            }
        }

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
