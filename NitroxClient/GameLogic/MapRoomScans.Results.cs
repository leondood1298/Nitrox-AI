using System;
using System.Collections.Generic;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Bases;
using Nitrox.Model.Subnautica.Packets;
using UnityEngine;

namespace NitroxClient.GameLogic;

/// <summary>
///     What a room's scan has found: the list the game keeps on the room. On the simulating player's client the game
///     fills it and this class reports it; on every other client this class fills it with what the server sends.
/// </summary>
public sealed partial class MapRoomScans
{
    private const float RESULT_CHANGE_THROTTLE_SECONDS = 0.5f;
    private const float PICKUP_POSITION_TOLERANCE_SQUARED = 0.01f;

    /// <summary>
    ///     Whether the game may add to and remove from the room's list of results as resources load and unload in
    ///     this client's game.
    /// </summary>
    public bool ScansLocally(MapRoomFunctionality mapRoom) => !TryGetRoom(mapRoom, out IRoom? room) || ScansLocally(room);

    /// <summary>
    ///     Called after a resource was loaded in this client's game and the game had its chance to add it to the room's results.
    /// </summary>
    public void ResourceDiscovered(MapRoomFunctionality mapRoom, ResourceTrackerDatabase.ResourceInfo info)
    {
        if (TryGetRoom(mapRoom, out IRoom? room))
        {
            ResourceDiscovered(room, info);
        }
    }

    /// <summary>
    ///     Called after a resource was unloaded in this client's game and the game had its chance to remove it from the room's results.
    /// </summary>
    public void ResourceRemoved(MapRoomFunctionality mapRoom, ResourceTrackerDatabase.ResourceInfo info)
    {
        if (TryGetRoom(mapRoom, out IRoom? room))
        {
            ResourceRemoved(room, info);
        }
    }

    /// <summary>
    ///     Called after a resource that was already loaded in this client's game was registered at another position.
    /// </summary>
    public void ResourceMoved(ResourceTrackerDatabase.ResourceInfo info)
    {
        foreach (MapRoomFunctionality mapRoom in MapRoomFunctionality.mapRooms)
        {
            if (TryGetRoom(mapRoom, out IRoom? room))
            {
                ResourceMoved(room, info);
            }
        }
    }

    /// <summary>
    ///     Called after the game filled the room's list of results from this client's own game.
    /// </summary>
    public void ResultsObtained(MapRoomFunctionality mapRoom)
    {
        if (TryGetRoom(mapRoom, out IRoom? room))
        {
            ResultsObtained(room);
        }
    }

    /// <summary>
    ///     Takes a resource the local player picked up or broke out of everything on this client that shows scan
    ///     results. The server tells the other players, but not the one who did it.
    /// </summary>
    public void RemoveResource(NitroxId resourceId, TechType techType, Vector3 position)
    {
        string id = resourceId.ToString();
        foreach (MapRoomFunctionality mapRoom in MapRoomFunctionality.mapRooms)
        {
            if (TryGetRoom(mapRoom, out IRoom? room))
            {
                RemoveResource(room, id, techType, position);
            }
        }
        RemoveHudNode(id, techType, position);
    }

    /// <summary>
    ///     Empties the results of a room that is about to be deconstructed, so that nothing of it stays on the HUD.
    /// </summary>
    public void ClearResults(MapRoomFunctionality mapRoom)
    {
        mapRoom.resourceNodes.Clear();
        mapRoom.numNodesScanned = 0;
        ShowResults(mapRoom);
    }

    public void ApplySnapshot(MapRoomScanSnapshot snapshot)
    {
        if (TryGetRoom(snapshot.MapRoomId, out IRoom? room))
        {
            ApplySnapshot(room, snapshot);
        }
    }

    public void ApplyChange(MapRoomScanResultChanged change)
    {
        if (TryGetRoom(change.MapRoomId, out IRoom? room))
        {
            ApplyChange(room, change);
        }
    }

    internal bool ScansLocally(IRoom room) => !GetState(room.Id).ResultsReceived || simulationOwnership.HasAnyLockType(room.Id);

    /// <summary>
    ///     The simulating player's game has replaced the room's list, which so far held what the server had added. The
    ///     server is sent the new list and answers with the one everybody gets.
    /// </summary>
    internal void ResultsObtained(IRoom room)
    {
        if (simulationOwnership.HasAnyLockType(room.Id))
        {
            GetState(room.Id).ReportDue = true;
        }
    }

