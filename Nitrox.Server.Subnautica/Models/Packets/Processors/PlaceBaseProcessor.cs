using System.Collections.Generic;
using System.Linq;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Bases;
using Nitrox.Server.Subnautica.Models.GameLogic;
using Nitrox.Server.Subnautica.Models.GameLogic.Bases;
using Nitrox.Server.Subnautica.Models.GameLogic.Entities;
using Nitrox.Server.Subnautica.Models.Packets.Core;

namespace Nitrox.Server.Subnautica.Models.Packets.Processors;

internal sealed class PlaceBaseProcessor(BuildingManager buildingManager, EntitySimulation entitySimulation, ScannerRoomManager scannerRoomManager)
    : BuildingProcessor<PlaceBase>(buildingManager, entitySimulation)
{
    public override async Task Process(AuthProcessorContext context, PlaceBase packet)
    {
        if (BuildingManager.CreateBase(packet))
        {
            BuildEntity builtBase = packet.BuildEntity;
            await TryClaimBuildPieceAsync(context, builtBase);
            // A Scanner Room that starts a base is not a built piece of its own, but it needs a simulation owner like
            // one: its scan state is only accepted from that owner.
            List<Entity> builtRooms = [];
            foreach (MapRoomEntity mapRoom in builtBase.ChildEntities.OfType<MapRoomEntity>())
            {
                await TryClaimBuildPieceAsync(context, mapRoom);
                await scannerRoomManager.CamerasBuiltAsync(context.Sender, mapRoom);
                if (scannerRoomManager.GetSnapshot(mapRoom.Id) is { } snapshot)
                {
                    builtRooms.Add(snapshot.Room);
                }
            }

            // End-players can process elementary operations without this data (packet would be heavier for no reason)
            packet.Deflate();
            if (builtRooms.Count > 0)
            {
                // Except for the Scanner Rooms, whose cameras the other players cannot make up, see UpdateBaseProcessor.
                BuildEntity rooms = BuildEntity.MakeEmpty();
                rooms.Id = builtBase.Id;
                rooms.Transform = builtBase.Transform;
                rooms.ChildEntities = builtRooms;
                packet.BuildEntity = rooms;
            }
            await context.SendToOthersAsync(packet);
        }
    }
}
