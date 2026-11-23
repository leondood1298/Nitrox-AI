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
using NSubstitute;
using Packet = Nitrox.Model.Packets.Packet;
using ServerPlayer = Nitrox.Server.Subnautica.Models.Player;

namespace Nitrox.Test.Server.Subnautica;

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
    internal MapRoomEntity Room { get; }
    internal ServerPlayer PlayerA { get; } = CreatePlayer(1, "A");
    internal ServerPlayer PlayerB { get; } = CreatePlayer(2, "B");
    internal IReadOnlyList<NitroxId> CameraIds { get; } = [CameraAId, CameraBId, CameraCId];

    /// <param name="packetSender">For tests that need the sender to do more than record.</param>
    /// <param name="playerManager">For tests that need connected players; the default has none.</param>
    /// <param name="diagnosticsLog">For tests that read the lines the diagnostics write; these diagnostics also know the lock table.</param>
    internal ScannerRoomScenarioFixture(IPacketSender? packetSender = null, PlayerManager? playerManager = null, ILogger<ScannerRoomDiagnostics>? diagnosticsLog = null)
    {
        PacketSender = packetSender ?? Substitute.For<IPacketSender>();
        PlayerManager = playerManager ?? new PlayerManager(null!, null!, null!, NullLogger<PlayerManager>.Instance);
        EntityRegistry = new EntityRegistry(NullLogger<EntityRegistry>.Instance);
        Room = new MapRoomEntity(RoomId, RoomParentId, new NitroxInt3(4, -2, 9));
        EntityRegistry.AddEntity(Room);
        foreach (NitroxId cameraId in CameraIds)
        {
            EntityRegistry.AddEntity(CreateCamera(cameraId));
        }

        Diagnostics = diagnosticsLog != null ? new ScannerRoomDiagnostics(diagnosticsLog, Ownership) : new ScannerRoomDiagnostics(NullLogger<ScannerRoomDiagnostics>.Instance);
        // Batch spawning and progress reporting are not part of these scenarios.
        WorldEntityManager = new WorldEntityManager(PacketSender, EntityRegistry, null!, PlayerManager, null!, NullLogger<WorldEntityManager>.Instance);
        Manager = CreateManager(EntityRegistry, Ownership, Diagnostics, PacketSender, PlayerManager, WorldEntityManager);
        Manager.RoomBuilt(Room);
        EntitySimulation = new EntitySimulation(PacketSender, EntityRegistry, WorldEntityManager, Ownership, PlayerManager, Manager, NullLogger<EntitySimulation>.Instance);
        ownershipRequestProcessor = new(Ownership, EntitySimulation, EntityRegistry, Manager);
        BuildingManager buildingManager = new(PacketSender, EntityRegistry, WorldEntityManager,
            Options.Create(new SubnauticaServerOptions()), Manager, NullLogger<BuildingManager>.Instance);
        placeBaseProcessor = new(buildingManager, EntitySimulation);
        updateBaseProcessor = new(buildingManager, EntitySimulation);
        destroyProcessor = new(PlayerManager, EntitySimulation, WorldEntityManager, Manager);
        // Base power sources are not part of these scenarios.
        metadataProcessor = new(PlayerManager, EntityRegistry, Ownership, Manager, null!, NullLogger<EntityMetadataUpdateProcessor>.Instance);
        pickupProcessor = new(EntityRegistry, WorldEntityManager, Ownership, Manager);
        spawnProcessor = new(PlayerManager, EntityRegistry, WorldEntityManager, EntitySimulation, Manager);
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

    internal async Task<MapRoomCameraDock> DockAsync(ServerPlayer player, NitroxId cameraId, int slot, bool isDocked,
        bool establishAuthority = true)
    {
        bool alreadyCanonical = ScannerRoomManager.IsRequestedStateCanonical(isDocked, cameraId, Room.GetDockedCamera(slot), Room.IsCameraDocked(cameraId));
        if (establishAuthority && !alreadyCanonical)
        {
            // Docking assumes normal simulation assignment already made the actor the camera authority. Establish
            // that production precondition here.
            Ownership.RevokeOwnerOfId(cameraId);
            Assert.IsTrue(Ownership.TryAcquire(cameraId, player, SimulationLockType.TRANSIENT));
        }
        int alreadySent = PacketSender.ReceivedCalls().Count();
        MapRoomCameraDock request = new(cameraId, RoomId, slot, isDocked: isDocked);
        await (isDocked ? Manager.DockCameraAsync(player, request) : Manager.UndockCameraAsync(player, request));
        return Answer<MapRoomCameraDock>(alreadySent, player, packet => packet.CameraId == cameraId && packet.DockingIndex == slot && packet.IsDocked == isDocked);
    }

    /// <summary>
    ///     Everything the server sends in answer to a dock packet, sent as is: no authority is set up for the sender.
    /// </summary>
    internal Task<IReadOnlyList<Packet>> DockPacketsAsync(ServerPlayer player, MapRoomCameraDock packet) =>
        SentDuringAsync(() => packet.IsDocked ? Manager.DockCameraAsync(player, packet) : Manager.UndockCameraAsync(player, packet));

    internal Task<MapRoomCameraControl> ControlAsync(ServerPlayer player, NitroxId cameraId, int slot, bool isControlling) =>
        ControlAnswerAsync(player, new MapRoomCameraControl(cameraId, RoomId, slot, isControlling, false));

    internal Task<MapRoomCameraControl> ControlLooseAsync(ServerPlayer player, NitroxId cameraId, bool isControlling) =>
        ControlAnswerAsync(player, new MapRoomCameraControl(cameraId, Optional.Empty, -1, isControlling, false));

    /// <summary>
    ///     Taking a camera can also end control of the camera the player had before, which is announced in the same go.
    /// </summary>
    private async Task<MapRoomCameraControl> ControlAnswerAsync(ServerPlayer player, MapRoomCameraControl request)
    {
        int alreadySent = PacketSender.ReceivedCalls().Count();
        await Manager.SetControlAsync(player, request);
        return Answer<MapRoomCameraControl>(alreadySent, player, packet => packet.CameraId == request.CameraId && packet.IsControlling == request.IsControlling);
    }

    /// <summary>
    ///     The answer to a request of <paramref name="player" /> among what was sent since it was made. Other requests
    ///     may have been answered in that time: a refusal goes to the player who asked alone, anything else to everyone
    ///     and names the player it is about.
    /// </summary>
    private T Answer<T>(int alreadySent, ServerPlayer player, Func<T, bool> isAnswer) where T : Packet
    {
        List<NSubstitute.Core.ICall> calls = PacketSender.ReceivedCalls().Skip(alreadySent).ToList();
        List<T> refusals = calls.Where(call => call.GetMethodInfo().Name == nameof(IPacketSender.SendPacketAsync) && Equals(call.GetArguments()[1], player.SessionId))
                                .Select(call => call.GetArguments()[0]).OfType<T>().Where(isAnswer).ToList();
        if (refusals.Count > 0)
        {
            return refusals.Single();
        }
        return calls.Where(call => call.GetMethodInfo().Name == nameof(IPacketSender.SendPacketToAllAsync))
                    .Select(call => call.GetArguments()[0]).OfType<T>()
                    .Single(packet => isAnswer(packet) && (packet is not MapRoomCameraControl control || control.ControllerSessionId == player.SessionId));
    }

    /// <summary>
    ///     Everything the server sends in answer to a control packet, for tests that look beyond the control response.
    /// </summary>
    internal Task<IReadOnlyList<Packet>> ControlPacketsAsync(ServerPlayer player, MapRoomCameraControl packet) =>
        SentDuringAsync(() => Manager.SetControlAsync(player, packet));

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
    ///     Finishes a piece on an existing base, the way the builder's client reports it.
    /// </summary>
    internal Task<IReadOnlyList<Packet>> UpdateBaseAsync(ServerPlayer player, UpdateBase packet) =>
        SentDuringAsync(() => updateBaseProcessor.Process(Context(player), packet));

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
    internal async Task<IReadOnlyList<MapRoomCameraControl>> JoinedPlayerControlGrantsAsync(SessionId joinedSessionId) =>
        (await SentDuringAsync(() => Manager.SendControlGrantsAsync(joinedSessionId))).Cast<MapRoomCameraControl>().ToList();

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

    internal async Task<SimulationOwnershipResponse> RequestOwnershipAsync(ServerPlayer player, NitroxId entityId,
        SimulationLockType lockType) =>
        (await SentDuringAsync(() => ownershipRequestProcessor.Process(Context(player), new SimulationOwnershipRequest(player.SessionId, entityId, lockType))))
        .OfType<SimulationOwnershipResponse>().Single();

    /// <summary>
    ///     Everything the server sends in answer to a camera light packet.
    /// </summary>
    internal Task<IReadOnlyList<Packet>> LightAsync(ServerPlayer player, MapRoomCameraLight packet) =>
        SentDuringAsync(() => Manager.SetCameraLightAsync(player, packet));

    /// <summary>
    ///     Everything the server sends in answer to a camera battery and health packet.
    /// </summary>
    internal Task<IReadOnlyList<Packet>> ComponentsAsync(ServerPlayer player, MapRoomCameraComponentState packet) =>
        SentDuringAsync(() => Manager.SetCameraComponentsAsync(player, packet));

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

    internal string Snapshot()
    {
        string registrations = string.Join(",", Room.CameraRegistry
            .OrderBy(record => record.CameraNumber)
            .Select(record => $"{record.CameraNumber}:{record.CameraId}"));
        return $"rev={Room.DockingRevision};left={Room.LeftDockCameraId};right={Room.RightDockCameraId};registry={registrations}";
    }

    internal void AssertCanonicalInvariants()
    {
        NitroxId[] dockedIds = new[] { Room.LeftDockCameraId, Room.RightDockCameraId }
            .Where(id => id != null)
            .Select(id => id!)
            .ToArray();

        Assert.AreEqual(dockedIds.Length, dockedIds.Distinct().Count(), Snapshot());
        Assert.AreEqual(Room.CameraRegistry.Count, Room.CameraRegistry.Select(record => record.CameraId).Distinct().Count(), Snapshot());
        Assert.AreEqual(Room.CameraRegistry.Count, Room.CameraRegistry.Select(record => record.CameraNumber).Distinct().Count(), Snapshot());
        Assert.IsTrue(Room.CameraRegistry.All(record => record.CameraNumber > 0), Snapshot());
        Assert.IsTrue(dockedIds.All(id => Room.GetCameraRecord(id) != null), Snapshot());
    }

    private static GlobalRootEntity CreateCamera(NitroxId cameraId) => new(
        new NitroxTransform(),
        GlobalRootEntity.GLOBAL_ROOT_LEVEL,
        CAMERA_CLASS_ID,
        true,
        cameraId,
        new NitroxTechType("MapRoomCamera"),
        null,
        null,
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
