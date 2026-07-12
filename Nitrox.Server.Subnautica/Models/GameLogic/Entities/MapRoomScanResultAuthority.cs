using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Bases;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;
using Nitrox.Model.Subnautica.Packets;

namespace Nitrox.Server.Subnautica.Models.GameLogic.Entities;

internal static class MapRoomScanResultAuthority
{
    public static bool TryApply(MapRoomEntity room, MapRoomScanResultChanged requested)
    {
        lock (room)
        {
            if (requested.IsServerResponse || requested.MapRoomId != room.Id || string.IsNullOrEmpty(requested.ResourceId) || requested.Generation != room.ScanResultGeneration || room.Metadata is not MapRoomMetadata metadata)
            {
                return false;
            }
            if (requested.Removed)
            {
                return room.TryRemoveScanResult(requested.Generation, requested.ResourceId);
            }
            if (!requested.TechType.Equals(metadata.TypeToScan))
            {
                return false;
            }
            return room.TryApplyScanResult(requested.Generation, new MapRoomScanResultRecord(requested.ResourceId, requested.TechType, requested.Position));
        }
    }
}
