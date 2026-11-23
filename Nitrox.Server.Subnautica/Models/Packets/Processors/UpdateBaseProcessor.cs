using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Bases;
using Nitrox.Server.Subnautica.Models.GameLogic;
using Nitrox.Server.Subnautica.Models.GameLogic.Bases;
using Nitrox.Server.Subnautica.Models.GameLogic.Entities;
using Nitrox.Server.Subnautica.Models.Packets.Core;

namespace Nitrox.Server.Subnautica.Models.Packets.Processors;

internal sealed class UpdateBaseProcessor(BuildingManager buildingManager, EntitySimulation entitySimulation, ScannerRoomManager scannerRoomManager)
    : BuildingProcessor<UpdateBase>(buildingManager, entitySimulation)
{
    public override async Task Process(AuthProcessorContext context, UpdateBase packet)
    {
        if (BuildingManager.UpdateBase(context.Sender, packet, out int operationId))
        {
            MapRoomEntity? builtRoom = packet.BuiltPieceEntity as MapRoomEntity;
            if (packet.BuiltPieceEntity is GlobalRootEntity entity)
            {
                await TryClaimBuildPieceAsync(context, entity);
            }
            // End-players can process elementary operations without this data (packet would be heavier for no reason)
            packet.Deflate();
            if (builtRoom != null)
            {
                // A Scanner Room comes with two cameras. The other players cannot make them up: their ids are the
                // builder's and their numbers the server's, so they are sent the room as the server stored it.
                packet.BuiltPieceEntity = scannerRoomManager.GetSnapshot(builtRoom.Id)?.Room;
                await scannerRoomManager.CamerasBuiltAsync(context.Sender, builtRoom);
            }
            await SendToOtherPlayersWithOperationIdAsync(context, packet, operationId);
        }
    }
}
