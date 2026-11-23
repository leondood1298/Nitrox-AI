using System.Linq;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Bases;
using Nitrox.Server.Subnautica.Models.GameLogic.Bases;
using Nitrox.Server.Subnautica.Models.GameLogic.Entities;
using Nitrox.Server.Subnautica.Models.Packets.Core;

namespace Nitrox.Server.Subnautica.Models.Packets.Processors;

internal sealed class PlaceBaseProcessor(BuildingManager buildingManager, EntitySimulation entitySimulation) : BuildingProcessor<PlaceBase>(buildingManager, entitySimulation)
{
    public override async Task Process(AuthProcessorContext context, PlaceBase packet)
    {
        if (BuildingManager.CreateBase(packet))
        {
            await TryClaimBuildPieceAsync(context, packet.BuildEntity);
            // A Scanner Room that starts a base is not a built piece of its own, but it needs a simulation owner like
            // one: its scan state and the docking of its default cameras are only accepted from that owner.
            foreach (MapRoomEntity mapRoom in packet.BuildEntity.ChildEntities.OfType<MapRoomEntity>())
            {
                await TryClaimBuildPieceAsync(context, mapRoom);
            }

            // End-players can process elementary operations without this data (packet would be heavier for no reason)
            packet.Deflate();
            await context.SendToOthersAsync(packet);
        }
    }
}
