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
        foreach ((NitroxId roomId, NitroxInt3 cell) in update.UpdatedMapRooms)
        {
            if (!cells.Add(cell))
            {
                return false;
            }
            if (!entityRegistry.TryGetEntityById(roomId, out MapRoomEntity room))
            {
                return false;
            }
            if (!IsAllowedParent(room.ParentId, update.BaseId, update.ChildrenTransfer))
            {
                return false;
            }
        }
        return true;
    }

    public static bool IsAllowedParent(NitroxId? roomParentId, NitroxId baseId, (NitroxId From, NitroxId To) transfer)
    {
        return roomParentId == baseId || (transfer.To == baseId && transfer.From != null && roomParentId == transfer.From);
    }
}
