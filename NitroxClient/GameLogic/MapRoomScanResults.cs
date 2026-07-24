using System.Collections.Generic;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Bases;
using Nitrox.Model.Subnautica.Extensions;
using Nitrox.Model.Subnautica.Packets;
using NitroxClient.Extensions;
using NitroxClient.MonoBehaviours;
using UnityEngine;

namespace NitroxClient.GameLogic;

public static class MapRoomScanResults
{
    private const float PICKUP_POSITION_TOLERANCE_SQUARED = 0.01f;

    public static void RemoveLocalResource(NitroxId resourceId)
    {
        RemoveLocalResource(resourceId, TechType.None, null);
    }

    public static void RemoveLocalResource(NitroxId resourceId, TechType techType, Vector3 pickupPosition)
    {
        RemoveLocalResource(resourceId, techType, (Vector3?)pickupPosition);
    }

    private static void RemoveLocalResource(NitroxId resourceId, TechType techType, Vector3? pickupPosition)
    {
        string id = resourceId.ToString();
        foreach (MapRoomFunctionality mapRoom in MapRoomFunctionality.mapRooms)
        {
            if (mapRoom && RemoveFromList(mapRoom.resourceNodes, id, techType, pickupPosition))
            {
                mapRoom.numNodesScanned = System.Math.Min(mapRoom.numNodesScanned, mapRoom.resourceNodes.Count);
                RefreshResultConsumers(mapRoom);
            }
        }
        RemoveHudNode(id, techType, pickupPosition);
    }

    public static void Cleanup(MapRoomFunctionality mapRoom)
    {
        mapRoom.resourceNodes.Clear();
        mapRoom.numNodesScanned = 0;
        foreach (GameObject blip in mapRoom.mapBlips)
        {
            if (blip)
            {
                blip.SetActive(false);
            }
        }
        uGUI_ResourceTracker resourceTracker = Object.FindObjectOfType<uGUI_ResourceTracker>();
        if (resourceTracker)
        {
            resourceTracker.gatherNextTick = true;
        }
    }

    public static void ApplySnapshot(MapRoomFunctionality mapRoom, long generation, long revision, IEnumerable<MapRoomScanResultRecord> results)
    {
        MapRoomNetworkState state = mapRoom.gameObject.EnsureComponent<MapRoomNetworkState>();
        if (generation != state.Generation || generation < state.ResultGeneration || (generation == state.ResultGeneration && revision < state.ResultRevision))
        {
            return;
        }
        ReconcileSnapshot(mapRoom.resourceNodes, results);
        state.ResultGeneration = generation;
        state.ResultRevision = revision;
        state.ResultStateInitialized = true;
        mapRoom.numNodesScanned = System.Math.Min(mapRoom.numNodesScanned, mapRoom.resourceNodes.Count);
        RefreshResultConsumers(mapRoom);
    }

    public static void ProcessDelta(MapRoomScanResultChanged packet)
    {
        if (!packet.IsServerResponse || !packet.Granted || !NitroxEntity.TryGetObjectFrom(packet.MapRoomId, out GameObject gameObject) || !gameObject.TryGetComponent(out MapRoomFunctionality mapRoom))
        {
            return;
        }
        MapRoomNetworkState state = gameObject.EnsureComponent<MapRoomNetworkState>();
        if (packet.Generation != state.Generation || packet.Generation < state.ResultGeneration || (packet.Generation == state.ResultGeneration && packet.Revision <= state.ResultRevision))
        {
            return;
        }
        if (packet.Generation > state.ResultGeneration)
        {
            mapRoom.resourceNodes.Clear();
            state.ResultGeneration = packet.Generation;
        }
        ApplyDeltaToList(mapRoom.resourceNodes, packet);
        state.ResultRevision = packet.Revision;
        state.ResultStateInitialized = true;
        mapRoom.numNodesScanned = System.Math.Min(mapRoom.numNodesScanned, mapRoom.resourceNodes.Count);
        RefreshResultConsumers(mapRoom);
    }

    public static void ProcessSnapshot(MapRoomScanResultSnapshot packet)
    {
        if (!packet.IsServerResponse || !packet.Granted || !NitroxEntity.TryGetObjectFrom(packet.MapRoomId, out GameObject gameObject) || !gameObject.TryGetComponent(out MapRoomFunctionality mapRoom))
        {
            return;
        }
        ApplySnapshot(mapRoom, packet.Generation, packet.Revision, packet.Results);
    }

