using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities;
using Nitrox.Model.Subnautica.Packets;
using Nitrox.Test.Server.Helper;

namespace Nitrox.Server.Subnautica.Models.Packets.Processors;

[TestClass]
public sealed class EntityReparentedProcessorTest
{
    [TestMethod]
    public async Task OfTwoPlayersTakingTheSameItemOutOfALockerTheFirstGetsIt()
    {
        ItemClaimScenario scenario = new();
        EntityReparentedProcessor processor = new(scenario.EntityRegistry, scenario.ItemClaims, NullLogger<EntityReparentedProcessor>.Instance);
        InventoryItemEntity item = scenario.AddItemIn(scenario.Locker.Id);

        await processor.Process(scenario.Context(scenario.PlayerA), new EntityReparented(item.Id, scenario.PlayerA.GameObjectId));
        await processor.Process(scenario.Context(scenario.PlayerB), new EntityReparented(item.Id, scenario.PlayerB.GameObjectId));

        item.ParentId.Should().Be(scenario.PlayerA.GameObjectId);
        // Bob's game moves the item to where it is: into Alice's hands
        EntityReparented correction = scenario.SentTo<EntityReparented>(scenario.PlayerB).Single();
        correction.Id.Should().Be(item.Id);
        correction.NewParentId.Should().Be(scenario.PlayerA.GameObjectId);
        scenario.SentTo<ItemClaimLost>(scenario.PlayerB).Single().WinnerName.Should().Be("Alice");
        scenario.SentToOthersOf<EntityReparented>(scenario.PlayerB).Should().BeEmpty();
    }

    [TestMethod]
    public async Task APlayerMovesTheirOwnItemIntoALocker()
    {
        ItemClaimScenario scenario = new();
        EntityReparentedProcessor processor = new(scenario.EntityRegistry, scenario.ItemClaims, NullLogger<EntityReparentedProcessor>.Instance);
        InventoryItemEntity item = scenario.AddItemIn(scenario.PlayerA.GameObjectId);

        await processor.Process(scenario.Context(scenario.PlayerA), new EntityReparented(item.Id, scenario.Locker.Id));

        item.ParentId.Should().Be(scenario.Locker.Id);
        scenario.SentToOthersOf<EntityReparented>(scenario.PlayerA).Should().ContainSingle();
    }
}
