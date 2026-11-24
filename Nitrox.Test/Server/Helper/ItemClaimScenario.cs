using Microsoft.Extensions.Logging.Abstractions;
using Nitrox.Model.DataStructures;
using Nitrox.Model.DataStructures.Unity;
using Nitrox.Model.Packets;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities;
using Nitrox.Server.Subnautica.Models.GameLogic;
using Nitrox.Server.Subnautica.Models.GameLogic.Entities;
using Nitrox.Server.Subnautica.Models.Packets.Core;
using Nitrox.Test.Server.GameLogic;
using NSubstitute;
using ServerPlayer = Nitrox.Server.Subnautica.Models.Player;

namespace Nitrox.Test.Server.Helper;

/// <summary>
///     Two connected players, each with a player entity, a locker and the server parts that decide who gets an item.
/// </summary>
internal sealed class ItemClaimScenario
{
    public const string TITANIUM_CLASS_ID = "41406e76-4b4c-4072-86f8-f5b8e6523b73";

    private TimeSpan now = TimeSpan.Zero;

    public ConnectedPlayers Players { get; } = new();
    public IPacketSender PacketSender { get; } = Substitute.For<IPacketSender>();
    public EntityRegistry EntityRegistry { get; } = new(NullLogger<EntityRegistry>.Instance);
    public SimulationOwnershipData Ownership { get; } = new();
    public WorldEntityManager WorldEntityManager { get; }
    public ScannerRoomManager ScannerRoomManager { get; }
    public ItemClaims ItemClaims { get; }
    public ServerPlayer PlayerA { get; }
    public ServerPlayer PlayerB { get; }
    public WorldEntity Locker { get; }

    public ItemClaimScenario()
    {
        PlayerA = Players.Connect("Alice");
        PlayerB = Players.Connect("Bob");
        foreach (ServerPlayer player in new[] { PlayerA, PlayerB })
        {
            EntityRegistry.AddOrUpdate(new PlayerEntity(new NitroxTransform(NitroxVector3.Zero, NitroxQuaternion.Identity, NitroxVector3.One), 0, null, false,
                                                        player.GameObjectId, NitroxTechType.None, null, null, []));
        }
        WorldEntityManager = new WorldEntityManager(PacketSender, EntityRegistry, null!, Players.PlayerManager, null!, NullLogger<WorldEntityManager>.Instance);
        ScannerRoomManager = ScannerRoomScenarioFixture.CreateManager(EntityRegistry, Ownership, new ScannerRoomDiagnostics(NullLogger<ScannerRoomDiagnostics>.Instance),
            PacketSender, Players.PlayerManager, WorldEntityManager);
        ItemClaims = new ItemClaims(EntityRegistry, Players.PlayerManager, () => now);
        Locker = new WorldEntity(new NitroxVector3(5, -5, 5), NitroxQuaternion.Identity, NitroxVector3.One, new NitroxTechType("SmallLocker"), 0, "locker", false, new NitroxId(), null);
        EntityRegistry.AddOrUpdate(Locker);
    }

    public void Wait(TimeSpan time)
    {
        now += time;
    }

    public AuthProcessorContext Context(ServerPlayer player) => new(player, PacketSender);

    public WorldEntity AddWorldItem()
    {
        WorldEntity item = new(new NitroxVector3(1, -2, 3), NitroxQuaternion.Identity, NitroxVector3.One, new NitroxTechType("Titanium"), 0, TITANIUM_CLASS_ID, false, new NitroxId(), null);
        EntityRegistry.AddOrUpdate(item);
        return item;
    }

    public InventoryItemEntity AddItemIn(NitroxId parentId)
    {
        InventoryItemEntity item = new(new NitroxId(), TITANIUM_CLASS_ID, new NitroxTechType("Titanium"), null, parentId, []);
        EntityRegistry.AddOrUpdate(item);
        return item;
    }

    public static InventoryItemEntity InInventoryOf(NitroxId itemId, ServerPlayer player) => new(itemId, TITANIUM_CLASS_ID, new NitroxTechType("Titanium"), null, player.GameObjectId, []);

    public List<T> SentTo<T>(ServerPlayer player) where T : Packet => ConnectedPlayers.SentTo<T>(PacketSender, player);

    public List<T> SentToOthersOf<T>(ServerPlayer player) where T : Packet
    {
        return PacketSender.ReceivedCalls()
                           .Where(call => call.GetMethodInfo().Name == nameof(IPacketSender.SendPacketToOthersAsync) && Equals(call.GetArguments()[1], player.SessionId))
                           .Select(call => call.GetArguments()[0])
                           .OfType<T>()
                           .ToList();
    }
}