    internal static void ReconcileSnapshot(List<ResourceTrackerDatabase.ResourceInfo> target, IEnumerable<MapRoomScanResultRecord> results)
    {
        target.Clear();
        foreach (MapRoomScanResultRecord result in results)
        {
            target.Add(ToResourceInfo(result.ResourceId, result.TechType, result.Position.ToUnity()));
        }
    }

    internal static void ApplyDeltaToList(List<ResourceTrackerDatabase.ResourceInfo> target, MapRoomScanResultChanged packet)
    {
        if (packet.Removed)
        {
            RemoveFromList(target, packet.ResourceId);
            return;
        }
        int index = target.FindIndex(info => info.uniqueId == packet.ResourceId);
        if (index >= 0)
        {
            // Preserve a streamed live ResourceInfo reference when the server echoes the canonical position/type.
            // Vanilla removal later uses object identity, so replacing the live object with a synthetic copy would
            // leave a ghost node when its cell unloads.
            ResourceTrackerDatabase.ResourceInfo existing = target[index];
            existing.uniqueId = packet.ResourceId;
            existing.techType = packet.TechType.ToUnity();
            existing.position = packet.Position.ToUnity();
            for (int duplicateIndex = target.Count - 1; duplicateIndex > index; duplicateIndex--)
            {
                if (target[duplicateIndex].uniqueId == packet.ResourceId)
                {
                    target.RemoveAt(duplicateIndex);
                }
            }
        }
        else
        {
            target.Add(ToResourceInfo(packet.ResourceId, packet.TechType, packet.Position.ToUnity()));
        }
    }

    /// <summary>
    ///     Runs after vanilla appended a newly streamed live resource. Replace any synthetic or duplicate
    ///     stable-ID entries with that exact live object so vanilla's later identity-based removal still works.
    /// </summary>
    public static void PreferLiveDiscoveredResource(MapRoomFunctionality mapRoom,
        ResourceTrackerDatabase.ResourceInfo liveInfo)
    {
        if (!mapRoom || !UpsertLiveDiscoveredResource(mapRoom.resourceNodes, liveInfo))
        {
            return;
        }
        mapRoom.numNodesScanned = System.Math.Min(mapRoom.numNodesScanned, mapRoom.resourceNodes.Count);
        RefreshResultConsumers(mapRoom);
    }

    public static bool EvictDiscoveredResource(MapRoomFunctionality mapRoom,
        ResourceTrackerDatabase.ResourceInfo info)
    {
        if (!mapRoom || !EvictDiscoveredResourceFromList(mapRoom.resourceNodes, info))
        {
            return false;
        }
        mapRoom.numNodesScanned = System.Math.Min(mapRoom.numNodesScanned, mapRoom.resourceNodes.Count);
        RefreshResultConsumers(mapRoom);
        return true;
    }

    internal static bool UpsertLiveDiscoveredResource(List<ResourceTrackerDatabase.ResourceInfo> target,
        ResourceTrackerDatabase.ResourceInfo liveInfo)
    {
        if (target == null || liveInfo == null || string.IsNullOrEmpty(liveInfo.uniqueId))
        {
            return false;
        }

        int firstMatchIndex = target.FindIndex(info => info != null && info.uniqueId == liveInfo.uniqueId);
        if (firstMatchIndex < 0)
        {
            target.Add(liveInfo);
            return true;
        }

        int matchCount = 0;
        bool onlyMatchIsLive = false;
        foreach (ResourceTrackerDatabase.ResourceInfo info in target)
        {
            if (info != null && info.uniqueId == liveInfo.uniqueId)
            {
                matchCount++;
                onlyMatchIsLive = ReferenceEquals(info, liveInfo);
            }
        }
        if (matchCount == 1 && onlyMatchIsLive)
        {
            return false;
        }

        target.RemoveAll(info => info != null && info.uniqueId == liveInfo.uniqueId);
        target.Insert(System.Math.Min(firstMatchIndex, target.Count), liveInfo);
        return true;
    }

    internal static bool EvictDiscoveredResourceFromList(List<ResourceTrackerDatabase.ResourceInfo> target,
        ResourceTrackerDatabase.ResourceInfo info)
    {
        if (target == null || info == null)
        {
            return false;
        }
        return string.IsNullOrEmpty(info.uniqueId)
            ? target.RemoveAll(candidate => ReferenceEquals(candidate, info)) > 0
            : RemoveFromList(target, info.uniqueId);
    }

