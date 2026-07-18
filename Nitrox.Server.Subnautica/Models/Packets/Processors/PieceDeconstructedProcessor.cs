using Nitrox.Server.Subnautica.Models.GameLogic.Bases;
using Nitrox.Server.Subnautica.Models.GameLogic;
using Nitrox.Server.Subnautica.Models.GameLogic.Entities;
using Nitrox.Server.Subnautica.Models.Packets.Core;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Bases;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.Packets;

namespace Nitrox.Server.Subnautica.Models.Packets.Processors;

internal sealed class PieceDeconstructedProcessor(BuildingManager buildingManager, EntityRegistry entityRegistry,
    MapRoomDeconstructionCleanup mapRoomCleanup, MapRoomLifecycle roomLifecycle,
    MapRoomCameraControlLifecycle cameraControlLifecycle,
    ILogger<PieceDeconstructedProcessor> logger) : BuildingProcessor<PieceDeconstructed>(buildingManager)
{
    private readonly ILogger<PieceDeconstructedProcessor> logger = logger;

    public override async Task Process(AuthProcessorContext context, PieceDeconstructed packet)
    {
        IDisposable? roomGate = entityRegistry.TryGetEntityById(packet.PieceId, out MapRoomEntity room)
            ? await roomLifecycle.EnterAsync(room.Id)
            : null;
        try
        {
			if (roomGate != null)
			{
				lock (room)
				{
					foreach (MapRoomCameraRecord camera in room.CameraRegistry)
					{
						cameraControlLifecycle.RememberKnown(camera.CameraId);
					}
				}
			}
			if (BuildingManager.ReplacePieceByGhost(context.Sender, packet, out Entity? removedEntity, out int operationId))
			{
				if (removedEntity is MapRoomEntity mapRoom)
				{
					await mapRoomCleanup.CleanupAsync(mapRoom, context);
					logger.ZLogInformation($"Cleared results, subscriptions, room lock, and {mapRoom.CameraRegistry.Count} camera registration(s) while deconstructing Scanner Room {mapRoom.Id}");
				}
				packet.BaseData = null;
				await SendToOtherPlayersWithOperationIdAsync(context, packet, operationId);
			}
        }
        finally
        {
            roomGate?.Dispose();
        }
    }
}
