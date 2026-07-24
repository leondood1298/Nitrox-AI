using System.Collections.Generic;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.Extensions;
using Nitrox.Model.Subnautica.Packets;
using NitroxClient.Communication.Abstract;
using NitroxClient.Extensions;
using NitroxClient.Helpers;
using NitroxClient.MonoBehaviours;

namespace NitroxClient.GameLogic;

public sealed class MapRoomScanResultBroadcaster(IPacketSender packetSender, ThrottledPacketSender throttledPacketSender, SimulationOwnership simulationOwnership)
{
    public void BroadcastSnapshot(MapRoomFunctionality mapRoom)
    {
        if (!TryGetAuthority(mapRoom, out NitroxId roomId, out long generation))
        {
            return;
        }
        List<Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Bases.MapRoomScanResultRecord> results = [];
        foreach (ResourceTrackerDatabase.ResourceInfo info in mapRoom.resourceNodes)
        {
            if (IsCurrentInRangeResult(mapRoom, info))
            {
                results.Add(new(info.uniqueId, info.techType.ToDto(), info.position.ToDto()));
            }
        }
        packetSender.Send(CreateSnapshotPacket(roomId, generation, results, mapRoom.wireFrameWorld.position, mapRoom.GetScanRange()));
    }

    public void BroadcastDiscovered(MapRoomFunctionality mapRoom, ResourceTrackerDatabase.ResourceInfo info)
    {
        if (!TryGetAuthority(mapRoom, out NitroxId roomId, out long generation))
        {
            return;
        }

        if (!HasValidResultIdentity(mapRoom, info))
        {
            MapRoomScanResults.EvictDiscoveredResource(mapRoom, info);
            return;
        }
        if (!IsWithinCurrentScanRange(mapRoom, info))
        {
            MapRoomScanResults.EvictDiscoveredResource(mapRoom, info);
            SendRangeExit(mapRoom, roomId, generation, info);
            return;
        }

        MapRoomScanResults.PreferLiveDiscoveredResource(mapRoom, info);
        Send(mapRoom, roomId, generation, info, removed: false);
    }

    public void BroadcastRemoved(MapRoomFunctionality mapRoom, ResourceTrackerDatabase.ResourceInfo info)
    {
        if (info != null && !string.IsNullOrEmpty(info.uniqueId) && info.techType == mapRoom.typeToScan &&
            TryGetAuthority(mapRoom, out NitroxId roomId, out long generation))
        {
            // Vanilla removes by reference. Remove any same-ID synthetic/duplicate left behind before
            // publishing the authoritative removal.
            MapRoomScanResults.EvictDiscoveredResource(mapRoom, info);
            Send(mapRoom, roomId, generation, info, removed: true);
        }
    }

    public void BroadcastMoved(MapRoomFunctionality mapRoom, ResourceTrackerDatabase.ResourceInfo info)
    {
        if (info == null || string.IsNullOrEmpty(info.uniqueId) || info.techType != mapRoom.typeToScan ||
            !TryGetAuthority(mapRoom, out NitroxId roomId, out long generation))
        {
            return;
        }
        bool removed = !IsWithinCurrentScanRange(mapRoom, info);
        if (removed)
        {
            SendRangeExit(mapRoom, roomId, generation, info);
        }
        else
        {
            MapRoomScanResultChanged packet = CreateChangedPacket(roomId, generation, info, removed: false,
                isRangeExit: false, mapRoom.wireFrameWorld.position, mapRoom.GetScanRange());
            throttledPacketSender.SendThrottled(packet, changed => (changed.MapRoomId, changed.ResourceId), 0.5f);
        }
    }

    public bool ShouldRunVanillaResults(MapRoomFunctionality mapRoom)
    {
        MapRoomNetworkState state = mapRoom ? mapRoom.GetComponent<MapRoomNetworkState>() : null;
        bool hasAuthority = TryGetAuthority(mapRoom, out _, out _);
        return ShouldRunVanillaResults(state && state.ResultStateInitialized, hasAuthority);
    }

    internal static bool ShouldRunVanillaResults(bool resultStateInitialized, bool hasAuthority) =>
        !resultStateInitialized || hasAuthority;

    private bool TryGetAuthority(MapRoomFunctionality mapRoom, out NitroxId roomId, out long generation)
    {
        generation = 0;
        if (!mapRoom || !mapRoom.TryGetNitroxId(out roomId) || !simulationOwnership.HasAnyLockType(roomId))
        {
            roomId = null;
            return false;
        }
        MapRoomNetworkState state = mapRoom.GetComponent<MapRoomNetworkState>();
        if (!state)
        {
            return false;
        }
        generation = state.Generation;
        return true;
    }

    private static bool IsCurrentInRangeResult(MapRoomFunctionality mapRoom, ResourceTrackerDatabase.ResourceInfo info)
    {
        return HasValidResultIdentity(mapRoom, info) && IsWithinCurrentScanRange(mapRoom, info);
    }

    private static bool HasValidResultIdentity(MapRoomFunctionality mapRoom, ResourceTrackerDatabase.ResourceInfo info) =>
        mapRoom && info != null && !string.IsNullOrEmpty(info.uniqueId) && info.techType == mapRoom.typeToScan;

    private static bool IsWithinCurrentScanRange(MapRoomFunctionality mapRoom, ResourceTrackerDatabase.ResourceInfo info) =>
        (mapRoom.wireFrameWorld.position - info.position).sqrMagnitude <= mapRoom.GetScanRange() * mapRoom.GetScanRange();

    private void SendRangeExit(MapRoomFunctionality mapRoom, NitroxId roomId, long generation,
        ResourceTrackerDatabase.ResourceInfo info)
    {
        MapRoomScanResultChanged packet = CreateChangedPacket(roomId, generation, info, removed: true,
            isRangeExit: true, mapRoom.wireFrameWorld.position, mapRoom.GetScanRange());
        throttledPacketSender.SendThrottled(packet, changed => (changed.MapRoomId, changed.ResourceId), 0.5f);
    }

    private void Send(MapRoomFunctionality mapRoom, NitroxId roomId, long generation, ResourceTrackerDatabase.ResourceInfo info, bool removed)
    {
        packetSender.Send(CreateChangedPacket(roomId, generation, info, removed, isRangeExit: false,
            mapRoom.wireFrameWorld.position, mapRoom.GetScanRange()));
    }

    internal static MapRoomScanResultChanged CreateChangedPacket(NitroxId roomId, long generation,
        ResourceTrackerDatabase.ResourceInfo info, bool removed, bool isRangeExit, UnityEngine.Vector3 scanOrigin, float scanRange) =>
        new(roomId, generation, info.uniqueId, info.techType.ToDto(), info.position.ToDto(), removed, isRangeExit,
            scanOrigin.ToDto(), scanRange);

    internal static MapRoomScanResultSnapshot CreateSnapshotPacket(NitroxId roomId, long generation,
        List<Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Bases.MapRoomScanResultRecord> results,
        UnityEngine.Vector3 scanOrigin, float scanRange) =>
        new(roomId, generation, results, scanOrigin.ToDto(), scanRange);
}
