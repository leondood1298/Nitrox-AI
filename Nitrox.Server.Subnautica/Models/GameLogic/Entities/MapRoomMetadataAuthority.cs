using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Bases;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;

namespace Nitrox.Server.Subnautica.Models.GameLogic.Entities;

internal static class MapRoomMetadataAuthority
{
    public static bool TryAcceptAndApply(MapRoomEntity room, MapRoomMetadata requested, out MapRoomMetadata accepted)
    {
        lock (room)
        {
            if (!TryAccept(room.Metadata as MapRoomMetadata, requested, out accepted))
            {
                return false;
            }
            room.Metadata = accepted;
            room.BeginScanResultGeneration(accepted.Generation);
            return true;
        }
    }

    public static bool TryAccept(MapRoomMetadata? current, MapRoomMetadata requested, out MapRoomMetadata accepted)
    {
        current ??= new MapRoomMetadata(NitroxTechType.None, 0);
        accepted = current;

        if (requested.Generation != current.Generation || requested.Revision != current.Revision)
        {
            return false;
        }

        if (!requested.TypeToScan.Equals(current.TypeToScan))
        {
            accepted = new MapRoomMetadata(requested.TypeToScan, 0, current.Generation + 1, current.Revision + 1);
            return true;
        }

        if (requested.NumNodesScanned <= current.NumNodesScanned)
        {
            return false;
        }

        accepted = new MapRoomMetadata(current.TypeToScan, requested.NumNodesScanned, current.Generation, current.Revision + 1);
        return true;
    }
}