    internal void ResourceDiscovered(IRoom room, ResourceTrackerDatabase.ResourceInfo info)
    {
        if (!simulationOwnership.HasAnyLockType(room.Id))
        {
            return;
        }
        if (!IsOfTarget(room, info))
        {
            Evict(room, info);
            return;
        }
        if (!IsInRange(room, info))
        {
            // The game adds a matching resource within a fixed distance, whatever the room's range is.
            Evict(room, info);
            ReportResult(room, info, removed: true, isRangeExit: true, throttled: true);
            return;
        }

        // The entry the game just added is the one it will later remove by reference. One the server added under the
        // same id would stay behind.
        if (UpsertLiveDiscoveredResource(room.Nodes, info))
        {
            ResultsChanged(room);
        }
        ReportResult(room, info, removed: false, isRangeExit: false, throttled: false);
    }

    internal void ResourceRemoved(IRoom room, ResourceTrackerDatabase.ResourceInfo info)
    {
        if (info == null || !IsOfTarget(room, info) || !simulationOwnership.HasAnyLockType(room.Id))
        {
            return;
        }
        // The game removes by reference. An entry the server added under the same id has to go as well.
        Evict(room, info);
        ReportResult(room, info, removed: true, isRangeExit: false, throttled: false);
    }

    internal void ResourceMoved(IRoom room, ResourceTrackerDatabase.ResourceInfo info)
    {
        if (info == null || !IsOfTarget(room, info) || !simulationOwnership.HasAnyLockType(room.Id))
        {
            return;
        }
        bool leftRange = !IsInRange(room, info);
        ReportResult(room, info, leftRange, leftRange, throttled: true);
    }

    internal void ApplySnapshot(IRoom room, MapRoomScanSnapshot snapshot)
    {
        RoomState state = GetState(room.Id);
        // After a server start nobody has reported the types yet. Until then every client lists what its own game finds.
        if (snapshot.AvailableTypes.Count > 0 || state.TypesReceived)
        {
            SetAvailableTypes(room, state, snapshot.AvailableTypes);
        }
        if (snapshot.Generation != state.Generation)
        {
            return;
        }
        ReconcileSnapshot(room.Nodes, snapshot.Results);
        state.ResultsReceived = true;
        ResultsChanged(room);
    }

    internal void ApplyChange(IRoom room, MapRoomScanResultChanged change)
    {
        RoomState state = GetState(room.Id);
        if (change.Generation != state.Generation)
        {
            return;
        }
        ApplyChangeToList(room.Nodes, change);
        state.ResultsReceived = true;
        ResultsChanged(room);
    }

    internal static void RemoveResource(IRoom room, string resourceId, TechType techType, Vector3 position)
    {
        if (RemoveFromList(room.Nodes, resourceId, techType, position))
        {
            ResultsChanged(room);
        }
    }

    internal static void ReconcileSnapshot(List<ResourceTrackerDatabase.ResourceInfo> target, IEnumerable<MapRoomScanResultRecord> results)
    {
        target.Clear();
        foreach (MapRoomScanResultRecord result in results)
        {
            target.Add(ToResourceInfo(result));
        }
    }

    internal static void ApplyChangeToList(List<ResourceTrackerDatabase.ResourceInfo> target, MapRoomScanResultChanged change)
    {
        MapRoomScanResultRecord result = change.Result;
        if (change.Removed)
        {
            RemoveFromList(target, result.ResourceId);
            return;
        }
        int index = target.FindIndex(info => info.uniqueId == result.ResourceId);
        if (index < 0)
        {
            target.Add(ToResourceInfo(result));
            return;
        }

        // The entry is changed, not replaced: on the simulating player's client it is the one the game added and
        // will later remove by reference. A copy in its place would stay behind as a ghost when its cell unloads.
        ResourceTrackerDatabase.ResourceInfo existing = target[index];
        existing.techType = result.TechType.ToUnity();
        existing.position = result.Position.ToUnity();
        for (int duplicateIndex = target.Count - 1; duplicateIndex > index; duplicateIndex--)
        {
            if (target[duplicateIndex].uniqueId == result.ResourceId)
            {
                target.RemoveAt(duplicateIndex);
            }
        }
    }

