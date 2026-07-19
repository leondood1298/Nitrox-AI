using System;
using System.Collections.Generic;
using System.Linq;
using Nitrox.Model.DataStructures;
using Nitrox.Model.DataStructures.Unity;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Bases;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;
using Nitrox.Model.Subnautica.Packets;

namespace Nitrox.Server.Subnautica.Models.GameLogic.Entities;

internal static class MapRoomScanResultAuthority
{
    internal const int MAX_SNAPSHOT_RESULTS = 10000;
    private const int MAX_RESOURCE_ID_LENGTH = 256;

    public static bool TryApplySnapshot(MapRoomEntity room, MapRoomScanResultSnapshot requested, IEnumerable<WorldEntity> worldEntities, NitroxVector3 scanAnchor,
        out List<MapRoomScanResultRecord> acceptedResults, out long revision)
    {
        lock (room)
        {
            acceptedResults = [];
            revision = room.ScanResultRevision;
            if (requested.IsServerResponse || requested.MapRoomId != room.Id || requested.Generation != room.ScanResultGeneration || requested.Results == null || requested.Results.Count > MAX_SNAPSHOT_RESULTS ||
                worldEntities == null || room.Metadata is not MapRoomMetadata metadata ||
                !MapRoomWorldResourceIndex.TryNormalizeQuery(requested.ScanOrigin, requested.ScanRange, scanAnchor,
                    out NitroxVector3 scanOrigin, out float scanRange))
            {
                return false;
            }
            if (!MapRoomWorldResourceIndex.IsRangeAllowed(room, scanRange))
            {
                return false;
            }
            if (metadata.TypeToScan.Equals(NitroxTechType.None) && requested.Results.Count != 0)
            {
                return false;
            }
            HashSet<string> ids = [];
            foreach (MapRoomScanResultRecord result in requested.Results)
            {
                if (result == null || !IsValidResourceId(result.ResourceId) || result.TechType == null || !IsFinite(result.Position) || !ids.Add(result.ResourceId) ||
                    !result.TechType.Equals(metadata.TypeToScan) || !MapRoomWorldResourceIndex.IsWithinRange(scanOrigin, result.Position, scanRange))
                {
                    return false;
                }
            }

            List<MapRoomScanResultRecord> merged = MapRoomWorldResourceIndex.MergeResults(requested.Results, worldEntities,
                metadata.TypeToScan, scanOrigin, scanRange, MAX_SNAPSHOT_RESULTS);
            if (ResultsEqual(room.ScanResults, merged))
            {
                acceptedResults = Clone(room.ScanResults);
                return true;
            }
            room.ScanResults = Clone(merged);
            room.ScanResultRevision++;
            revision = room.ScanResultRevision;
            acceptedResults = Clone(room.ScanResults);
            return true;
        }
    }

