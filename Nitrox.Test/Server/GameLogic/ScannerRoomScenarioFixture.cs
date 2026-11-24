using System.Net;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Nitrox.Model.Configuration;
using Nitrox.Model.Core;
using Nitrox.Model.DataStructures;
using Nitrox.Model.DataStructures.GameLogic;
using Nitrox.Model.DataStructures.Unity;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Bases;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Bases;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;
using Nitrox.Model.Subnautica.Packets;
using Nitrox.Server.Subnautica.Models.AppEvents;
using Nitrox.Server.Subnautica.Models.Communication;
using Nitrox.Server.Subnautica.Models.GameLogic;
using Nitrox.Server.Subnautica.Models.GameLogic.Bases;
using Nitrox.Server.Subnautica.Models.GameLogic.Entities;
using Nitrox.Server.Subnautica.Models.Packets.Core;
using Nitrox.Server.Subnautica.Models.Packets.Processors;
using Nitrox.Test.Server.Helper;
using NSubstitute;
using Packet = Nitrox.Model.Packets.Packet;
using ServerPlayer = Nitrox.Server.Subnautica.Models.Player;

namespace Nitrox.Test.Server.GameLogic;

/// <summary>
///     Deterministic in-process Scanner Room server: the real <see cref="ScannerRoomManager" /> on a real entity
///     registry and lock table, with only network delivery replaced by a substitute that records what was sent.
///     Scanner Room requests go straight to the manager; the processors of other packets that reach into it (pickup,
///     destruction, building, simulation requests, metadata) are the real ones.
/// </summary>
internal sealed class ScannerRoomScenarioFixture
{
    internal static readonly NitroxId RoomId = new("00000000-0000-0000-0000-000000000100");
    internal static readonly NitroxId RoomParentId = new("00000000-0000-0000-0000-000000000101");
    internal static readonly NitroxId CameraAId = new("00000000-0000-0000-0000-000000000201");
    internal static readonly NitroxId CameraBId = new("00000000-0000-0000-0000-000000000202");
    internal static readonly NitroxId CameraCId = new("00000000-0000-0000-0000-000000000203");
    private const string CAMERA_CLASS_ID = "733fd479-0760-4bc2-a03e-281cbf02bfa4";

    private readonly SimulationOwnershipRequestProcessor ownershipRequestProcessor;
    private readonly PlaceBaseProcessor placeBaseProcessor;
    private readonly PickupItemPacketProcessor pickupProcessor;
    private readonly EntitySpawnedByClientProcessor spawnProcessor;
    private readonly UpdateBaseProcessor updateBaseProcessor;
    private readonly EntityDestroyedPacketProcessor destroyProcessor;
    private readonly EntityMetadataUpdateProcessor metadataProcessor;

    /// <summary>
    ///     Records everything the server sends, to whomever, in the order it is sent.
    /// </summary>
    internal IPacketSender PacketSender { get; }
    internal EntityRegistry EntityRegistry { get; }
    internal SimulationOwnershipData Ownership { get; } = new();
    internal ScannerRoomDiagnostics Diagnostics { get; }
    internal ScannerRoomManager Manager { get; }
    internal PlayerManager PlayerManager { get; }
    internal EntitySimulation EntitySimulation { get; }
    internal WorldEntityManager WorldEntityManager { get; }
    internal ItemClaims ItemClaims { get; }
    internal MapRoomEntity Room { get; }
    internal ServerPlayer PlayerA { get; }
    internal ServerPlayer PlayerB { get; }
    internal IReadOnlyList<NitroxId> CameraIds { get; } = [CameraAId, CameraBId, CameraCId];

