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
        packetSender.Send(new MapRoomScanResultSnapshot(roomId, generation, results));
    }

    public void BroadcastDiscovered(MapRoomFunctionality mapRoom, ResourceTrackerDatabase.ResourceInfo info)
    {
        if (TryGetAuthority(mapRoom, out NitroxId roomId, out long generation) && IsCurrentInRangeResult(mapRoom, info))
        {
            Send(roomId, generation, info, removed: false);
        }
    }

    public void BroadcastRemoved(MapRoomFunctionality mapRoom, ResourceTrackerDatabase.ResourceInfo info)
    {
        if (info != null && info.techType == mapRoom.typeToScan && TryGetAuthority(mapRoom, out NitroxId roomId, out long generation))
        {
            Send(roomId, generation, info, removed: true);
        }
    }

    public void BroadcastMoved(MapRoomFunctionality mapRoom, ResourceTrackerDatabase.ResourceInfo info)
    {
        if (info == null || info.techType != mapRoom.typeToScan || !TryGetAuthority(mapRoom, out NitroxId roomId, out long generation))
        {
            return;
        }
        bool removed = !IsCurrentInRangeResult(mapRoom, info);
        MapRoomScanResultChanged packet = new(roomId, generation, info.uniqueId, info.techType.ToDto(), info.position.ToDto(), removed);
        throttledPacketSender.SendThrottled(packet, changed => (changed.MapRoomId, changed.ResourceId), 0.5f);
    }

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
        return info != null && !string.IsNullOrEmpty(info.uniqueId) && info.techType == mapRoom.typeToScan && (mapRoom.wireFrameWorld.position - info.position).sqrMagnitude <= mapRoom.GetScanRange() * mapRoom.GetScanRange();
    }

    private void Send(NitroxId roomId, long generation, ResourceTrackerDatabase.ResourceInfo info, bool removed)
    {
        packetSender.Send(new MapRoomScanResultChanged(roomId, generation, info.uniqueId, info.techType.ToDto(), info.position.ToDto(), removed));
    }
}
