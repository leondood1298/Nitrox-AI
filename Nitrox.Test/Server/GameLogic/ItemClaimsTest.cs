using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;
using Nitrox.Server.Subnautica.Models.GameLogic;
using Nitrox.Test.Server.Helper;

namespace Nitrox.Test.Server.GameLogic;

[TestClass]
public sealed class ItemClaimsTest
{
    private ItemClaimScenario scenario;

    [TestInitialize]
    public void Init()
    {
        scenario = new ItemClaimScenario();
    }

    [TestMethod]
    public void AnItemInAnotherPlayersInventoryIsHeldByThem()
    {
        InventoryItemEntity item = scenario.AddItemIn(scenario.PlayerA.GameObjectId);

        scenario.ItemClaims.TryGetOtherHolder(item, scenario.PlayerB, out string? holder).Should().BeTrue();
        holder.Should().Be("Alice");
        scenario.ItemClaims.TryGetOtherHolder(item, scenario.PlayerA, out _).Should().BeFalse();
    }

    [TestMethod]
    public void AnItemInAContainerInAnInventoryIsHeldByThatPlayer()
    {
        InventoryItemEntity bag = scenario.AddItemIn(scenario.PlayerA.GameObjectId);
        InventoryItemEntity battery = scenario.AddItemIn(bag.Id);

        scenario.ItemClaims.TryGetOtherHolder(battery, scenario.PlayerB, out string? holder).Should().BeTrue();
        holder.Should().Be("Alice");
    }

    [TestMethod]
    public void AnItemInALockerOrTheWorldIsHeldByNobody()
    {
        scenario.ItemClaims.TryGetOtherHolder(scenario.AddItemIn(scenario.Locker.Id), scenario.PlayerB, out _).Should().BeFalse();
        scenario.ItemClaims.TryGetOtherHolder(scenario.AddWorldItem(), scenario.PlayerB, out _).Should().BeFalse();
    }

    [TestMethod]
    public void APlayerDoesNotHoldItself()
    {
        Entity playerA = scenario.EntityRegistry.GetEntityById(scenario.PlayerA.GameObjectId).Value;

        scenario.ItemClaims.TryGetOtherHolder(playerA, scenario.PlayerB, out _).Should().BeFalse();
    }

    [TestMethod]
    public void TheFirstClaimOnASourceWins()
    {
        ItemSource medkit = new(new NitroxId(), "medkit");

        scenario.ItemClaims.TryClaimSource(medkit, scenario.PlayerA, out _, out _).Should().BeTrue();
        scenario.ItemClaims.TryClaimSource(medkit, scenario.PlayerB, out string? winner, out bool reserved).Should().BeFalse();
        winner.Should().Be("Alice");
        reserved.Should().BeFalse();
        // The winner's other items from the same source (an outcrop's resources) are its own
        scenario.ItemClaims.TryClaimSource(medkit, scenario.PlayerA, out _, out _).Should().BeTrue();
    }

    [TestMethod]
    public void DifferentThingsFromOneSourceDoNotCompete()
    {
        NitroxId plant = new();

        scenario.ItemClaims.TryClaimSource(new ItemSource(plant, "fruit0"), scenario.PlayerA, out _, out _).Should().BeTrue();
        scenario.ItemClaims.TryClaimSource(new ItemSource(plant, "fruit1"), scenario.PlayerB, out _, out _).Should().BeTrue();
    }

    [TestMethod]
    public void ASourceCanGiveAgainOnceTheClaimIsForgotten()
    {
        ItemSource medkit = new(new NitroxId(), "medkit");
        scenario.ItemClaims.TryClaimSource(medkit, scenario.PlayerA, out _, out _).Should().BeTrue();

        scenario.Wait(ItemClaims.SOURCE_CLAIM_LIFETIME + TimeSpan.FromSeconds(1));

        scenario.ItemClaims.TryClaimSource(medkit, scenario.PlayerB, out _, out _).Should().BeTrue();
    }

    [TestMethod]
    public void ACraftsResultIsItsCraftersForTenSecondsOnceDone()
    {
        NitroxId fabricator = new();
        CrafterMetadata start = new(new NitroxTechType("Titanium"), 100f, 3f, 1, -1);
        scenario.ItemClaims.AcceptCrafterUpdate(fabricator, null, start, scenario.PlayerA).Should().BeTrue();
        ItemSource result = new(fabricator, "craft100:-1:1");

        scenario.Wait(TimeSpan.FromSeconds(3 + 9));
        scenario.ItemClaims.TryClaimSource(result, scenario.PlayerB, out string? crafter, out bool reserved).Should().BeFalse();
        crafter.Should().Be("Alice");
        reserved.Should().BeTrue();
        // Bob's game took the result: emptying the fabricator is not kept
        scenario.ItemClaims.AcceptCrafterUpdate(fabricator, start, new CrafterMetadata(new NitroxTechType("None"), 100f, 0f, 0, 0), scenario.PlayerB).Should().BeFalse();
    }