    /// <param name="packetSender">For tests that need the sender to do more than record.</param>
    /// <param name="playerManager">For tests that need connected players; the default has none.</param>
    /// <param name="diagnosticsLog">For tests that read the lines the diagnostics write; these diagnostics also know the lock table.</param>
    /// <param name="connectedPlayers">
    ///     For tests of what is passed on to the other players: the fixture's two players are then connected players
    ///     of this manager, and more can be connected to it.
    /// </param>
    internal ScannerRoomScenarioFixture(IPacketSender? packetSender = null, PlayerManager? playerManager = null, ILogger<ScannerRoomDiagnostics>? diagnosticsLog = null,
        ConnectedPlayers? connectedPlayers = null)
    {
        PacketSender = packetSender ?? Substitute.For<IPacketSender>();
        PlayerManager = connectedPlayers?.PlayerManager ?? playerManager ?? new PlayerManager(null!, null!, null!, NullLogger<PlayerManager>.Instance);
        PlayerA = connectedPlayers?.Connect("PlayerA") ?? CreatePlayer(1, "A");
        PlayerB = connectedPlayers?.Connect("PlayerB") ?? CreatePlayer(2, "B");
        EntityRegistry = new EntityRegistry(NullLogger<EntityRegistry>.Instance);
        Room = new MapRoomEntity(RoomId, RoomParentId, new NitroxInt3(4, -2, 9));
        EntityRegistry.AddEntity(Room);

        Diagnostics = diagnosticsLog != null ? new ScannerRoomDiagnostics(diagnosticsLog, Ownership) : new ScannerRoomDiagnostics(NullLogger<ScannerRoomDiagnostics>.Instance);
        // Batch spawning and progress reporting are not part of these scenarios.
        WorldEntityManager = new WorldEntityManager(PacketSender, EntityRegistry, null!, PlayerManager, null!, NullLogger<WorldEntityManager>.Instance);
        Manager = CreateManager(EntityRegistry, Ownership, Diagnostics, PacketSender, PlayerManager, WorldEntityManager);
        Manager.RoomBuilt(Room);
        foreach (NitroxId cameraId in CameraIds)
        {
            // Stored the way a camera a player crafted is: the server numbers it first, here 1, 2 and 3.
            GlobalRootEntity camera = CreateCamera(cameraId);
            Manager.KeepCameraStateOnReplacement(camera);
            WorldEntityManager.AddOrUpdateGlobalRootEntity(camera);
        }
        EntitySimulation = new EntitySimulation(PacketSender, EntityRegistry, WorldEntityManager, Ownership, PlayerManager, Manager, NullLogger<EntitySimulation>.Instance);
        ownershipRequestProcessor = new(Ownership, EntitySimulation, EntityRegistry, Manager);
        BuildingManager buildingManager = new(PacketSender, EntityRegistry, WorldEntityManager,
            Options.Create(new SubnauticaServerOptions()), Manager, NullLogger<BuildingManager>.Instance);
        placeBaseProcessor = new(buildingManager, EntitySimulation, Manager);
        updateBaseProcessor = new(buildingManager, EntitySimulation, Manager);
        ItemClaims = new ItemClaims(EntityRegistry, PlayerManager);
        destroyProcessor = new(PlayerManager, EntitySimulation, WorldEntityManager, Manager, EntityRegistry, ItemClaims);
        // Base power sources are not part of these scenarios.
        metadataProcessor = new(PlayerManager, EntityRegistry, Ownership, Manager, null!, WorldEntityManager, EntitySimulation, ItemClaims, NullLogger<EntityMetadataUpdateProcessor>.Instance);
        pickupProcessor = new(EntityRegistry, WorldEntityManager, Ownership, Manager, ItemClaims);
        spawnProcessor = new(PlayerManager, EntityRegistry, WorldEntityManager, EntitySimulation, Manager, ItemClaims);
    }

