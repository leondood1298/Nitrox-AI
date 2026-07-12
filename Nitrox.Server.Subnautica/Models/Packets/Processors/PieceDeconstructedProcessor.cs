using Nitrox.Server.Subnautica.Models.GameLogic.Bases;
using Nitrox.Server.Subnautica.Models.GameLogic;
using Nitrox.Server.Subnautica.Models.Packets.Core;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Bases;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.Packets;

namespace Nitrox.Server.Subnautica.Models.Packets.Processors;

internal sealed class PieceDeconstructedProcessor(BuildingManager buildingManager, SimulationOwnershipData simulationOwnershipData, ILogger<PieceDeconstructedProcessor> logger) : BuildingProcessor<PieceDeconstructed>(buildingManager)
{
    private readonly SimulationOwnershipData simulationOwnershipData = simulationOwnershipData;
    private readonly ILogger<PieceDeconstructedProcessor> logger = logger;

    public override async Task Process(AuthProcessorContext context, PieceDeconstructed packet)
    {
        if (BuildingManager.ReplacePieceByGhost(context.Sender, packet, out Entity? removedEntity, out int operationId))
        {
			if (removedEntity is MapRoomEntity mapRoom)
			{
				foreach (MapRoomCameraRecord camera in mapRoom.CameraRegistry)
				{
					simulationOwnershipData.RevokeOwnerOfId(camera.CameraId);
					await context.SendToAllAsync(new DropSimulationOwnership(camera.CameraId));
				}
				logger.ZLogInformation($"Cleaned {mapRoom.CameraRegistry.Count} camera registration(s) while deconstructing Scanner Room {mapRoom.Id}");
			}
            packet.BaseData = null;
            await SendToOtherPlayersWithOperationIdAsync(context, packet, operationId);
        }
    }
}
