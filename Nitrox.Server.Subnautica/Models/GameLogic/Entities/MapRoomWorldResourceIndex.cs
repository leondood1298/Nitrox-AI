using System;
using System.Collections.Generic;
using System.Linq;
using Nitrox.Model.DataStructures;
using Nitrox.Model.DataStructures.Unity;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Bases;

namespace Nitrox.Server.Subnautica.Models.GameLogic.Entities;

internal static class MapRoomWorldResourceIndex
{
    internal const float DEFAULT_SCAN_RANGE = 300f;
    internal const float SCAN_RANGE_PER_MODULE = 50f;
    internal const float MAX_SCAN_RANGE = 500f;
    // MapRoomEntity stores a base-grid cell, not an exact server world transform. Anchor to the base root and allow a
    // deliberately broad 512 m extent so legitimate large bases work while arbitrary remote-world queries do not.
    internal const float MAX_ORIGIN_DISTANCE_FROM_BASE = 512f;
    private const float MIN_SCAN_RANGE_TOLERANCE = 0.01f;
    private const int MAX_RANGE_UPGRADE_COUNT = 4;
    private static readonly NitroxTechType rangeUpgradeTechType = new("MapRoomUpgradeScanRange");

    public static bool TryResolveScanAnchor(EntityRegistry entityRegistry, MapRoomEntity room, out NitroxVector3 scanAnchor)
    {
        scanAnchor = NitroxVector3.Zero;
        if (room.ParentId == null || !entityRegistry.TryGetEntityById(room.ParentId, out WorldEntity parent) || parent.Transform == null)
        {
            return false;
        }

        scanAnchor = parent.Transform.Position;
        return IsFinite(scanAnchor);
    }

    public static bool TryNormalizeQuery(NitroxVector3 requestedOrigin, float requestedRange, NitroxVector3 scanAnchor,
        out NitroxVector3 scanOrigin, out float scanRange)
    {
        scanOrigin = NitroxVector3.Zero;
        scanRange = 0f;
        if (!IsFinite(requestedOrigin) || !IsFinite(scanAnchor) || float.IsNaN(requestedRange) || float.IsInfinity(requestedRange))
        {
            return false;
        }

        // A zero origin is possible during the first UI update. Anchoring that one query to the base root is safe and
        // close enough to initialize the UI; subsequent publishes carry the wire-frame's exact world position.
        scanOrigin = requestedRange == 0f && requestedOrigin.Equals(NitroxVector3.Zero) ? scanAnchor : requestedOrigin;
        if (!IsWithinRange(scanAnchor, scanOrigin, MAX_ORIGIN_DISTANCE_FROM_BASE))
        {
            return false;
        }

        // The scanner UI can publish once before vanilla has initialized its range field.
        if (requestedRange == 0f)
        {
            scanRange = DEFAULT_SCAN_RANGE;
            return true;
        }

        if (requestedRange < DEFAULT_SCAN_RANGE - MIN_SCAN_RANGE_TOLERANCE || requestedRange > MAX_SCAN_RANGE)
        {
            return false;
        }

        scanRange = Math.Max(DEFAULT_SCAN_RANGE, requestedRange);
        return true;
    }

    public static float GetMaximumScanRange(MapRoomEntity room) =>
        Math.Min(MAX_SCAN_RANGE, DEFAULT_SCAN_RANGE + CountRangeUpgrades(room.ChildEntities) * SCAN_RANGE_PER_MODULE);

    public static bool IsRangeAllowed(MapRoomEntity room, float scanRange) =>
        scanRange <= GetMaximumScanRange(room) + MIN_SCAN_RANGE_TOLERANCE;

    public static List<MapRoomScanResultRecord> MergeResults(IEnumerable<MapRoomScanResultRecord> ownerResults,
        IEnumerable<WorldEntity> worldEntities, NitroxTechType target, NitroxVector3 scanOrigin, float scanRange, int maxResults)
    {
        Dictionary<string, MapRoomScanResultRecord> byId = new(StringComparer.Ordinal);
        HashSet<string> ownerIds = new(StringComparer.Ordinal);
        foreach (MapRoomScanResultRecord result in ownerResults)
        {
            ownerIds.Add(result.ResourceId);
            byId[result.ResourceId] = new MapRoomScanResultRecord(result.ResourceId, result.TechType, result.Position);
        }

        if (target == null || target.Equals(NitroxTechType.None))
        {
            return [];
        }

        foreach (WorldEntity entity in worldEntities)
        {
            if (entity?.Id == null || entity.TechType == null || !entity.TechType.Equals(target))
            {
                continue;
            }

            string id = entity.Id.ToString();
            ownerIds.Remove(id);
            if (entity.Transform == null || !IsFinite(entity.Transform.Position) || !IsWithinRange(scanOrigin, entity.Transform.Position, scanRange))
            {
                // An exact target entity is server-canonical even when it is outside this query. Do not retain a
                // client's spoofed/stale in-range position for the same ID.
                byId.Remove(id);
                continue;
            }

            byId[id] = new MapRoomScanResultRecord(id, target, entity.Transform.Position);
        }

        return OrderAndCap(byId.Values, ownerIds, scanOrigin, maxResults);
    }

