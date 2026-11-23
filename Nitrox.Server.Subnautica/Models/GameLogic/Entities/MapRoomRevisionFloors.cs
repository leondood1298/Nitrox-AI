using System.Collections.Concurrent;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Bases;

namespace Nitrox.Server.Subnautica.Models.GameLogic.Entities;

/// <summary>
///     Remembers the last revisions that removed Scanner Rooms and cameras used, so the same id never starts over.
///     A room that is deconstructed and built again keeps its id, and the ids of its two default cameras are derived
///     from it. Clients keep the highest docking, light and component revision they saw per id for as long as they are
///     connected and discard anything lower, so a new room or camera record starting at zero would be ignored by every
///     player who saw the old one. Scan state needs no floor: clients keep it on the room object, which is rebuilt too.
/// </summary>
/// <remarks>
///     Kept in memory only. After a restart every client reconnects with nothing remembered.
/// </remarks>
internal sealed class MapRoomRevisionFloors
{
    private readonly ConcurrentDictionary<NitroxId, long> dockingRevisionsByRoomId = new();
    private readonly ConcurrentDictionary<NitroxId, CameraRevisions> revisionsByCameraId = new();

    /// <summary>
    ///     Called when a room is removed.
    /// </summary>
    public void Remember(MapRoomEntity removedRoom)
    {
        long used;
        lock (removedRoom)
        {
            used = removedRoom.DockingRevision;
        }
        dockingRevisionsByRoomId.AddOrUpdate(removedRoom.Id, used, (_, known) => Math.Max(known, used));
    }

    /// <summary>
    ///     Called when a camera's record is removed for good: the camera was destroyed, alone or with its room.
    /// </summary>
    public void Remember(MapRoomCameraRecord removedRecord)
    {
        CameraRevisions used;
        lock (removedRecord)
        {
            used = new CameraRevisions(removedRecord.LightRevision, removedRecord.ComponentRevision);
        }
        revisionsByCameraId.AddOrUpdate(removedRecord.CameraId, used,
            (_, known) => new CameraRevisions(Math.Max(known.Light, used.Light), Math.Max(known.Component, used.Component)));
    }

    /// <summary>
    ///     Called before a newly built room becomes reachable. Its docking revision starts above the one its id used.
    /// </summary>
    public void RaiseAbove(MapRoomEntity newRoom)
    {
        if (!dockingRevisionsByRoomId.TryGetValue(newRoom.Id, out long used))
        {
            return;
        }
        lock (newRoom)
        {
            newRoom.DockingRevision = Math.Max(newRoom.DockingRevision, used + 1);
        }
    }

    /// <summary>
    ///     Called when a camera gets a new record. Its light and component revisions start above the ones its id used.
    /// </summary>
    public void RaiseAbove(MapRoomCameraRecord newRecord)
    {
        if (!revisionsByCameraId.TryGetValue(newRecord.CameraId, out CameraRevisions used))
        {
            return;
        }
        lock (newRecord)
        {
            newRecord.LightRevision = Math.Max(newRecord.LightRevision, used.Light + 1);
            newRecord.ComponentRevision = Math.Max(newRecord.ComponentRevision, used.Component + 1);
        }
    }

    private readonly record struct CameraRevisions(long Light, long Component);
}
