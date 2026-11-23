using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Bases;
using Nitrox.Server.Subnautica.Models.AppEvents;
using Nitrox.Server.Subnautica.Models.GameLogic.Entities;
using Nitrox.Server.Subnautica.Models.Packets.Core;

namespace Nitrox.Server.Subnautica.Models.GameLogic;

/// <summary>
///     Everything the server knows and decides about Scanner Rooms and their cameras: which camera sits in which dock,
///     who controls a camera, the light, battery and health of every camera, a room's scan target, scan results and
///     available scan types, and who is subscribed to its results. Packet processors and the other services hand a
///     request to one method here; it validates, changes the state and sends the answer. Scan results, available
///     types and subscriptions are not part of any entity: they are neither saved nor sent to a joining player.
/// </summary>
/// <remarks>
///     <para>
///         One gate guards all of it. What the gate puts in order:
///     </para>
///     <list type="bullet">
///         <item>
///             Every read and every change of Scanner Room state: the Scanner Room data of a <see cref="MapRoomEntity" />
///             and its camera records, the state a camera without a record keeps on its own entity, and the private
///             state below. <see cref="MapRoomEntity" /> and the record classes are only data: nothing outside this
///             class changes them, and whoever needs to read them outside gets copies
///             (<see cref="GetSnapshot" />, <see cref="DetachRooms" />).
///         </item>
///         <item>
///             The packets that announce a change. They are queued before the gate is left (the packet sender queues
///             synchronously), so clients are told of changes in the order the server made them: the end of one
///             player's control cannot overtake the next player's grant.
///         </item>
///     </list>
///     <para>
///         The gate is taken first, the lock of <see cref="SimulationOwnershipData" /> second (light, battery and
///         preview reports are queued under that lock as well, so a lock cannot change hands before the report it
///         authorized is on its way). Code that runs under the ownership lock must not enter the gate.
///     </para>
///     <para>
///         The gate is not reentrant. Nothing awaited inside it may call a method of this class that enters it; such a
///         call would never return. <see cref="IsScannerCamera" />, <see cref="IsControlledBy" /> and
///         <see cref="TryGetController" /> do not enter the gate and may be called from anywhere.
///     </para>
/// </remarks>
internal sealed partial class ScannerRoomManager(IPacketSender packetSender, EntityRegistry entityRegistry, WorldEntityManager worldEntityManager,
    SimulationOwnershipData simulationOwnershipData, PlayerManager playerManager, ScannerRoomDiagnostics diagnostics,
    ILogger<ScannerRoomManager> logger) : ISessionCleaner
{
    private const string MAP_ROOM_CAMERA_CLASS_ID = "733fd479-0760-4bc2-a03e-281cbf02bfa4";
    private static readonly NitroxTechType mapRoomCameraTechType = new("MapRoomCamera");

    private readonly SemaphoreSlim gate = new(1, 1);

    /// <summary>
    ///     The rooms this manager has seen: the ones in a loaded save, the ones built since, and any other it was asked
    ///     about. The entity registry stays the authority on which of them still exist, see <see cref="TryGetRoom" />.
    ///     Only used inside the gate.
    /// </summary>
    private readonly Dictionary<NitroxId, MapRoomEntity> roomsById = [];

    /// <summary>
    ///     Every id that has ever been handled as a Scanner Room camera, kept after the camera is gone. A request that
    ///     is still on its way when its camera is removed is then recognised as one for a camera and refused, instead
    ///     of being treated as a request for an ordinary entity. Written inside the gate, read from anywhere.
    /// </summary>
    private readonly ConcurrentDictionary<NitroxId, byte> knownCameraIds = new();

    /// <summary>
    ///     Removes what a departed player left behind that no other service has to act on. Their camera control ends
    ///     in <see cref="RevokeLocksOfSessionAsync" />, which the simulation cleanup calls because it has to run before
    ///     the cameras are handed to other players.
    /// </summary>
    public async Task OnEventAsync(ISessionCleaner.Args args)
    {
        using Hold held = await EnterAsync();
        // Session ids are handed out again after a delay, so a subscription must not outlive its session.
        subscribers.RemoveWhere(subscriber => subscriber.SessionId == args.Session.Id);
    }

    /// <summary>
    ///     Whether this id belongs to a Scanner Room camera: one out in the world, or one a room has, or ever had, a
    ///     record for. Does not enter the gate.
    /// </summary>
    public bool IsScannerCamera(NitroxId id) => id != null && (knownCameraIds.ContainsKey(id) || IsWorldCamera(id));

    /// <summary>
    ///     Called once the saved world is in the entity registry and before any player can connect. Finds the rooms and
    ///     their cameras, and puts every camera that a room still has a record for, but that exists nowhere (in no dock
    ///     and as no entity), back into a free dock of that room. That changes dock state without a packet, which is
    ///     only right while nobody is connected who would have to be told.
    /// </summary>
    public void WorldLoaded()
    {
        using Hold held = Enter();
        foreach (MapRoomEntity room in entityRegistry.GetEntities<MapRoomEntity>())
        {
            roomsById[room.Id] = room;
            RememberCameras(room);
        }
        List<MapRoomEntity> rooms = GetRooms();
        foreach (MapRoomEntity room in rooms)
        {
            int restored = RestoreOrphanedRegistrations(room, id => entityRegistry.TryGetEntityById(id, out Entity? _));
            if (restored > 0)
            {
                diagnostics.RecordInvariantFailure("orphan_recovery", room, reason: $"restored_{restored}");
            }
        }
        foreach (MapRoomEntity room in rooms)
        {
            diagnostics.RecordCheckpoint("load", room);
        }
    }

    /// <summary>
    ///     A copy of what the server holds for one room, or null if no such room is registered.
    /// </summary>
    public ScannerRoomSnapshot? GetSnapshot(NitroxId roomId)
    {
        using Hold held = Enter();
        return TryGetRoom(roomId, out MapRoomEntity? room) ? Capture(room) : null;
    }

    /// <summary>
    ///     Copies of all rooms, taken at one moment and ordered by id.
    /// </summary>
    public IReadOnlyList<ScannerRoomSnapshot> GetSnapshots()
    {
        using Hold held = Enter();
        return [.. GetRooms().OrderBy(room => room.Id.ToString(), StringComparer.Ordinal).Select(Capture)];
    }

    /// <summary>
    ///     Returns the given entities with every Scanner Room in them replaced by a copy, all copies taken at one
    ///     moment. A save and a building resync serialize these on their own thread while packets keep changing the
    ///     live rooms; with the copies the serializer needs no lock on anything, and packet handling never waits for
    ///     it.
    /// </summary>
    /// <param name="rooms">The copies that were put in, for the caller to describe what it wrote.</param>
    public List<GlobalRootEntity> DetachRooms(IEnumerable<GlobalRootEntity> entities, out IReadOnlyList<ScannerRoomSnapshot> rooms)
    {
        using Hold held = Enter();
        return DetachRoomsInGate(entities, out rooms);
    }

    /// <inheritdoc cref="DetachRooms(IEnumerable{GlobalRootEntity}, out IReadOnlyList{ScannerRoomSnapshot})" />
    public List<GlobalRootEntity> DetachRooms(IEnumerable<GlobalRootEntity> entities) => DetachRooms(entities, out _);

    /// <summary>
    ///     The global root entities a joining player is sent: without the cameras that sit in a dock (the client
    ///     creates those from the room's record), and with every room as a copy from the same moment the docks were
    ///     read at.
    /// </summary>
    public List<GlobalRootEntity> GetInitialSyncEntities(IEnumerable<GlobalRootEntity> entities)
    {
        using Hold held = Enter();
        HashSet<NitroxId> dockedCameraIds = GetDockedCameraIdsInGate();
        return DetachRoomsInGate(entities.Where(entity => !dockedCameraIds.Contains(entity.Id)), out _);
    }

    /// <summary>
    ///     The cameras that sit in a dock right now. A docked camera is a record of its room and is simulated by nobody.
    /// </summary>
    public HashSet<NitroxId> GetDockedCameraIds()
    {
        using Hold held = Enter();
        return GetDockedCameraIdsInGate();
    }

    /// <summary>
    ///     For tests that stand in for an operation in flight: nothing else gets into the gate until the result is disposed.
    /// </summary>
    internal async Task<IDisposable> HoldGateAsync() => await EnterAsync();

    private HashSet<NitroxId> GetDockedCameraIdsInGate()
    {
        HashSet<NitroxId> dockedCameraIds = [];
        foreach (MapRoomEntity room in GetRooms())
        {
            if (room.LeftDockCameraId != null)
            {
                dockedCameraIds.Add(room.LeftDockCameraId);
            }
            if (room.RightDockCameraId != null)
            {
                dockedCameraIds.Add(room.RightDockCameraId);
            }
        }
        return dockedCameraIds;
    }

    private List<GlobalRootEntity> DetachRoomsInGate(IEnumerable<GlobalRootEntity> entities, out IReadOnlyList<ScannerRoomSnapshot> rooms)
    {
        List<ScannerRoomSnapshot> copies = [];
        List<GlobalRootEntity> detached = [];
        foreach (GlobalRootEntity entity in entities)
        {
            switch (entity)
            {
                case MapRoomEntity room:
                    detached.Add(Detach(room, copies));
                    break;
                case BuildEntity build when build.ChildEntities.Exists(child => child is MapRoomEntity):
                    // A room is a piece of its base. The base is copied shallowly so the copy can take the room's place.
                    List<Entity> children = [.. build.ChildEntities.Select(child => child is MapRoomEntity room ? Detach(room, copies) : child)];
                    detached.Add(new BuildEntity(build.BaseData, build.Transform, build.Level, build.ClassId, build.SpawnedByServer, build.Id, build.TechType,
                        build.Metadata, build.ParentId, children) { OperationId = build.OperationId });
                    break;
                default:
                    detached.Add(entity);
                    break;
            }
        }
        rooms = copies;
        return detached;
    }

    private MapRoomEntity Detach(MapRoomEntity room, List<ScannerRoomSnapshot> copies)
    {
        ScannerRoomSnapshot snapshot = Capture(room);
        copies.Add(snapshot);
        return snapshot.Room;
    }

    private ScannerRoomSnapshot Capture(MapRoomEntity room)
    {
        scansByRoomId.TryGetValue(room.Id, out RoomScan? scan);
        return new ScannerRoomSnapshot(CopyRoom(room), scan?.Results.Count ?? 0, scan?.AvailableTypes.Count ?? 0);
    }

    /// <summary>
    ///     An exact copy: unlike the constructor used for loading a save, nothing is normalized, so a broken room is
    ///     saved and reported as it is.
    /// </summary>
    private static MapRoomEntity CopyRoom(MapRoomEntity room) => new(room.Id, room.ParentId!, room.Cell)
    {
        TechType = room.TechType,
        Metadata = room.Metadata,
        ChildEntities = [.. room.ChildEntities],
        Transform = room.Transform,
        Level = room.Level,
        ClassId = room.ClassId,
        SpawnedByServer = room.SpawnedByServer,
        LeftDockCameraId = room.LeftDockCameraId,
        RightDockCameraId = room.RightDockCameraId,
        DockingRevision = room.DockingRevision,
        CameraRegistry = [.. room.CameraRegistry.Select(record => record == null ? null! : new MapRoomCameraRecord(record.CameraId, record.CameraNumber, record.LightOn,
            record.LightRevision, record.Energy, record.Health, record.ComponentRevision))],
        FabricatorMetadata = room.FabricatorMetadata
    };

    private async ValueTask<Hold> EnterAsync()
    {
        await gate.WaitAsync();
        return new Hold(gate);
    }

    private Hold Enter()
    {
        gate.Wait();
        return new Hold(gate);
    }

    /// <summary>
    ///     The room registered under this id. The entity registry decides whether a room exists; a room it holds that
    ///     is new to this manager is taken in here. A room it no longer holds stays in <see cref="roomsById" /> until
    ///     its deconstruction has been handled, which needs to find it.
    /// </summary>
    private bool TryGetRoom(NitroxId? roomId, [NotNullWhen(true)] out MapRoomEntity? room)
    {
        room = null;
        if (roomId == null || !entityRegistry.TryGetEntityById(roomId, out room))
        {
            return false;
        }
        TrackRoom(room);
        return true;
    }

    private void TrackRoom(MapRoomEntity room)
    {
        if (roomsById.TryGetValue(room.Id, out MapRoomEntity? tracked) && ReferenceEquals(tracked, room))
        {
            return;
        }
        roomsById[room.Id] = room;
        RememberCameras(room);
    }

    private void RememberCameras(MapRoomEntity room)
    {
        foreach (MapRoomCameraRecord? record in room.CameraRegistry)
        {
            if (record?.CameraId != null)
            {
                knownCameraIds.TryAdd(record.CameraId, 0);
            }
        }
        if (room.LeftDockCameraId != null)
        {
            knownCameraIds.TryAdd(room.LeftDockCameraId, 0);
        }
        if (room.RightDockCameraId != null)
        {
            knownCameraIds.TryAdd(room.RightDockCameraId, 0);
        }
    }

    /// <summary>
    ///     Every room that is registered right now.
    /// </summary>
    private List<MapRoomEntity> GetRooms()
    {
        List<MapRoomEntity> rooms = new(roomsById.Count);
        foreach (NitroxId roomId in roomsById.Keys.ToArray())
        {
            if (TryGetRoom(roomId, out MapRoomEntity? room))
            {
                rooms.Add(room);
            }
        }
        return rooms;
    }

    private MapRoomEntity? FindRoom(Optional<NitroxId> roomId) => roomId.HasValue && TryGetRoom(roomId.Value, out MapRoomEntity? room) ? room : null;

    private bool IsWorldCamera(NitroxId cameraId) => TryGetWorldCamera(cameraId, out _);

    private bool TryGetWorldCamera(NitroxId cameraId, [NotNullWhen(true)] out WorldEntity? camera) =>
        entityRegistry.TryGetEntityById(cameraId, out camera) && mapRoomCameraTechType.Equals(camera.TechType);

    /// <summary>
    ///     Where a camera is on record, across all rooms. A camera belongs to at most one room and sits in at most one
    ///     dock; anything else is a broken state that the callers refuse to build on.
    /// </summary>
    private CameraTopology GetTopology(NitroxId cameraId)
    {
        int registrations = 0;
        int dockedAssociations = 0;
        MapRoomEntity? firstRoom = null;
        MapRoomCameraRecord? firstRecord = null;
        MapRoomEntity? lastRoom = null;
        MapRoomCameraRecord? lastRecord = null;
        MapRoomEntity? dockedRoom = null;
        int dockedSlot = -1;
        foreach (MapRoomEntity room in GetRooms())
        {
            if (room.IsCameraDocked(cameraId))
            {
                dockedAssociations++;
            }
            if (room.GetCameraRecord(cameraId) is not { } record)
            {
                continue;
            }
            registrations++;
            firstRoom ??= room;
            firstRecord ??= record;
            lastRoom = room;
            lastRecord = record;
            int slot = GetDockingIndex(room, cameraId);
            if (slot >= 0)
            {
                dockedRoom = room;
                dockedSlot = slot;
            }
        }
        return new CameraTopology(IsWorldCamera(cameraId), registrations, dockedAssociations, firstRoom, firstRecord, lastRoom, lastRecord, dockedRoom, dockedSlot);
    }

    /// <summary>
    ///     The dock the camera sits in, or -1.
    /// </summary>
    private static int GetDockingIndex(MapRoomEntity room, NitroxId cameraId) =>
        room.LeftDockCameraId == cameraId ? 0 : room.RightDockCameraId == cameraId ? 1 : -1;

    private ValueTask ReplyAsync<T>(Player player, T packet) where T : Packet => packetSender.SendPacketAsync(packet, player.SessionId);

    private ValueTask SendToAllAsync<T>(T packet) where T : Packet => packetSender.SendPacketToAllAsync(packet);

    private ValueTask SendToOthersAsync<T>(Player player, T packet) where T : Packet => packetSender.SendPacketToOthersAsync(packet, player.SessionId);

    private readonly struct Hold(SemaphoreSlim gate) : IDisposable
    {
        public void Dispose() => gate.Release();
    }

    /// <param name="FirstRoom">The first room found with a record for the camera.</param>
    /// <param name="LastRoom">The last room found with a record for the camera; the same room unless the state is broken.</param>
    /// <param name="DockedRoom">The room that has both a record for the camera and the camera in a dock.</param>
    private readonly record struct CameraTopology(
        bool IsWorldCamera,
        int Registrations,
        int DockedAssociations,
        MapRoomEntity? FirstRoom,
        MapRoomCameraRecord? FirstRecord,
        MapRoomEntity? LastRoom,
        MapRoomCameraRecord? LastRecord,
        MapRoomEntity? DockedRoom,
        int DockedSlot)
    {
        public bool IsAmbiguous => Registrations > 1 || DockedAssociations > 1 || DockedAssociations == 1 && Registrations != 1;

        public bool IsScannerCamera => IsWorldCamera || Registrations > 0;

        public bool IsKnownScannerCamera => !IsAmbiguous && (IsWorldCamera || Registrations == 1);

        /// <summary>
        ///     A registered world entity that is docked nowhere. Such a camera is simulated by a player like any other
        ///     loose entity, while a docked camera is only a record of its room and is simulated by nobody.
        /// </summary>
        public bool IsLooseWorldCamera => IsKnownScannerCamera && IsWorldCamera && DockedAssociations == 0;

        /// <summary>
        ///     The room the camera belongs to, if it belongs to exactly one.
        /// </summary>
        public MapRoomEntity? UniqueRoom => Registrations == 1 ? LastRoom : null;
    }
}