    /// <summary>
    ///     A manager on the given registry and lock table, for tests that bring their own.
    /// </summary>
    internal static ScannerRoomManager CreateManager(EntityRegistry entityRegistry, SimulationOwnershipData ownership, ScannerRoomDiagnostics diagnostics,
        IPacketSender? packetSender = null, PlayerManager? playerManager = null, WorldEntityManager? worldEntityManager = null)
    {
        packetSender ??= Substitute.For<IPacketSender>();
        playerManager ??= new PlayerManager(null!, null!, null!, NullLogger<PlayerManager>.Instance);
        worldEntityManager ??= new WorldEntityManager(packetSender, entityRegistry, null!, playerManager, null!, NullLogger<WorldEntityManager>.Instance);
        return new ScannerRoomManager(packetSender, entityRegistry, worldEntityManager, ownership, playerManager, diagnostics, NullLogger<ScannerRoomManager>.Instance);
    }

    /// <summary>
    ///     Registers another room the way a finished construction does.
    /// </summary>
    internal MapRoomEntity AddRoom(MapRoomEntity room)
    {
        Manager.RoomBuilt(room);
        EntityRegistry.AddEntity(room);
        return room;
    }

    /// <summary>
    ///     Makes a room scan for a type, with a player who simulates the room. A scan is measured from the room's
    ///     base, which is put at the origin.
    /// </summary>
    internal void StartScan(ServerPlayer simulator, MapRoomEntity room, NitroxTechType target, long generation = 5)
    {
        if (!EntityRegistry.TryGetEntityById(RoomParentId, out WorldEntity _))
        {
            EntityRegistry.AddEntity(new WorldEntity(NitroxVector3.Zero, NitroxQuaternion.Identity, NitroxVector3.One, new NitroxTechType("Base"), 0,
                RoomParentId.ToString(), true, RoomParentId, null));
        }
        room.Metadata = new MapRoomMetadata(target, 0, generation);
        if (Ownership.GetPlayerForLock(room.Id) != simulator)
        {
            Ownership.RevokeOwnerOfId(room.Id);
            Assert.IsTrue(Ownership.TryAcquire(room.Id, simulator, SimulationLockType.TRANSIENT));
        }
    }

    /// <summary>
    ///     Reports the full list of what a room's scan has found, the way its simulating player's client does.
    /// </summary>
    /// <returns>The packets the report was answered with, to whomever.</returns>
    internal Task<IReadOnlyList<Packet>> ReportScanAsync(ServerPlayer player, MapRoomEntity room, params MapRoomScanResultRecord[] results) =>
        SentDuringAsync(() => Manager.ReportScanAsync(player, new MapRoomScanReport(room.Id, (room.Metadata as MapRoomMetadata)?.Generation ?? 0,
            NitroxVector3.Zero, 300f, [.. results])));

    /// <summary>
    ///     Every packet sent so far, to whomever, in the order it was sent.
    /// </summary>
    internal IReadOnlyList<Packet> SentPackets() => PacketSender.ReceivedCalls().Select(call => call.GetArguments()[0]).OfType<Packet>().ToList();

    /// <summary>
    ///     The packets of one type sent so far, to whomever.
    /// </summary>
    internal List<T> Sent<T>() where T : Packet => SentPackets().OfType<T>().ToList();

    /// <summary>
    ///     The packets of one type sent so far to every player at once.
    /// </summary>
    internal List<T> SentToAll<T>() where T : Packet => SentBy<T>(nameof(IPacketSender.SendPacketToAllAsync));

    /// <summary>
    ///     The packets of one type sent so far to every player but one.
    /// </summary>
    internal List<T> SentToOthers<T>() where T : Packet => SentBy<T>(nameof(IPacketSender.SendPacketToOthersAsync));

    /// <summary>
    ///     The packets of one type sent so far to this player alone.
    /// </summary>
    internal List<T> SentTo<T>(ServerPlayer player) where T : Packet =>
        PacketSender.ReceivedCalls()
                    .Where(call => call.GetMethodInfo().Name == nameof(IPacketSender.SendPacketAsync) && Equals(call.GetArguments()[1], player.SessionId))
                    .Select(call => call.GetArguments()[0])
                    .OfType<T>()
                    .ToList();

    private List<T> SentBy<T>(string sendMethod) where T : Packet =>
        PacketSender.ReceivedCalls().Where(call => call.GetMethodInfo().Name == sendMethod).Select(call => call.GetArguments()[0]).OfType<T>().ToList();

