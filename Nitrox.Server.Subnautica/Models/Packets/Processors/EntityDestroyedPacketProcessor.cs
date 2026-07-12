using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Bases;
using Nitrox.Model.Subnautica.Packets;
using Nitrox.Server.Subnautica.Models.GameLogic;
using Nitrox.Server.Subnautica.Models.GameLogic.Entities;
using Nitrox.Server.Subnautica.Models.Packets.Core;

namespace Nitrox.Server.Subnautica.Models.Packets.Processors;

internal sealed class EntityDestroyedPacketProcessor(PlayerManager playerManager, EntitySimulation entitySimulation, WorldEntityManager worldEntityManager, EntityRegistry entityRegistry) : IAuthPacketProcessor<EntityDestroyed>
{
    private readonly PlayerManager playerManager = playerManager;
    private readonly EntitySimulation entitySimulation = entitySimulation;
    private readonly WorldEntityManager worldEntityManager = worldEntityManager;
    private readonly EntityRegistry entityRegistry = entityRegistry;

    public async Task Process(AuthProcessorContext context, EntityDestroyed packet)
    {
        entitySimulation.EntityDestroyed(packet.Id);

        foreach (MapRoomEntity mapRoom in entityRegistry.GetEntities<MapRoomEntity>())
        {
            MapRoomCameraDock? undock = null;
            lock (mapRoom)
            {
                MapRoomCameraRecord? record = mapRoom.GetCameraRecord(packet.Id);
                if (mapRoom.RemoveCamera(packet.Id, out int dockingIndex) && dockingIndex >= 0)
                {
                    undock = new MapRoomCameraDock(packet.Id, mapRoom.Id, dockingIndex, mapRoom.DockingRevision, true, true, false,
                        record?.CameraNumber ?? 0, false, record?.LightRevision ?? 0, record?.Energy ?? 0f, 0f, record?.ComponentRevision ?? 0);
                }
            }
            if (undock != null)
            {
                await context.SendToAllAsync(undock);
            }
        }

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
