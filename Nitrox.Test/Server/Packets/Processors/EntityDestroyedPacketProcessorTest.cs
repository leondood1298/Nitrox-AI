using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities;
using Nitrox.Model.Subnautica.Packets;
using Nitrox.Server.Subnautica.Models.GameLogic;
using Nitrox.Server.Subnautica.Models.GameLogic.Entities;
using Nitrox.Test.Server.Helper;

namespace Nitrox.Server.Subnautica.Models.Packets.Processors;

[TestClass]
public sealed class EntityDestroyedPacketProcessorTest
{
    [TestMethod]
    public async Task APlayerCannotDestroyAnItemAnotherPlayerHolds()
    {
        ItemClaimScenario scenario = new();
        EntityDestroyedPacketProcessor processor = CreateProcessor(scenario);
        InventoryItemEntity item = scenario.AddItemIn(scenario.PlayerA.GameObjectId);

        // Bob's game ate the item before it heard that Alice got it first
        await processor.Process(scenario.Context(scenario.PlayerB), new EntityDestroyed(item.Id));

        scenario.EntityRegistry.GetEntityById(item.Id).HasValue.Should().BeTrue();
        scenario.SentTo<SpawnEntities>(scenario.PlayerB).Single().Entities.Single().Id.Should().Be(item.Id);
    }

    [TestMethod]
    public async Task APlayerDestroysTheirOwnItem()
    {
        ItemClaimScenario scenario = new();
        EntityDestroyedPacketProcessor processor = CreateProcessor(scenario);
        InventoryItemEntity item = scenario.AddItemIn(scenario.PlayerA.GameObjectId);

        await processor.Process(scenario.Context(scenario.PlayerA), new EntityDestroyed(item.Id));

        scenario.EntityRegistry.GetEntityById(item.Id).HasValue.Should().BeFalse();
    }

    private static EntityDestroyedPacketProcessor CreateProcessor(ItemClaimScenario scenario)
    {
        EntitySimulation entitySimulation = new(scenario.PacketSender, scenario.EntityRegistry, scenario.WorldEntityManager, scenario.Ownership, scenario.Players.PlayerManager,
            scenario.ScannerRoomManager, NullLogger<EntitySimulation>.Instance);
        return new EntityDestroyedPacketProcessor(scenario.Players.PlayerManager, entitySimulation, scenario.WorldEntityManager, scenario.ScannerRoomManager, scenario.EntityRegistry,
            scenario.ItemClaims);
    }
}