    internal static bool RemoveFromList(List<ResourceTrackerDatabase.ResourceInfo> target, string resourceId)
    {
        return RemoveFromList(target, resourceId, TechType.None, null);
    }

    internal static bool RemoveFromSet(HashSet<ResourceTrackerDatabase.ResourceInfo> target, string resourceId) =>
        RemoveFromSet(target, resourceId, TechType.None, null);

    internal static bool RemoveFromList(List<ResourceTrackerDatabase.ResourceInfo> target, string resourceId, TechType techType, Vector3? pickupPosition) =>
        target.RemoveAll(info => MatchesPickup(info, resourceId, techType, pickupPosition)) > 0;

    internal static bool RemoveFromSet(HashSet<ResourceTrackerDatabase.ResourceInfo> target, string resourceId, TechType techType, Vector3? pickupPosition) =>
        target.RemoveWhere(info => MatchesPickup(info, resourceId, techType, pickupPosition)) > 0;

    internal static bool MatchesPickup(ResourceTrackerDatabase.ResourceInfo info, string resourceId, TechType techType, Vector3? pickupPosition) =>
        info.uniqueId == resourceId || pickupPosition.HasValue && techType != TechType.None && info.techType == techType &&
        (info.position - pickupPosition.Value).sqrMagnitude <= PICKUP_POSITION_TOLERANCE_SQUARED;

    internal static void RefreshResultConsumers(MapRoomFunctionality mapRoom)
    {
        RefreshHologramBlips(mapRoom);

        // Vanilla only gathers Scanner HUD nodes every ten seconds (or after a local database removal).
        // Schedule the same gather on the next HUD tick so canonical removals cannot leave ghost markers.
        uGUI_ResourceTracker resourceTracker = Object.FindObjectOfType<uGUI_ResourceTracker>();
        if (resourceTracker)
        {
            resourceTracker.gatherNextTick = true;
        }
    }

    private static void RemoveHudNode(string resourceId, TechType techType, Vector3? pickupPosition)
    {
        uGUI_ResourceTracker resourceTracker = Object.FindObjectOfType<uGUI_ResourceTracker>();
        if (!resourceTracker)
        {
            return;
        }
        RemoveFromSet(resourceTracker.nodes, resourceId, techType, pickupPosition);
        resourceTracker.gatherNextTick = true;
        resourceTracker.UpdateBlips();
    }

    internal static bool ShouldRepublishProgress(long previousGeneration, long acceptedGeneration, bool targetAlreadySelected,
        int localProgress, int acceptedProgress, bool hasOwnership) =>
        hasOwnership && targetAlreadySelected && acceptedGeneration > previousGeneration && localProgress > acceptedProgress;

    private static void RefreshHologramBlips(MapRoomFunctionality mapRoom)
    {
        if (!mapRoom.mapBlipRoot)
        {
            return;
        }

        int visibleCount = System.Math.Min(mapRoom.numNodesScanned, mapRoom.resourceNodes.Count);
        Vector3 origin = mapRoom.mapBlipRoot.transform.position;
        for (int i = 0; i < visibleCount; i++)
        {
            Vector3 localPosition = (mapRoom.resourceNodes[i].position - origin) * mapRoom.mapScale;
            if (i >= mapRoom.mapBlips.Count)
            {
                GameObject blip = Object.Instantiate(mapRoom.blipPrefab);
                blip.transform.SetParent(mapRoom.mapBlipRoot.transform, false);
                mapRoom.mapBlips.Add(blip);
            }
            mapRoom.mapBlips[i].transform.localPosition = localPosition;
            mapRoom.mapBlips[i].SetActive(true);
        }
        for (int i = visibleCount; i < mapRoom.mapBlips.Count; i++)
        {
            mapRoom.mapBlips[i].SetActive(false);
        }
    }

    private static ResourceTrackerDatabase.ResourceInfo ToResourceInfo(string resourceId, Nitrox.Model.Subnautica.DataStructures.GameLogic.NitroxTechType techType, Vector3 position) => new()
    {
        uniqueId = resourceId,
        techType = techType.ToUnity(),
        position = position
    };
}