    public static bool TryApply(MapRoomEntity room, MapRoomScanResultChanged requested, EntityRegistry entityRegistry, NitroxVector3? scanAnchor,
        out bool canonicalCorrectionRequired)
    {
        canonicalCorrectionRequired = false;
        lock (room)
        {
            if (requested.IsServerResponse || requested.MapRoomId != room.Id || !IsValidResourceId(requested.ResourceId) || requested.TechType == null ||
                requested.Generation != room.ScanResultGeneration || (!requested.Removed && !IsFinite(requested.Position)) ||
                requested.IsRangeExit && !requested.Removed || entityRegistry == null || room.Metadata is not MapRoomMetadata metadata ||
                metadata.TypeToScan.Equals(NitroxTechType.None) || !requested.TechType.Equals(metadata.TypeToScan))
            {
                return false;
            }
            if (requested.Removed)
            {
                MapRoomScanResultRecord? current = room.ScanResults.Find(result => result.ResourceId == requested.ResourceId);
                if (current == null || !current.TechType.Equals(metadata.TypeToScan))
                {
                    return false;
                }

                bool hasLiveExactEntity = TryGetLiveExactWorldEntity(entityRegistry, requested.ResourceId, metadata.TypeToScan, out WorldEntity liveExactEntity);
                if (!requested.IsRangeExit)
                {
                    // A local ResourceTracker can unregister when its cell unloads. The server registry remains canonical
                    // for exact-TechType supplements, while owner-only override mappings retain vanilla removal behavior.
                    if (hasLiveExactEntity)
                    {
                        if (!scanAnchor.HasValue ||
                            !MapRoomWorldResourceIndex.TryNormalizeQuery(requested.ScanOrigin, requested.ScanRange, scanAnchor.Value,
                                out NitroxVector3 removalScanOrigin, out float removalScanRange) ||
                            !MapRoomWorldResourceIndex.IsRangeAllowed(room, removalScanRange) ||
                            !IsFinite(liveExactEntity.Transform.Position))
                        {
                            // Do not amplify an invalid request into a full corrective snapshot.
                            return false;
                        }
                        if (MapRoomWorldResourceIndex.IsWithinRange(removalScanOrigin, liveExactEntity.Transform.Position, removalScanRange))
                        {
                            canonicalCorrectionRequired = true;
                            return false;
                        }
                        return room.TryRemoveScanResult(requested.Generation, requested.ResourceId);
                    }
                    return room.TryRemoveScanResult(requested.Generation, requested.ResourceId);
                }

                if (!scanAnchor.HasValue ||
                    !MapRoomWorldResourceIndex.TryNormalizeQuery(requested.ScanOrigin, requested.ScanRange, scanAnchor.Value,
                        out NitroxVector3 scanOrigin, out float scanRange) ||
                    !MapRoomWorldResourceIndex.IsRangeAllowed(room, scanRange))
                {
                    return false;
                }

                NitroxVector3 position = hasLiveExactEntity ? liveExactEntity.Transform.Position : requested.Position;
                if (!IsFinite(position))
                {
                    return false;
                }
                if (MapRoomWorldResourceIndex.IsWithinRange(scanOrigin, position, scanRange))
                {
                    // Vanilla has already removed an outside-reported node locally. Restore exact server-backed nodes
                    // when the authoritative transform still belongs to this scanner query.
                    canonicalCorrectionRequired = hasLiveExactEntity;
                    return false;
                }
                return room.TryRemoveScanResult(requested.Generation, requested.ResourceId);
            }
            if (!scanAnchor.HasValue ||
                !MapRoomWorldResourceIndex.TryNormalizeQuery(requested.ScanOrigin, requested.ScanRange, scanAnchor.Value,
                    out NitroxVector3 addedScanOrigin, out float addedScanRange) ||
                !MapRoomWorldResourceIndex.IsRangeAllowed(room, addedScanRange))
            {
                return false;
            }
            NitroxVector3 acceptedPosition = TryGetLiveExactWorldEntity(entityRegistry, requested.ResourceId, metadata.TypeToScan,
                out WorldEntity authoritativeEntity) ? authoritativeEntity.Transform.Position : requested.Position;
            if (!IsFinite(acceptedPosition) || !MapRoomWorldResourceIndex.IsWithinRange(addedScanOrigin, acceptedPosition, addedScanRange))
            {
                return false;
            }
            bool isNewResult = room.ScanResults.TrueForAll(result => result.ResourceId != requested.ResourceId);
            if (isNewResult && room.ScanResults.Count >= MAX_SNAPSHOT_RESULTS)
            {
                return false;
            }
            return room.TryApplyScanResult(requested.Generation, new MapRoomScanResultRecord(requested.ResourceId, requested.TechType, acceptedPosition));
        }
    }

    public static List<MapRoomScanResultChanged> InvalidateResource(IEnumerable<MapRoomEntity> rooms, NitroxId resourceId)
    {
        string id = resourceId.ToString();
        List<MapRoomScanResultChanged> removals = [];
        foreach (MapRoomEntity room in rooms)
        {
            lock (room)
            {
                MapRoomScanResultRecord? result = room.ScanResults.Find(record => record.ResourceId == id);
                if (result == null || !room.TryRemoveScanResult(room.ScanResultGeneration, id))
                {
                    continue;
                }
                removals.Add(new MapRoomScanResultChanged(room.Id, room.ScanResultGeneration, id, result.TechType, result.Position,
                    removed: true, isRangeExit: false, scanOrigin: NitroxVector3.Zero, scanRange: 0f,
                    revision: room.ScanResultRevision, isServerResponse: true, granted: true));
            }
        }
        return removals;
    }

    private static bool IsValidResourceId(string resourceId) => !string.IsNullOrEmpty(resourceId) && resourceId.Length <= MAX_RESOURCE_ID_LENGTH;

    private static bool ResultsEqual(IReadOnlyList<MapRoomScanResultRecord> current, IReadOnlyList<MapRoomScanResultRecord> requested) =>
        current.Count == requested.Count && current.Zip(requested, (left, right) =>
            left.ResourceId == right.ResourceId && left.TechType.Equals(right.TechType) && left.Position.Equals(right.Position)).All(equal => equal);

    private static List<MapRoomScanResultRecord> Clone(IEnumerable<MapRoomScanResultRecord> results) =>
        results.Select(result => new MapRoomScanResultRecord(result.ResourceId, result.TechType, result.Position)).ToList();

    private static bool TryGetLiveExactWorldEntity(EntityRegistry entityRegistry, string resourceId, NitroxTechType target,
        out WorldEntity worldEntity)
    {
        worldEntity = null;
        return Guid.TryParse(resourceId, out Guid guid) &&
               entityRegistry.TryGetEntityById(new NitroxId(guid), out worldEntity) &&
               worldEntity.TechType != null && worldEntity.TechType.Equals(target) && worldEntity.Transform != null;
    }

    private static bool IsFinite(NitroxVector3 position) =>
        !float.IsNaN(position.X) && !float.IsInfinity(position.X) &&
        !float.IsNaN(position.Y) && !float.IsInfinity(position.Y) &&
        !float.IsNaN(position.Z) && !float.IsInfinity(position.Z);
}
