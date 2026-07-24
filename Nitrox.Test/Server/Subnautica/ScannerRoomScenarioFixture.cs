using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Nitrox.Model.Core;
using Nitrox.Model.DataStructures;
using Nitrox.Model.DataStructures.GameLogic;
using Nitrox.Model.DataStructures.Unity;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Bases;
using Nitrox.Model.Subnautica.Packets;
using Nitrox.Server.Subnautica.Models.GameLogic;
using Nitrox.Server.Subnautica.Models.GameLogic.Entities;
using Nitrox.Server.Subnautica.Models.Packets.Core;
using Nitrox.Server.Subnautica.Models.Packets.Processors;
using Packet = Nitrox.Model.Packets.Packet;
using ServerPlayer = Nitrox.Server.Subnautica.Models.Player;

namespace Nitrox.Test.Server.Subnautica;

/// <summary>
/// Deterministic in-process Scanner Room server fixture. It deliberately drives the real packet
/// processors and canonical state stores while replacing only network delivery with a recorder.
/// </summary>
internal sealed class ScannerRoomScenarioFixture
{
    internal static readonly NitroxId RoomId = new("00000000-0000-0000-0000-000000000100");
    internal static readonly NitroxId RoomParentId = new("00000000-0000-0000-0000-000000000101");
    internal static readonly NitroxId CameraAId = new("00000000-0000-0000-0000-000000000201");
    internal static readonly NitroxId CameraBId = new("00000000-0000-0000-0000-000000000202");
    internal static readonly NitroxId CameraCId = new("00000000-0000-0000-0000-000000000203");

    private readonly MapRoomCameraDockProcessor dockProcessor;
    private readonly MapRoomCameraControlProcessor controlProcessor;
    private readonly MapRoomDeconstructionCleanup deconstructionCleanup;
    private readonly SimulationOwnershipRequestProcessor ownershipRequestProcessor;

    internal EntityRegistry EntityRegistry { get; }
    internal SimulationOwnershipData Ownership { get; } = new();
    internal ScannerRoomDiagnostics Diagnostics { get; }
    internal MapRoomCameraControlReleaseFactory ControlReleaseFactory { get; }
    internal MapRoomLifecycle RoomLifecycle { get; } = new();
    internal MapRoomCameraControlLifecycle ControlLifecycle { get; } = new();
    internal EntitySimulation EntitySimulation { get; }
    internal MapRoomEntity Room { get; }
    internal ServerPlayer PlayerA { get; } = CreatePlayer(1, "A");
    internal ServerPlayer PlayerB { get; } = CreatePlayer(2, "B");
    internal IReadOnlyList<NitroxId> CameraIds { get; } = [CameraAId, CameraBId, CameraCId];

    internal ScannerRoomScenarioFixture()
    {
        EntityRegistry = new EntityRegistry(NullLogger<EntityRegistry>.Instance);
        Room = new MapRoomEntity(RoomId, RoomParentId, new NitroxInt3(4, -2, 9));
        EntityRegistry.AddEntity(Room);
        foreach (NitroxId cameraId in CameraIds)
        {
            EntityRegistry.AddEntity(CreateCamera(cameraId));
        }

        // Dock processing only uses the world manager when it must persist a previously untracked
        // loose camera. All fixture cameras are registered, so unrelated world services stay absent.
        RecordingPacketSender worldPacketSender = new();
        Diagnostics = new ScannerRoomDiagnostics(NullLogger<ScannerRoomDiagnostics>.Instance);
        ControlReleaseFactory = new MapRoomCameraControlReleaseFactory(EntityRegistry, Diagnostics, ControlLifecycle);
        WorldEntityManager worldEntityManager = new(worldPacketSender, EntityRegistry, null!, null!, null!, Diagnostics, NullLogger<WorldEntityManager>.Instance);
        dockProcessor = new(EntityRegistry, worldEntityManager, Ownership, ControlReleaseFactory, RoomLifecycle,
            ControlLifecycle, Diagnostics, NullLogger<MapRoomCameraDockProcessor>.Instance);
        controlProcessor = new(Ownership, EntityRegistry, ControlLifecycle, Diagnostics);
        PlayerManager playerManager = new(null!, null!, NullLogger<PlayerManager>.Instance);
        deconstructionCleanup = new(EntityRegistry, Ownership, ControlReleaseFactory,
            new MapRoomScanResultSubscriptions(), ControlLifecycle, playerManager, Diagnostics);
        EntitySimulation = new(worldPacketSender, EntityRegistry, worldEntityManager, Ownership,
            playerManager, ControlReleaseFactory, ControlLifecycle, NullLogger<EntitySimulation>.Instance);
        ownershipRequestProcessor = new(Ownership, EntitySimulation, ControlReleaseFactory, ControlLifecycle, Diagnostics);
    }

