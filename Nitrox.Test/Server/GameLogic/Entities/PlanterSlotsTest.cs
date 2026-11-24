using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;

namespace Nitrox.Server.Subnautica.Models.GameLogic.Entities;

[TestClass]
public sealed class PlanterSlotsTest
{
    private static readonly NitroxId planterId = new();

    [TestMethod]
    public void SlotHeldByAnotherSeedIsTaken()
    {
        InventoryItemEntity first = Seed(0);
        InventoryItemEntity second = Seed(0);

        PlanterSlots.IsTaken([first], second).Should().BeTrue();
        PlanterSlots.IsTaken([first], Seed(1)).Should().BeFalse();
    }

    [TestMethod]
    public void SeedPlantedAgainInItsOwnSlotDoesNotTakeItFromItself()
    {
        InventoryItemEntity seed = Seed(2);

        PlanterSlots.IsTaken([seed], new InventoryItemEntity(seed.Id, seed.ClassId, seed.TechType, new PlantableMetadata(9f, 2), planterId, [], false)).Should().BeFalse();
    }

    [TestMethod]
    public void AnythingButAPlantedSeedIsLeftAlone()
    {
        InventoryItemEntity plant = Seed(0);
        InventoryItemEntity storedItem = new(new NitroxId(), "item", new NitroxTechType("Titanium"), null, planterId, [], false);

        PlanterSlots.IsTaken([plant], storedItem).Should().BeFalse();
    }

    private static InventoryItemEntity Seed(int slot) => new(new NitroxId(), "seed", new NitroxTechType("MelonSeed"), new PlantableMetadata(5f, slot), planterId, [], false);
}