    /// <summary>
    ///     Makes the entry the game just added for a newly loaded resource the only one with its id.
    /// </summary>
    internal static bool UpsertLiveDiscoveredResource(List<ResourceTrackerDatabase.ResourceInfo> target, ResourceTrackerDatabase.ResourceInfo liveInfo)
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
        target.Insert(Math.Min(firstMatchIndex, target.Count), liveInfo);
        return true;
    }

    internal static bool EvictDiscoveredResourceFromList(List<ResourceTrackerDatabase.ResourceInfo> target, ResourceTrackerDatabase.ResourceInfo info)
    {
        if (target == null || info == null)
        {
            return false;
        }
        return string.IsNullOrEmpty(info.uniqueId)
            ? target.RemoveAll(candidate => ReferenceEquals(candidate, info)) > 0
            : RemoveFromList(target, info.uniqueId);
    }

    internal static bool RemoveFromList(List<ResourceTrackerDatabase.ResourceInfo> target, string resourceId) => RemoveFromList(target, resourceId, TechType.None, null);

    internal static bool RemoveFromSet(HashSet<ResourceTrackerDatabase.ResourceInfo> target, string resourceId) => RemoveFromSet(target, resourceId, TechType.None, null);

    internal static bool RemoveFromList(List<ResourceTrackerDatabase.ResourceInfo> target, string resourceId, TechType techType, Vector3? pickupPosition) =>
        target.RemoveAll(info => MatchesPickup(info, resourceId, techType, pickupPosition)) > 0;

    internal static bool RemoveFromSet(HashSet<ResourceTrackerDatabase.ResourceInfo> target, string resourceId, TechType techType, Vector3? pickupPosition) =>
        target.RemoveWhere(info => MatchesPickup(info, resourceId, techType, pickupPosition)) > 0;

    /// <summary>
    ///     A picked up resource is found by its id, or, because the id of a resource can change when it is picked up,
    ///     by its type at the place it was picked up from.
    /// </summary>
    internal static bool MatchesPickup(ResourceTrackerDatabase.ResourceInfo info, string resourceId, TechType techType, Vector3? pickupPosition) =>
        info.uniqueId == resourceId || pickupPosition.HasValue && techType != TechType.None && info.techType == techType &&
        (info.position - pickupPosition.Value).sqrMagnitude <= PICKUP_POSITION_TOLERANCE_SQUARED;

    private void ReportResults(IRoom room, RoomState state)
    {
        List<MapRoomScanResultRecord> results = [];
        foreach (ResourceTrackerDatabase.ResourceInfo info in room.Nodes)
        {
            if (IsOfTarget(room, info) && IsInRange(room, info))
            {
                results.Add(new MapRoomScanResultRecord(info.uniqueId, info.techType.ToDto(), info.position.ToDto()));
            }
        }
        packetSender.Send(new MapRoomScanReport(room.Id, state.Generation, room.ScanOrigin.ToDto(), room.ScanRange, results));
    }

    private void ReportResult(IRoom room, ResourceTrackerDatabase.ResourceInfo info, bool removed, bool isRangeExit, bool throttled)
    {
        RoomState state = GetState(room.Id);
        if (room.Target != state.Target)
        {
            // The server has not confirmed this target yet. The full list is reported once it has.
            return;
        }
        MapRoomScanResultReport report = new(room.Id, state.Generation, new MapRoomScanResultRecord(info.uniqueId, info.techType.ToDto(), info.position.ToDto()), removed,
            isRangeExit, room.ScanOrigin.ToDto(), room.ScanRange);
        if (throttled)
        {
            throttledPacketSender.SendThrottled(report, sent => (sent.MapRoomId, sent.Result.ResourceId), RESULT_CHANGE_THROTTLE_SECONDS);
        }
        else
        {
            packetSender.Send(report);
        }
    }

    private static void Evict(IRoom room, ResourceTrackerDatabase.ResourceInfo info)
    {
        if (EvictDiscoveredResourceFromList(room.Nodes, info))
        {
            ResultsChanged(room);
        }
    }

    /// <summary>
    ///     The scan cannot have revealed more than there is to find.
    /// </summary>
    private static void ResultsChanged(IRoom room)
    {
        room.NodesScanned = Math.Min(room.NodesScanned, room.Nodes.Count);
        room.ShowResults();
    }

    private static bool IsOfTarget(IRoom room, ResourceTrackerDatabase.ResourceInfo info) =>
        info != null && !string.IsNullOrEmpty(info.uniqueId) && info.techType == room.Target;

    private static bool IsInRange(IRoom room, ResourceTrackerDatabase.ResourceInfo info) =>
        (room.ScanOrigin - info.position).sqrMagnitude <= room.ScanRange * room.ScanRange;

    private static ResourceTrackerDatabase.ResourceInfo ToResourceInfo(MapRoomScanResultRecord result) => new()
    {
        uniqueId = result.ResourceId,
        techType = result.TechType.ToUnity(),
        position = result.Position.ToUnity()
    };
}
