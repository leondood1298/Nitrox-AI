using System.Collections.Generic;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Bases;
using Nitrox.Model.Subnautica.Extensions;
using Nitrox.Model.Subnautica.Packets;
using NitroxClient.Extensions;
using NitroxClient.MonoBehaviours;
using UnityEngine;

namespace NitroxClient.GameLogic;

internal static class MapRoomScanResults
{
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

    private static ResourceTrackerDatabase.ResourceInfo ToResourceInfo(string resourceId, Nitrox.Model.Subnautica.DataStructures.GameLogic.NitroxTechType techType, Vector3 position) => new()
    {
        uniqueId = resourceId,
        techType = techType.ToUnity(),
        position = position
    };
}
