using System.Collections.Generic;
using System.Linq;
using Nitrox.Model.DataStructures.Unity;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Bases;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;
using Nitrox.Model.Subnautica.Packets;

namespace Nitrox.Server.Subnautica.Models.GameLogic.Entities;

internal static class MapRoomScanResultAuthority
{
    private const int MAX_SNAPSHOT_RESULTS = 10000;
    private const int MAX_RESOURCE_ID_LENGTH = 256;

    public static bool TryApplySnapshot(MapRoomEntity room, MapRoomScanResultSnapshot requested, out List<MapRoomScanResultRecord> acceptedResults, out long revision)
    {
        lock (room)
        {
            acceptedResults = [];
            revision = room.ScanResultRevision;
            if (requested.IsServerResponse || requested.MapRoomId != room.Id || requested.Generation != room.ScanResultGeneration || requested.Results == null || requested.Results.Count > MAX_SNAPSHOT_RESULTS || room.Metadata is not MapRoomMetadata metadata)
            {
                return false;
            }
            HashSet<string> ids = [];
            foreach (MapRoomScanResultRecord result in requested.Results)
            {
                if (result == null || !IsValidResourceId(result.ResourceId) || !IsFinite(result.Position) || !ids.Add(result.ResourceId) || !result.TechType.Equals(metadata.TypeToScan))
                {
                    return false;
                }
            }
            if (requested.Results.Count > 0 && room.ScanResults.Count == requested.Results.Count && room.ScanResults.Zip(requested.Results, (current, requestedResult) => current.ResourceId == requestedResult.ResourceId && current.TechType.Equals(requestedResult.TechType) && current.Position.Equals(requestedResult.Position)).All(equal => equal))
            {
                return false;
            }
            room.ScanResults = requested.Results.Select(result => new MapRoomScanResultRecord(result.ResourceId, result.TechType, result.Position)).ToList();
            room.ScanResultRevision++;
            revision = room.ScanResultRevision;
            acceptedResults = room.ScanResults.Select(result => new MapRoomScanResultRecord(result.ResourceId, result.TechType, result.Position)).ToList();
            return true;
        }
    }

    public static bool TryApply(MapRoomEntity room, MapRoomScanResultChanged requested)
    {
        lock (room)
        {
            if (requested.IsServerResponse || requested.MapRoomId != room.Id || !IsValidResourceId(requested.ResourceId) || requested.Generation != room.ScanResultGeneration || (!requested.Removed && !IsFinite(requested.Position)) || room.Metadata is not MapRoomMetadata metadata)
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

    private static bool IsValidResourceId(string resourceId) => !string.IsNullOrEmpty(resourceId) && resourceId.Length <= MAX_RESOURCE_ID_LENGTH;

    private static bool IsFinite(NitroxVector3 position) =>
        !float.IsNaN(position.X) && !float.IsInfinity(position.X) &&
        !float.IsNaN(position.Y) && !float.IsInfinity(position.Y) &&
        !float.IsNaN(position.Z) && !float.IsInfinity(position.Z);
}
