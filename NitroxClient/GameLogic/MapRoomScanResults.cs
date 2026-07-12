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
        int index = target.FindIndex(info => info.uniqueId == packet.ResourceId);
        if (packet.Removed)
        {
            if (index >= 0)
            {
                target.RemoveAt(index);
            }
            return;
        }
        ResourceTrackerDatabase.ResourceInfo updated = ToResourceInfo(packet.ResourceId, packet.TechType, packet.Position.ToUnity());
        if (index >= 0)
        {
            target[index] = updated;
        }
        else
        {
            target.Add(updated);
        }
    }

    private static void RefreshResultConsumers(MapRoomFunctionality mapRoom)
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
