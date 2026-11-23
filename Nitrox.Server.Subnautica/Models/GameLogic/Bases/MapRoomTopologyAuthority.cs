using System.Collections.Generic;
using Nitrox.Model.DataStructures;
using Nitrox.Model.DataStructures.Unity;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Bases;
using Nitrox.Model.Subnautica.Packets;
using Nitrox.Server.Subnautica.Models.GameLogic.Entities;

namespace Nitrox.Server.Subnautica.Models.GameLogic.Bases;

internal static class MapRoomTopologyAuthority
{
    public static bool Validate(EntityRegistry entityRegistry, UpdateBase update)
    {
        if (update.UpdatedMapRooms == null)
        {
            return false;
        }

        HashSet<NitroxInt3> cells = [];
        MapRoomEntity? builtRoom = update.BuiltPieceEntity as MapRoomEntity;
        bool builtRoomValidated = builtRoom == null;
        foreach ((NitroxId roomId, NitroxInt3 cell) in update.UpdatedMapRooms)
        {
            if (!cells.Add(cell))
            {
                return false;
            }
            if (!entityRegistry.TryGetEntityById(roomId, out MapRoomEntity room))
            {
                if (builtRoom == null ||
                    builtRoomValidated ||
                    builtRoom.Id != roomId ||
                    builtRoom.Id != update.FormerGhostId ||
                    builtRoom.Cell != cell ||
                    builtRoom.ParentId != update.BaseId ||
                    !IsPendingMapRoomGhost(entityRegistry, update))
                {
                    return false;
                }
                room = builtRoom;
                builtRoomValidated = true;
            }
            if (!IsAllowedParent(room.ParentId, update.BaseId, update.ChildrenTransfer))
            {
                return false;
            }
        }
        return builtRoomValidated;
    }

    public static bool IsAllowedParent(NitroxId? roomParentId, NitroxId baseId, (NitroxId From, NitroxId To) transfer)
    {
        return roomParentId == baseId || (transfer.To == baseId && transfer.From != null && roomParentId == transfer.From);
    }

    private static bool IsPendingMapRoomGhost(EntityRegistry entityRegistry, UpdateBase update)
    {
        return entityRegistry.TryGetEntityById(update.FormerGhostId, out GhostEntity ghost) &&
               ghost.TechType?.Name == "BaseMapRoom" &&
               ghost.ParentId == update.BaseId;
    }
}