    /// <summary>
    ///     The packets sent while <paramref name="action" /> ran.
    /// </summary>
    internal async Task<IReadOnlyList<Packet>> SentDuringAsync(Func<Task> action)
    {
        int alreadySent = SentPackets().Count;
        await action();
        return SentPackets().Skip(alreadySent).ToList();
    }

    /// <summary>
    ///     Reports a camera as docked in, or undocked from, a room: the fixture's room unless another one is given.
    /// </summary>
    /// <param name="establishAuthority">
    ///     Makes the player the camera's simulating player first, which is who reports a dock. False to send the
    ///     report as whoever the player is to the camera right now.
    /// </param>
    internal async Task<DockResult> DockAsync(ServerPlayer player, NitroxId cameraId, int slot, bool isDocked, bool establishAuthority = true, NitroxId? roomId = null)
    {
        roomId ??= RoomId;
        if (establishAuthority && Ownership.GetPlayerForLock(cameraId) != player)
        {
            Ownership.RevokeOwnerOfId(cameraId);
            Assert.IsTrue(Ownership.TryAcquire(cameraId, player, SimulationLockType.TRANSIENT));
        }
        int alreadySent = SentPackets().Count;
        bool granted = isDocked
            ? await Manager.DockCameraAsync(player, new MapRoomCameraDocked(cameraId, roomId, slot))
            : await Manager.UndockCameraAsync(player, new MapRoomCameraUndocked(cameraId, roomId));
        return new DockResult(granted, CameraState(cameraId), SentPackets().Skip(alreadySent).ToList());
    }

    /// <summary>
    ///     What the server holds for a camera, wherever the camera is. The state of a new camera if there is none.
    /// </summary>
    internal MapRoomCameraMetadata CameraState(NitroxId cameraId) =>
        EntityRegistry.TryGetEntityById(cameraId, out Entity camera) && camera.Metadata is MapRoomCameraMetadata state ? state : new MapRoomCameraMetadata();

    /// <summary>
    ///     Replaces what the server holds for a camera, for tests that start from a camera in a certain state.
    /// </summary>
    internal void SetCameraState(NitroxId cameraId, MapRoomCameraMetadata state)
    {
        Assert.IsTrue(EntityRegistry.TryGetEntityById(cameraId, out Entity camera));
        camera.Metadata = state;
    }

    internal void SetCameraComponents(NitroxId cameraId, float energy, float health)
    {
        SetCameraState(cameraId, CameraState(cameraId).WithComponents(energy, health));
    }

    /// <summary>
    ///     The camera in a dock of a room: the child of the room that is a camera with that docking index.
    /// </summary>
    internal NitroxId? DockedCamera(int slot, MapRoomEntity? room = null) =>
        (room ?? Room).ChildEntities.FirstOrDefault(child => ScannerRoomManager.IsCamera(child) && child.Metadata is MapRoomCameraMetadata state && state.DockingIndex == slot)?.Id;

    /// <summary>
    ///     Whether the camera is a child of a room.
    /// </summary>
    internal bool IsDocked(NitroxId cameraId) =>
        EntityRegistry.TryGetEntityById(cameraId, out Entity camera) && camera.ParentId != null && EntityRegistry.TryGetEntityById(camera.ParentId, out MapRoomEntity _);

