using Microsoft.Extensions.Logging.Abstractions;
using Nitrox.Model.DataStructures;
using Nitrox.Model.DataStructures.Unity;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;
using Nitrox.Server.Subnautica.Models.GameLogic.Entities;

namespace Nitrox.Test.Server.GameLogic.Entities;

[TestClass]
public sealed class EntityRegistryTest
{
    private EntityRegistry registry = null!;

    [TestInitialize]
    public void Setup()
    {
        registry = new EntityRegistry(NullLogger<EntityRegistry>.Instance);
    }

    [TestMethod]
    public void ADroppedToolWithItsBatteryListedTwiceKeepsOnlyTheInstalledBattery()
    {
        NitroxId flashlightId = new();
        NitroxId batteryId = new();
        WorldEntity flashlight = new(new NitroxTransform(NitroxVector3.Zero, NitroxQuaternion.Identity, NitroxVector3.One), 0, "flashlight", false, flashlightId,
                                     new NitroxTechType("Flashlight"), null, null,
                                     [
                                         new PrefabChildEntity(batteryId, "battery", new NitroxTechType("Battery"), 0, new BatteryMetadata(82.2f), flashlightId),
                                         new InstalledBatteryEntity(0, batteryId, new NitroxTechType("Battery"), new BatteryMetadata(82.2f), flashlightId, [])
                                     ]);

        registry.AddOrUpdate(flashlight);

        Entity battery = flashlight.ChildEntities.Should().ContainSingle().Subject;
        battery.Should().BeOfType<InstalledBatteryEntity>();
        registry.GetEntityById(batteryId).Value.Should().BeSameAs(battery);
    }

    [TestMethod]
    public void AChildAddedAgainUnderItsParentReplacesTheOldOneInsteadOfJoiningIt()
    {
        NitroxId toolId = new();
        NitroxId batteryId = new();
        InventoryItemEntity tool = new(toolId, "tool", new NitroxTechType("Welder"), null, null, []);
        registry.AddOrUpdate(tool);
        // A stale copy in the parent's children which the id index does not point at
        tool.ChildEntities.Add(new PrefabChildEntity(batteryId, "battery", new NitroxTechType("Battery"), 0, new BatteryMetadata(80f), toolId));
        InstalledBatteryEntity installed = new(0, batteryId, new NitroxTechType("Battery"), new BatteryMetadata(40f), toolId, []);

        registry.AddOrUpdate(installed);

        tool.ChildEntities.Should().ContainSingle().Which.Should().BeSameAs(installed);
    }

    [TestMethod]
    public void ReparentingAnEntityUnderAParentThatHoldsAStaleCopyLeavesOneChild()
    {
        InventoryItemEntity locker = new(new NitroxId(), "locker", new NitroxTechType("Locker"), null, null, []);
        InventoryItemEntity item = new(new NitroxId(), "item", new NitroxTechType("Titanium"), null, null, []);
        registry.AddOrUpdate(locker);
        registry.AddOrUpdate(item);
        locker.ChildEntities.Add(new InventoryItemEntity(item.Id, "item", new NitroxTechType("Titanium"), null, locker.Id, []));

        registry.ReparentEntity(item, locker);

        locker.ChildEntities.Should().ContainSingle().Which.Should().BeSameAs(item);
    }

    [TestMethod]
    public void KeepingOneChildPerIdKeepsTheLastOneAtEveryLevel()
    {
        NitroxId duplicateId = new();
        InventoryItemEntity first = new(duplicateId, "a", NitroxTechType.None, null, null, []);
        InventoryItemEntity last = new(duplicateId, "a", NitroxTechType.None, null, null, []);
        InventoryItemEntity other = new(new NitroxId(), "b", NitroxTechType.None, null, null, [first, last]);
        InventoryItemEntity root = new(new NitroxId(), "root", NitroxTechType.None, null, null, [other]);

        int removed = EntityRegistry.KeepLastChildPerId(root);

        removed.Should().Be(1);
        other.ChildEntities.Should().ContainSingle().Which.Should().BeSameAs(last);
    }
}