    public static List<NitroxTechType> MergeScanTypes(IEnumerable<NitroxTechType> ownerTypes,
        IEnumerable<NitroxTechType> detectableTypes, IEnumerable<WorldEntity> worldEntities, NitroxVector3 scanOrigin, float scanRange)
    {
        HashSet<NitroxTechType> merged = new(ownerTypes);
        HashSet<NitroxTechType> detectable = new(detectableTypes);
        foreach (WorldEntity entity in worldEntities)
        {
            if (entity?.TechType == null || entity.TechType.Equals(NitroxTechType.None) || !detectable.Contains(entity.TechType) || entity.Transform == null)
            {
                continue;
            }

            NitroxVector3 position = entity.Transform.Position;
            if (IsFinite(position) && IsWithinRange(scanOrigin, position, scanRange))
            {
                merged.Add(entity.TechType);
            }
        }

        return merged.OrderBy(type => type.ToString(), StringComparer.Ordinal).ToList();
    }

    internal static bool IsWithinRange(NitroxVector3 origin, NitroxVector3 position, float range) =>
        DistanceSquared(origin, position) <= (double)range * range;

    internal static bool IsFinite(NitroxVector3 position) =>
        !float.IsNaN(position.X) && !float.IsInfinity(position.X) &&
        !float.IsNaN(position.Y) && !float.IsInfinity(position.Y) &&
        !float.IsNaN(position.Z) && !float.IsInfinity(position.Z);

    private static double DistanceSquared(NitroxVector3 origin, NitroxVector3 position)
    {
        double x = position.X - origin.X;
        double y = position.Y - origin.Y;
        double z = position.Z - origin.Z;
        return x * x + y * y + z * z;
    }

    private static int CountRangeUpgrades(IEnumerable<Entity> entities)
    {
        if (entities == null)
        {
            return 0;
        }

        Stack<Entity> pending = new();
        foreach (Entity entity in entities)
        {
            if (entity != null)
            {
                pending.Push(entity);
            }
        }

        HashSet<Entity> visitedReferences = new(ReferenceEqualityComparer.Instance);
        HashSet<NitroxId> visitedIds = [];
        int count = 0;
        while (pending.Count > 0)
        {
            Entity entity = pending.Pop();
            if (!visitedReferences.Add(entity) || entity.Id != null && !visitedIds.Add(entity.Id))
            {
                continue;
            }
            if (entity?.TechType != null && entity.TechType.Equals(rangeUpgradeTechType))
            {
                count++;
                if (count >= MAX_RANGE_UPGRADE_COUNT)
                {
                    return MAX_RANGE_UPGRADE_COUNT;
                }
            }
            if (entity.ChildEntities == null)
            {
                continue;
            }
            foreach (Entity child in entity.ChildEntities)
            {
                if (child != null)
                {
                    pending.Push(child);
                }
            }
        }
        return count;
    }

    private static List<MapRoomScanResultRecord> OrderAndCap(IEnumerable<MapRoomScanResultRecord> results, ISet<string> ownerIds,
        NitroxVector3 scanOrigin, int maxResults)
    {
        List<MapRoomScanResultRecord> ordered = results.OrderBy(result => DistanceSquared(scanOrigin, result.Position))
                                                       .ThenBy(result => result.ResourceId, StringComparer.Ordinal)
                                                       .ToList();
        if (ordered.Count <= maxResults)
        {
            return ordered;
        }

        HashSet<string> selectedIds = ordered.Where(result => ownerIds.Contains(result.ResourceId))
                                             .Take(maxResults)
                                             .Select(result => result.ResourceId)
                                             .ToHashSet(StringComparer.Ordinal);
        int remaining = maxResults - selectedIds.Count;
        selectedIds.UnionWith(ordered.Where(result => !ownerIds.Contains(result.ResourceId))
                                     .Take(remaining)
                                     .Select(result => result.ResourceId));
        return ordered.Where(result => selectedIds.Contains(result.ResourceId)).ToList();
    }
}