    /// <summary>
    ///     Takes or gives up control of a camera the way a client does. To take it, the player says that it takes the
    ///     camera and asks for the camera's exclusive lock through the real lock request processor. To give it up, the
    ///     player says so and asks for an ordinary lock.
    /// </summary>
    internal async Task<ControlResult> ControlAsync(ServerPlayer player, NitroxId cameraId, bool isControlling)
    {
        int alreadySent = SentPackets().Count;
        bool granted;
        if (isControlling)
        {
            await Manager.SetControlAsync(player, new MapRoomCameraControlChanged(cameraId, player.SessionId, true));
            await RequestOwnershipAsync(player, cameraId, SimulationLockType.EXCLUSIVE);
            granted = IsController(cameraId, player.SessionId);
        }
        else
        {
            granted = IsController(cameraId, player.SessionId);
            await Manager.SetControlAsync(player, new MapRoomCameraControlChanged(cameraId, player.SessionId, false));
            if (Ownership.TryGetLock(cameraId, out SimulationOwnershipData.PlayerLock heldLock) && heldLock.Player == player && heldLock.LockType == SimulationLockType.EXCLUSIVE)
            {
                await RequestOwnershipAsync(player, cameraId, SimulationLockType.TRANSIENT);
            }
        }
        return new ControlResult(granted, Manager.TryGetController(cameraId, out SessionId controller) ? controller : null, SentPackets().Skip(alreadySent).ToList());
    }

    /// <summary>
    ///     Says that a player controls a camera, or no longer does, without asking for the lock: what the server
    ///     answers when the lock is not what the player claims.
    /// </summary>
    internal Task<IReadOnlyList<Packet>> ControlChangedAsync(ServerPlayer player, NitroxId cameraId, bool isControlling) =>
        SentDuringAsync(() => Manager.SetControlAsync(player, new MapRoomCameraControlChanged(cameraId, player.SessionId, isControlling)));

    /// <summary>
    ///     What deconstructing a room sends, once the room has left the entity registry.
    /// </summary>
    internal Task<IReadOnlyList<Packet>> CleanupAsync(MapRoomEntity room) => SentDuringAsync(() => Manager.RoomDeconstructedAsync(PlayerA, room));

    /// <summary>
    ///     Picks a camera up into the inventory of <paramref name="player" />. A client never sends camera state with
    ///     the inventory item; <paramref name="clientMetadata" /> is for tests of a client that does.
    /// </summary>
    internal Task<IReadOnlyList<Packet>> PickupAsync(ServerPlayer player, NitroxId cameraId, EntityMetadata? clientMetadata = null)
    {
        InventoryItemEntity item = new(cameraId, CAMERA_CLASS_ID, new NitroxTechType("MapRoomCamera"), clientMetadata, player.GameObjectId, []);
        return SentDuringAsync(() => pickupProcessor.Process(Context(player), new PickupItem(item)));
    }

    /// <summary>
    ///     Drops a camera from an inventory back into the world, the way the dropping client reports it.
    /// </summary>
    internal Task<IReadOnlyList<Packet>> DropAsync(ServerPlayer player, NitroxId cameraId, EntityMetadata? clientMetadata = null)
    {
        GlobalRootEntity camera = CreateCamera(cameraId);
        camera.Metadata = clientMetadata;
        return SentDuringAsync(() => spawnProcessor.Process(Context(player), new EntitySpawnedByClient(camera, true, true)));
    }

    /// <summary>
    ///     Announces an entity a client made, the way <c>Entities.BroadcastEntitySpawnedByClient</c> does.
    /// </summary>
    internal Task<IReadOnlyList<Packet>> SpawnByClientAsync(ServerPlayer player, Entity entity, bool requireSimulation = true) =>
        SentDuringAsync(() => spawnProcessor.Process(Context(player), new EntitySpawnedByClient(entity, false, requireSimulation)));

    /// <summary>
    ///     Finishes a piece on an existing base, the way the builder's client reports it.
    /// </summary>
    internal Task<IReadOnlyList<Packet>> UpdateBaseAsync(ServerPlayer player, UpdateBase packet) =>
        SentDuringAsync(() => updateBaseProcessor.Process(Context(player), packet));