    internal async Task<MapRoomCameraDock> DockAsync(ServerPlayer player, NitroxId cameraId, int slot, bool isDocked,
        bool establishAuthority = true)
    {
        bool alreadyCanonical;
        lock (Room)
        {
            alreadyCanonical = MapRoomCameraDockProcessor.IsRequestedStateCanonical(isDocked, cameraId,
                Room.GetDockedCamera(slot), Room.IsCameraDocked(cameraId));
        }
        if (establishAuthority && !alreadyCanonical)
        {
            // Packet processing assumes normal simulation assignment already made the actor the
            // camera authority. Establish that production precondition without mocking the processor.
            Ownership.RevokeOwnerOfId(cameraId);
            Assert.IsTrue(Ownership.TryToAcquire(cameraId, player, SimulationLockType.TRANSIENT));
        }
        RecordingPacketSender requestPacketSender = new();
        await dockProcessor.Process(new AuthProcessorContext(player, requestPacketSender), new MapRoomCameraDock(cameraId, RoomId, slot, isDocked: isDocked));
        return requestPacketSender.Single<MapRoomCameraDock>();
    }

    internal async Task<MapRoomCameraControl> ControlAsync(ServerPlayer player, NitroxId cameraId, int slot, bool isControlling)
    {
        RecordingPacketSender requestPacketSender = new();
        await controlProcessor.Process(new AuthProcessorContext(player, requestPacketSender), new MapRoomCameraControl(cameraId, RoomId, slot, isControlling, false));
        return requestPacketSender.Single<MapRoomCameraControl>();
    }

    internal async Task<MapRoomCameraControl> ControlLooseAsync(ServerPlayer player, NitroxId cameraId, bool isControlling)
    {
        RecordingPacketSender requestPacketSender = new();
        await controlProcessor.Process(new AuthProcessorContext(player, requestPacketSender),
            new MapRoomCameraControl(cameraId, Optional.Empty, -1, isControlling, false));
        return requestPacketSender.Single<MapRoomCameraControl>();
    }

    internal async Task<IReadOnlyList<Packet>> CleanupAsync(MapRoomEntity room)
    {
        RecordingPacketSender requestPacketSender = new();
        await deconstructionCleanup.CleanupAsync(room, new AuthProcessorContext(PlayerA, requestPacketSender));
        return requestPacketSender.Packets;
    }

    internal async Task<SimulationOwnershipResponse> RequestOwnershipAsync(ServerPlayer player, NitroxId entityId,
        SimulationLockType lockType)
    {
        RecordingPacketSender requestPacketSender = new();
        await ownershipRequestProcessor.Process(new AuthProcessorContext(player, requestPacketSender),
            new SimulationOwnershipRequest(player.SessionId, entityId, lockType));
        return requestPacketSender.Single<SimulationOwnershipResponse>();
    }

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
        "733fd479-0760-4bc2-a03e-281cbf02bfa4",
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

    private sealed class RecordingPacketSender : IPacketSender
    {
        private readonly List<Packet> packets = [];

        public ValueTask SendPacketAsync<T>(T packet, SessionId sessionId) where T : Packet
        {
            packets.Add(packet);
            return ValueTask.CompletedTask;
        }

        public ValueTask SendPacketToAllAsync<T>(T packet) where T : Packet
        {
            packets.Add(packet);
            return ValueTask.CompletedTask;
        }

        public ValueTask SendPacketToOthersAsync<T>(T packet, SessionId excludedSessionId) where T : Packet
        {
            packets.Add(packet);
            return ValueTask.CompletedTask;
        }

        internal IReadOnlyList<Packet> Packets => packets;

        internal T Single<T>() where T : Packet => packets.OfType<T>().Single();
    }
}
