using System.Threading.Tasks;
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
using Nitrox.Server.Subnautica.Models.Communication;
using Nitrox.Server.Subnautica.Models.GameLogic;
using Nitrox.Server.Subnautica.Models.GameLogic.Bases;
using Nitrox.Server.Subnautica.Models.GameLogic.Entities;
using Nitrox.Server.Subnautica.Models.Packets.Core;
using NSubstitute;
using Packet = Nitrox.Model.Packets.Packet;
using ServerPlayer = Nitrox.Server.Subnautica.Models.Player;

namespace Nitrox.Test.Server.GameLogic.Bases;

/// <summary>
///     A server with a real entity registry, lock table and <see cref="BuildingManager" />, and one base. Only network
///     delivery is replaced by a substitute that records what was sent.
/// </summary>
internal sealed class BuildingScenarioFixture
{
    internal static readonly NitroxId BaseId = new("00000000-0000-0000-0000-000000000500");

    internal IPacketSender PacketSender { get; } = Substitute.For<IPacketSender>();
    internal EntityRegistry EntityRegistry { get; } = new(NullLogger<EntityRegistry>.Instance);
    internal SimulationOwnershipData Ownership { get; } = new();
    internal PlayerManager PlayerManager { get; } = new(null!, null!, null!, NullLogger<PlayerManager>.Instance);
    internal WorldEntityManager WorldEntityManager { get; }
    internal ScannerRoomManager ScannerRoomManager { get; }
    internal EntitySimulation EntitySimulation { get; }
    internal BuildingManager BuildingManager { get; }
    internal BuildEntity Base { get; }
    internal ServerPlayer PlayerA { get; } = CreatePlayer(1, "A");
    internal ServerPlayer PlayerB { get; } = CreatePlayer(2, "B");

    internal BuildingScenarioFixture()
    {
        WorldEntityManager = new WorldEntityManager(PacketSender, EntityRegistry, null!, PlayerManager, null!, NullLogger<WorldEntityManager>.Instance);
        ScannerRoomManager = ScannerRoomScenarioFixture.CreateManager(EntityRegistry, Ownership, new ScannerRoomDiagnostics(NullLogger<ScannerRoomDiagnostics>.Instance),
            PacketSender, PlayerManager, WorldEntityManager);
        EntitySimulation = new EntitySimulation(PacketSender, EntityRegistry, WorldEntityManager, Ownership, PlayerManager, ScannerRoomManager, NullLogger<EntitySimulation>.Instance);
        BuildingManager = new BuildingManager(PacketSender, EntityRegistry, WorldEntityManager, Options.Create(new SubnauticaServerOptions()), ScannerRoomManager,
            NullLogger<BuildingManager>.Instance);

        Base = BuildEntity.MakeEmpty();
        Base.Id = BaseId;
        Base.BaseData = new BaseData();
        WorldEntityManager.AddOrUpdateGlobalRootEntity(Base);
    }

    internal T Add<T>(T entity) where T : GlobalRootEntity
    {
        WorldEntityManager.AddOrUpdateGlobalRootEntity(entity);
        return entity;
    }

    internal InteriorPieceEntity AddInteriorPiece(NitroxId id, string techType, params Entity[] children)
    {
        InteriorPieceEntity piece = InteriorPieceEntity.MakeEmpty();
        piece.Id = id;
        piece.ParentId = BaseId;
        piece.TechType = new NitroxTechType(techType);
        piece.Transform = new NitroxTransform();
        foreach (Entity child in children)
        {
            child.ParentId = id;
            piece.ChildEntities.Add(child);
        }
        return Add(piece);
    }

    internal GhostEntity AddGhost(NitroxId id, NitroxId? parentId = null)
    {
        GhostEntity ghost = GhostEntity.MakeEmpty();
        ghost.Id = id;
        ghost.ParentId = parentId ?? BaseId;
        ghost.Transform = new NitroxTransform();
        return Add(ghost);
    }

    internal static GhostEntity Ghost(NitroxId id, NitroxId? parentId = null)
    {
        GhostEntity ghost = GhostEntity.MakeEmpty();
        ghost.Id = id;
        ghost.ParentId = parentId ?? BaseId;
        ghost.Transform = new NitroxTransform();
        return ghost;
    }

    internal static InventoryItemEntity Item(string id, string techType, NitroxId? parentId = null) =>
        new(new NitroxId(id), "classId", new NitroxTechType(techType), null, parentId, []);

    internal IReadOnlyList<Packet> SentPackets() => PacketSender.ReceivedCalls().Select(call => call.GetArguments()[0]).OfType<Packet>().ToList();

    internal List<T> SentTo<T>(ServerPlayer player) where T : Packet =>
        PacketSender.ReceivedCalls()
                    .Where(call => call.GetMethodInfo().Name == nameof(IPacketSender.SendPacketAsync) && Equals(call.GetArguments()[1], player.SessionId))
                    .Select(call => call.GetArguments()[0])
                    .OfType<T>()
                    .ToList();

    internal AuthProcessorContext Context(ServerPlayer player) => new(player, PacketSender);

    internal async Task<IReadOnlyList<Packet>> SentDuringAsync(Func<Task> action)
    {
        int alreadySent = SentPackets().Count;
        await action();
        return SentPackets().Skip(alreadySent).ToList();
    }

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