    /// <summary>
    ///     Finishes a Scanner Room on a base that is made for it, the way the builder's client reports it. The room
    ///     names the base as its parent and comes with whatever children the test gave it.
    /// </summary>
    internal async Task<IReadOnlyList<Packet>> BuildRoomAsync(ServerPlayer builder, MapRoomEntity room)
    {
        BuildEntity builtBase = BuildEntity.MakeEmpty();
        builtBase.Id = room.ParentId;
        WorldEntityManager.AddOrUpdateGlobalRootEntity(builtBase);
        GhostEntity ghost = GhostEntity.MakeEmpty();
        ghost.Id = room.Id;
        ghost.ParentId = room.ParentId;
        ghost.TechType = new NitroxTechType("BaseMapRoom");
        WorldEntityManager.AddOrUpdateGlobalRootEntity(ghost);

        return await UpdateBaseAsync(builder, new UpdateBase(room.ParentId, room.Id, null, room, new Dictionary<NitroxId, NitroxBaseFace>(), new Dictionary<NitroxId, NitroxInt3>(),
            new Dictionary<NitroxId, NitroxInt3> { [room.Id] = room.Cell }, default, 1));
    }

    /// <summary>
    ///     Reports an entity as destroyed, the way the client that saw it die does.
    /// </summary>
    internal Task<IReadOnlyList<Packet>> DestroyAsync(ServerPlayer player, NitroxId entityId) =>
        SentDuringAsync(() => destroyProcessor.Process(Context(player), new EntityDestroyed(entityId)));

    /// <summary>
    ///     Sends a metadata update for an entity. Unless the fixture was given connected players there is nobody to
    ///     relay an accepted update to, so what comes back is only what the server replied to <paramref name="player" />.
    /// </summary>
    internal Task<IReadOnlyList<Packet>> UpdateMetadataAsync(ServerPlayer player, NitroxId entityId, EntityMetadata metadata) =>
        SentDuringAsync(() => metadataProcessor.Process(Context(player), new EntityMetadataUpdate(entityId, metadata)));

    /// <summary>
    ///     What a player is told about camera control once their initial sync has finished.
    /// </summary>
    internal Task<IReadOnlyList<Packet>> JoinedPlayerControlGrantsAsync(SessionId joinedSessionId) =>
        SentDuringAsync(() => Manager.SendControlGrantsAsync(joinedSessionId));

    /// <summary>
    ///     Runs the simulation cleanup of a player who left.
    /// </summary>
    /// <returns>The packets that cleanup sent.</returns>
    internal Task<IReadOnlyList<Packet>> DisconnectAsync(ServerPlayer player) =>
        SentDuringAsync(() => EntitySimulation.OnEventAsync(new ISessionCleaner.Args(
            new SessionManager.Session(player.SessionId, new IPEndPoint(IPAddress.Loopback, 11000)), 1)));

    /// <summary>
    ///     Finishes the first piece of a new base, the way the builder's client reports it.
    /// </summary>
    internal Task<IReadOnlyList<Packet>> PlaceBaseAsync(ServerPlayer player, PlaceBase packet) =>
        SentDuringAsync(() => placeBaseProcessor.Process(Context(player), packet));

    /// <summary>
    ///     Asks for a simulation lock the way a client does, through the real lock request processor.
    /// </summary>
    /// <returns>The answer to this player, also when another player's request is answered at the same moment.</returns>
    internal async Task<SimulationOwnershipResponse> RequestOwnershipAsync(ServerPlayer player, NitroxId entityId, SimulationLockType lockType)
    {
        int alreadyAnswered = SentTo<SimulationOwnershipResponse>(player).Count;
        await ownershipRequestProcessor.Process(Context(player), new SimulationOwnershipRequest(player.SessionId, entityId, lockType));
        return SentTo<SimulationOwnershipResponse>(player).Skip(alreadyAnswered).Single();
    }

    /// <summary>
    ///     Reports a camera's state the way a client does: everything it has for the camera, here what the server
    ///     holds with the given values changed.
    /// </summary>
    /// <returns>What the server sent in answer, see <see cref="UpdateMetadataAsync" />.</returns>
    internal Task<IReadOnlyList<Packet>> ReportCameraAsync(ServerPlayer player, NitroxId cameraId, bool? lightOn = null, float? energy = null, float? health = null)
    {
        MapRoomCameraMetadata stored = CameraState(cameraId);
        return UpdateMetadataAsync(player, cameraId,
            new MapRoomCameraMetadata(lightOn ?? stored.LightOn, energy ?? stored.Energy, health ?? stored.Health, stored.CameraNumber, stored.DockingIndex));
    }

