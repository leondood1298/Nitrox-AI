using System.Threading.Tasks;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities;
using Nitrox.Model.Subnautica.Packets;
using Nitrox.Test.Server.Helper;

namespace Nitrox.Server.Subnautica.Models.Packets.Processors;

[TestClass]
public sealed class PlayerItemsSecuredProcessorTest
{
    [TestMethod]
    public async Task EnteringABaseSecuresWhatThePlayerCarriesOnly()
    {
        ItemClaimScenario scenario = new();
        InventoryItemEntity carried = scenario.AddItemIn(scenario.PlayerA.GameObjectId);
        carried.DestroyOnDeath = true;
        InventoryItemEntity othersItem = scenario.AddItemIn(scenario.PlayerB.GameObjectId);
        othersItem.DestroyOnDeath = true;

        await new PlayerItemsSecuredProcessor(scenario.EntityRegistry).Process(scenario.Context(scenario.PlayerA), new PlayerItemsSecured());

        carried.DestroyOnDeath.Should().BeFalse();
        othersItem.DestroyOnDeath.Should().BeTrue();
    }
}
