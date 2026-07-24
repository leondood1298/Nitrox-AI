using System.Collections.Generic;
using System.Linq;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Bases;
using Nitrox.Server.Subnautica.Models.GameLogic.Bases;
using Nitrox.Server.Subnautica.Models.GameLogic.Entities;
using Nitrox.Server.Subnautica.Models.Packets.Core;

namespace Nitrox.Server.Subnautica.Models.Packets.Processors;

internal sealed class BaseDeconstructedProcessor(BuildingManager buildingManager, EntityRegistry entityRegistry,
    MapRoomDeconstructionCleanup mapRoomCleanup, MapRoomLifecycle roomLifecycle,
    MapRoomCameraControlLifecycle cameraControlLifecycle) : BuildingProcessor<BaseDeconstructed>(buildingManager)
{
    public override async Task Process(AuthProcessorContext context, BaseDeconstructed packet)
    {
        List<MapRoomEntity> removedMapRooms = entityRegistry.TryGetEntityById(packet.FormerBaseId, out Entity root)
            ? Descendants(root).OfType<MapRoomEntity>().ToList()
            : [];
        IReadOnlyList<IDisposable> roomGates = await roomLifecycle.EnterManyAsync(removedMapRooms.Select(room => room.Id));
        try
        {
            foreach (MapRoomEntity mapRoom in removedMapRooms)
            {
                lock (mapRoom)
                {
                    foreach (MapRoomCameraRecord camera in mapRoom.CameraRegistry)
                    {
                        cameraControlLifecycle.RememberKnown(camera.CameraId);
                    }
                }
            }
            if (BuildingManager.ReplaceBaseByGhost(packet))
            {
                foreach (MapRoomEntity mapRoom in removedMapRooms)
                {
                    await mapRoomCleanup.CleanupAsync(mapRoom, context);
                }
                await context.SendToOthersAsync(packet);
            }
        }
        finally
        {
            MapRoomLifecycle.ReleaseReverse(roomGates);
        }
    }

    private static IEnumerable<Entity> Descendants(Entity entity)
    {
        foreach (Entity child in entity.ChildEntities)
        {
            yield return child;
            foreach (Entity descendant in Descendants(child))
            {
                yield return descendant;
            }
        }
    }
}