    /// <summary>
    ///     Everything the server sends in answer to a camera preview packet.
    /// </summary>
    internal Task<IReadOnlyList<Packet>> PreviewAsync(ServerPlayer player, MapRoomCameraPreview packet) =>
        SentDuringAsync(() => Manager.PublishPreviewAsync(player, packet));

    /// <summary>
    ///     Whether the server has this player on record as the one controlling the camera.
    /// </summary>
    internal bool IsController(NitroxId cameraId, SessionId sessionId) =>
        Manager.TryGetController(cameraId, out SessionId controller) && controller == sessionId;

    internal bool HasController(NitroxId cameraId) => Manager.TryGetController(cameraId, out _);

    internal AuthProcessorContext Context(ServerPlayer player) => new(player, PacketSender);

    internal string Snapshot() =>
        $"left={DockedCamera(0)};right={DockedCamera(1)};cameras={string.Join(",", CameraIds.Select(id => $"{CameraState(id).CameraNumber}:{id}:{CameraState(id).DockingIndex}"))}";

    internal void AssertCanonicalInvariants()
    {
        Assert.IsNull(ScannerRoomStateFingerprint.Validate(Room), Snapshot());
        foreach (Entity camera in ScannerRoomStateFingerprint.GetCameras(Room))
        {
            Assert.AreEqual(Room.Id, camera.ParentId, Snapshot());
            Assert.IsTrue(EntityRegistry.TryGetEntityById(camera.Id, out Entity registered) && ReferenceEquals(registered, camera), Snapshot());
        }
    }

    /// <param name="Granted">Whether the player controls the camera now (or, for a release, did before it).</param>
    /// <param name="Controller">The player the server has on record as controlling the camera afterwards.</param>
    /// <param name="Sent">Everything the server sent in answer, to whomever.</param>
    internal readonly record struct ControlResult(bool Granted, SessionId? Controller, IReadOnlyList<Packet> Sent);

    /// <param name="Granted">Whether the server took the report over.</param>
    /// <param name="State">What the server holds for the camera after the report.</param>
    /// <param name="Sent">Everything the server sent in answer, to whomever.</param>
    internal readonly record struct DockResult(bool Granted, MapRoomCameraMetadata State, IReadOnlyList<Packet> Sent)
    {
        public int CameraNumber => State.CameraNumber;
    }

    /// <summary>
    ///     A camera entity as a client builds it: out in the world, or the child of a room if given one.
    /// </summary>
    internal static GlobalRootEntity CreateCamera(NitroxId cameraId, NitroxId? roomId = null, MapRoomCameraMetadata? state = null) => new(
        new NitroxTransform(),
        GlobalRootEntity.GLOBAL_ROOT_LEVEL,
        CAMERA_CLASS_ID,
        true,
        cameraId,
        new NitroxTechType("MapRoomCamera"),
        state,
        roomId,
        []);

    private static ServerPlayer CreatePlayer(ushort sessionId, string name) => new(
        (PeerId)(uint)sessionId,
        (SessionId)sessionId,
        name,
        false,
        null,
        NitroxVector3.Zero,
        NitroxQuaternion.Identity,
        new NitroxId($"00000000-0000-0000-0000-{sessionId:D12}"),
        Optional.Empty,
        Perms.PLAYER,
        new PlayerStatsData(45f, 45f, 100f, 100f, 100f, 0f),
        SubnauticaGameMode.SURVIVAL,
        Array.Empty<NitroxTechType>(),
        Array.Empty<Optional<NitroxId>>(),
        new Dictionary<string, NitroxId>(),
        new Dictionary<string, float>(),
        new Dictionary<string, PingInstancePreference>(),
        new List<int>(),
        false,
        false);
}
