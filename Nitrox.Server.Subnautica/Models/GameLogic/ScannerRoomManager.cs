using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Bases;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;
using Nitrox.Server.Subnautica.Models.AppEvents;
using Nitrox.Server.Subnautica.Models.GameLogic.Entities;
using Nitrox.Server.Subnautica.Models.Packets.Core;

namespace Nitrox.Server.Subnautica.Models.GameLogic;

/// <summary>
///     Everything the server knows and decides about Scanner Rooms and their cameras: which camera sits in which dock,
///     who controls a camera, the light, battery, health and number of every camera, a room's scan target, scan results and
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
///             Every read and every change of Scanner Room state: the Scanner Room data of a <see cref="MapRoomEntity" />,
///             which cameras are its children, the state every camera keeps on its own entity, and the private
///             state below. Nothing outside this class changes them, and whoever needs to read a room outside gets
///             a copy (<see cref="GetSnapshot" />, <see cref="DetachRooms" />).
///         </item>
///         <item>
///             The packets that announce a change. They are queued before the gate is left (the packet sender queues
///             synchronously), so clients are told of changes in the order the server made them: the end of one
///             player's control cannot overtake the next player's grant.
///         </item>
///     </list>
///     <para>
///         The gate is taken first, the lock of <see cref="SimulationOwnershipData" /> second (what a player may set
///         of a camera's state is decided and stored under that lock, and a preview is queued under it, so a lock
///         cannot change hands in the middle of either). Code that runs under the ownership lock must not enter the
///         gate. An accepted update of a camera's state is passed on by the packet processor after the gate is left,
///         like every entity metadata update.
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
    ///     Every id that has ever been handled as a Scanner Room camera, kept after the camera is gone or went into an
    ///     inventory. A request that is still on its way then is recognised as one for a camera and refused, instead
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
    ///     Whether this id belongs to a Scanner Room camera: one in a dock or out in the world, or one that ever was
    ///     handled as a camera. Does not enter the gate.
    /// </summary>
    public bool IsScannerCamera(NitroxId id) => id != null && (knownCameraIds.ContainsKey(id) || IsWorldCamera(id));

    /// <summary>
    ///     Whether this entity is a camera that sits in a dock. A docked camera does not move unless a player controls
    ///     it. Does not enter the gate.
    /// </summary>
    public bool IsDockedCamera(WorldEntity entity) =>
        IsCamera(entity) && entity.ParentId != null && entityRegistry.TryGetEntityById(entity.ParentId, out MapRoomEntity? _);

    /// <summary>
    ///     Called once the saved world is in the entity registry and before any player can connect. Finds the rooms and
    ///     the cameras, and how far the camera numbers have got.
    /// </summary>
    public void WorldLoaded()
    {
        using Hold held = Enter();
        foreach (MapRoomEntity room in entityRegistry.GetEntities<MapRoomEntity>())
        {
            roomsById[room.Id] = room;
            RememberCameras(room);
        }
        List<Entity> cameras = entityRegistry.GetAllEntities().Where(IsCamera).ToList();
        lastCameraNumber = cameras.Count == 0 ? 0 : cameras.Max(camera => ReadState(camera).CameraNumber);
        // In a fixed order, so that a world whose cameras have no numbers yet gets the same ones every time it is loaded.
        foreach (Entity camera in cameras.OrderBy(camera => camera.Id.ToString(), StringComparer.Ordinal))
        {
            SetState(camera, EnsureCameraNumber(ReadState(camera)));
        }
        foreach (MapRoomEntity room in GetRooms())
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
    ///     For tests that stand in for an operation in flight: nothing else gets into the gate until the result is disposed.
    /// </summary>
    internal async Task<IDisposable> HoldGateAsync() => await EnterAsync();

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
    ///     A copy of the room and of the cameras in its docks. The room's other children are the same objects as in
    ///     the original; nothing in this class changes them.
    /// </summary>
    private static MapRoomEntity CopyRoom(MapRoomEntity room) => new(room.Id, room.ParentId!, room.Cell)
    {
        TechType = room.TechType,
        Metadata = room.Metadata,
        ChildEntities = [.. room.ChildEntities.Select(child => child is GlobalRootEntity camera && IsCamera(camera) ? CopyCamera(camera) : child)],
        Transform = room.Transform,
        Level = room.Level,
        ClassId = room.ClassId,
        SpawnedByServer = room.SpawnedByServer,
        FabricatorMetadata = room.FabricatorMetadata
    };

    private static GlobalRootEntity CopyCamera(GlobalRootEntity camera) =>
        new(camera.Transform, camera.Level, camera.ClassId, camera.SpawnedByServer, camera.Id, camera.TechType, camera.Metadata, camera.ParentId, [.. camera.ChildEntities]);

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
        foreach (Entity child in room.ChildEntities)
        {
            if (IsCamera(child))
            {
                knownCameraIds.TryAdd(child.Id, 0);
            }
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

    internal static bool IsCamera(Entity? entity) => entity != null && mapRoomCameraTechType.Equals(entity.TechType);

    /// <summary>
    ///     A camera as the entity registry has it: in a dock (a child of that room), loose in the world, or in an
    ///     inventory. Empty if the id is not a camera's.
    /// </summary>
    private CameraView GetCamera(NitroxId? cameraId)
    {
        if (cameraId == null || !entityRegistry.TryGetEntityById(cameraId, out Entity? entity) || !IsCamera(entity))
        {
            return new CameraView(null, new MapRoomCameraMetadata(), null);
        }
        MapRoomEntity? dockedRoom = entity is WorldEntity && entity.ParentId != null && TryGetRoom(entity.ParentId, out MapRoomEntity? room) ? room : null;
        return new CameraView(entity, ReadState(entity), dockedRoom);
    }

    private ValueTask ReplyAsync<T>(Player player, T packet) where T : Packet => packetSender.SendPacketAsync(packet, player.SessionId);

    private ValueTask SendToAllAsync<T>(T packet) where T : Packet => packetSender.SendPacketToAllAsync(packet);

    private ValueTask SendToOthersAsync<T>(Player player, T packet) where T : Packet => packetSender.SendPacketToOthersAsync(packet, player.SessionId);

    private readonly struct Hold(SemaphoreSlim gate) : IDisposable
    {
        public void Dispose() => gate.Release();
    }

    /// <param name="Entity">The camera's entity, or null if there is no such camera.</param>
    /// <param name="State">What the camera's entity holds, or the state of a new camera if it holds nothing.</param>
    /// <param name="DockedRoom">The room the camera is a child of, if it sits in a dock.</param>
    private readonly record struct CameraView(Entity? Entity, MapRoomCameraMetadata State, MapRoomEntity? DockedRoom)
    {
        public bool Exists => Entity != null;

        /// <summary>
        ///     The camera if it is in a dock or loose in the world, null if it is in an inventory or does not exist.
        /// </summary>
        public WorldEntity? WorldCamera => Entity as WorldEntity;

        /// <summary>
        ///     Out in the world and in no dock. Such a camera drifts, can be picked up, and can be carried by a Stalker.
        /// </summary>
        public bool IsLooseWorldCamera => Entity is WorldEntity && DockedRoom == null;

        public int DockedSlot => DockedRoom != null ? State.DockingIndex : MapRoomCameraMetadata.NOT_DOCKED;
    }
}
