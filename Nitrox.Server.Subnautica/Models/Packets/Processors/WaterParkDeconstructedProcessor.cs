using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Server.Subnautica.Models.GameLogic.Bases;
using Nitrox.Server.Subnautica.Models.GameLogic.Entities;
using Nitrox.Server.Subnautica.Models.Packets.Core;

namespace Nitrox.Server.Subnautica.Models.Packets.Processors;

internal sealed class WaterParkDeconstructedProcessor(BuildingManager buildingManager, EntitySimulation entitySimulation)
    : BuildingProcessor<WaterParkDeconstructed>(buildingManager, entitySimulation)
{
    public override async Task Process(AuthProcessorContext context, WaterParkDeconstructed packet)
    {
        if (BuildingManager.ReplacePieceByGhost(context.Sender, packet, out Entity removedEntity, out int operationId) &&
            BuildingManager.CreateWaterParkPiece(packet, removedEntity))
        {
            // The containment split off is a new piece: like a built one it needs a player to simulate it, or its fish
            // are never born, its eggs vanish when they hatch and its planter never grows until somebody joins
            await TryClaimBuildPieceAsync(context, packet.NewWaterPark);
            packet.BaseData = null;
            await SendToOtherPlayersWithOperationIdAsync(context, packet, operationId);
        }
    }
}
