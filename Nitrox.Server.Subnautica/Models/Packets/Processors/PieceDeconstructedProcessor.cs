using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Bases;
using Nitrox.Server.Subnautica.Models.GameLogic;
using Nitrox.Server.Subnautica.Models.GameLogic.Bases;
using Nitrox.Server.Subnautica.Models.Packets.Core;

namespace Nitrox.Server.Subnautica.Models.Packets.Processors;

internal sealed class PieceDeconstructedProcessor(BuildingManager buildingManager, ScannerRoomManager scannerRoomManager) : BuildingProcessor<PieceDeconstructed>(buildingManager)
{
    public override async Task Process(AuthProcessorContext context, PieceDeconstructed packet)
    {
        if (BuildingManager.ReplacePieceByGhost(context.Sender, packet, out Entity? removedEntity, out int operationId))
        {
            if (removedEntity is MapRoomEntity mapRoom)
            {
                await scannerRoomManager.RoomDeconstructedAsync(context.Sender, mapRoom);
            }
            packet.BaseData = null;
            await SendToOtherPlayersWithOperationIdAsync(context, packet, operationId);
        }
    }
}