    [TestMethod]
    public void ACraftsResultIsTheFirstTakersAfterTenSeconds()
    {
        NitroxId fabricator = new();
        scenario.ItemClaims.AcceptCrafterUpdate(fabricator, null, new CrafterMetadata(new NitroxTechType("Titanium"), 100f, 3f, 1, -1), scenario.PlayerA).Should().BeTrue();

        scenario.Wait(TimeSpan.FromSeconds(3 + 11));

        scenario.ItemClaims.TryClaimSource(new ItemSource(fabricator, "craft100:-1:1"), scenario.PlayerB, out _, out _).Should().BeTrue();
        scenario.ItemClaims.TryClaimSource(new ItemSource(fabricator, "craft100:-1:1"), scenario.PlayerA, out string? winner, out _).Should().BeFalse();
        winner.Should().Be("Bob");
    }

    [TestMethod]
    public void ASecondCraftCannotStartOnABusyFabricator()
    {
        NitroxId fabricator = new();
        CrafterMetadata first = new(new NitroxTechType("Titanium"), 100f, 3f, 1, -1);
        scenario.ItemClaims.AcceptCrafterUpdate(fabricator, null, first, scenario.PlayerA).Should().BeTrue();

        scenario.ItemClaims.AcceptCrafterUpdate(fabricator, first, new CrafterMetadata(new NitroxTechType("Glass"), 100.5f, 3f, 1, -1), scenario.PlayerB).Should().BeFalse();
    }

    [TestMethod]
    public void ACraftCanStartOnceTheResultIsTaken()
    {
        NitroxId fabricator = new();
        CrafterMetadata taken = new(new NitroxTechType("None"), 100f, 0f, 0, 0);

        scenario.ItemClaims.AcceptCrafterUpdate(fabricator, taken, new CrafterMetadata(new NitroxTechType("Glass"), 200f, 3f, 1, -1), scenario.PlayerB).Should().BeTrue();
    }

    [TestMethod]
    public void OneModuleSlotHoldsOneModule()
    {
        InstalledModuleEntity first = new("Module1", "module", new NitroxId(), new NitroxTechType("VehicleArmorPlating"), null, scenario.Locker.Id, []);
        scenario.ItemClaims.TryTakeSlot(first, scenario.PlayerA, out _).Should().BeTrue();
        scenario.EntityRegistry.AddOrUpdate(first);

        InstalledModuleEntity second = new("Module1", "module", new NitroxId(), new NitroxTechType("VehicleArmorPlating"), null, scenario.Locker.Id, []);
        scenario.ItemClaims.TryTakeSlot(second, scenario.PlayerB, out string? installer).Should().BeFalse();
        installer.Should().Be("Alice");
        InstalledModuleEntity otherSlot = new("Module2", "module", new NitroxId(), new NitroxTechType("VehicleArmorPlating"), null, scenario.Locker.Id, []);
        scenario.ItemClaims.TryTakeSlot(otherSlot, scenario.PlayerB, out _).Should().BeTrue();
    }

    [TestMethod]
    public void OneBatterySlotHoldsOneBattery()
    {
        InstalledBatteryEntity first = new(0, new NitroxId(), new NitroxTechType("PowerCell"), null, scenario.Locker.Id, []);
        scenario.EntityRegistry.AddOrUpdate(first);

        scenario.ItemClaims.TryTakeBatterySlot(new InstalledBatteryEntity(0, new NitroxId(), new NitroxTechType("PowerCell"), null, scenario.Locker.Id, []), out _).Should().BeFalse();
        scenario.ItemClaims.TryTakeBatterySlot(new InstalledBatteryEntity(1, new NitroxId(), new NitroxTechType("PowerCell"), null, scenario.Locker.Id, []), out _).Should().BeTrue();
        scenario.ItemClaims.TryTakeBatterySlot(first, out _).Should().BeTrue();
    }

    [TestMethod]
    public void APickedFruitCountsAsPickedLatelyForAMoment()
    {
        TimeSpan now = TimeSpan.Zero;
        ItemClaims claims = new(scenario.EntityRegistry, scenario.Players.PlayerManager, () => now);
        NitroxId plant = new();

        claims.NotePicked(plant, 2);

        claims.PickedLately(plant, 2).Should().BeTrue();
        claims.PickedLately(plant, 1).Should().BeFalse();
        now = ItemClaims.RECENT_PICK;
        claims.PickedLately(plant, 2).Should().BeFalse();
    }
}
