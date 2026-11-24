using Microsoft.Extensions.Logging.Abstractions;
using Nitrox.Model.DataStructures;
using Nitrox.Model.DataStructures.Unity;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;
using Nitrox.Server.Subnautica.Models.GameLogic.Entities;
using Nitrox.Server.Subnautica.Models.Serialization;
using Nitrox.Server.Subnautica.Models.Serialization.World;

namespace Nitrox.Test.Server.Serialization;

[TestClass]
public sealed class PersistedEntitiesTest
{
    private string folder = null!;

    [TestInitialize]
    public void Setup()
    {
        folder = Path.Combine(Path.GetTempPath(), $"nitrox-entities-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(folder);
    }

    [TestCleanup]
    public void Cleanup() => Directory.Delete(folder, true);

    [TestMethod]
    public void ASaveStoresAChildOnlyInsideItsParent()
    {
        WorldEntity parent = NewWorldEntity(null);
        WorldEntity child = NewWorldEntity(parent);

        List<Entity> saved = PersistedEntities.SelectForSave([parent, child]);

        saved.Should().ContainSingle().Which.Should().BeSameAs(parent);
        WriteAndRead(saved).Single().ChildEntities.Should().ContainSingle().Which.Id.Should().Be(child.Id);
    }

    [TestMethod]
    public void AWorldSavedThisWayIsReadBackWithEachEntityOnceAndChildrenInTheirParents()
    {
        WorldEntity parent = NewWorldEntity(null);
        WorldEntity child = NewWorldEntity(parent);
        WorldEntity grandChild = NewWorldEntity(child);

        PersistedEntities.LoadedEntities loaded = PersistedEntities.PrepareLoaded(WriteAndRead(PersistedEntities.SelectForSave([parent, child, grandChild])), []);

        loaded.Entities.Select(entity => entity.Id).Should().BeEquivalentTo([parent.Id, child.Id, grandChild.Id]);
        Entity loadedParent = loaded.Entities.Single(entity => entity.Id == parent.Id);
        Entity loadedChild = loadedParent.ChildEntities.Should().ContainSingle().Subject;
        loadedChild.Id.Should().Be(child.Id);
        loadedChild.ChildEntities.Should().ContainSingle().Which.Id.Should().Be(grandChild.Id);
        loaded.Entities.Single(entity => entity.Id == child.Id).Should().BeSameAs(loadedChild);
    }

    [TestMethod]
    public void AWorldWrittenByTheBuildBeforeIsStillReadWithEachEntityOnce()
    {
        // That build listed every entity at the top and every child inside its parent as well
        WorldEntity parent = NewWorldEntity(null);
        WorldEntity child = NewWorldEntity(parent);

        PersistedEntities.LoadedEntities loaded = PersistedEntities.PrepareLoaded(WriteAndRead([parent, child]), []);

        loaded.Entities.Should().HaveCount(2);
        loaded.Entities.Single(entity => entity.Id == parent.Id).ChildEntities.Should().ContainSingle().Which.Should().BeSameAs(loaded.Entities.Single(entity => entity.Id == child.Id));
        loaded.Orphans.Should().Be(0);
    }

    [TestMethod]
    public void ADroppedToolSavedWithItsBatteryTwiceIsLoadedWithTheBatteryThatKeptTheCharge()
    {
        // A build before this saved a dropped tool's battery as a prefab child with the charge at the drop and again as
        // the installed battery, which alone received the charge afterwards
        WorldEntity flashlight = NewWorldEntity((WorldEntity?)null);
        NitroxId batteryId = new();
        flashlight.ChildEntities.Add(new PrefabChildEntity(batteryId, "battery", new NitroxTechType("Battery"), 0, new BatteryMetadata(82.2f), flashlight.Id));
        flashlight.ChildEntities.Add(new InstalledBatteryEntity(0, batteryId, new NitroxTechType("Battery"), new BatteryMetadata(45f), flashlight.Id, []));

        PersistedEntities.LoadedEntities loaded = PersistedEntities.PrepareLoaded(WriteAndRead(PersistedEntities.SelectForSave([flashlight])), []);

        loaded.DuplicateChildren.Should().Be(1);
        Entity battery = loaded.Entities.Single(entity => entity.Id == flashlight.Id).ChildEntities.Should().ContainSingle().Subject;
        battery.Should().BeOfType<InstalledBatteryEntity>().Which.Metadata.Should().BeOfType<BatteryMetadata>().Which.Charge.Should().Be(45f);
        loaded.Entities.Single(entity => entity.Id == batteryId).Should().BeSameAs(battery);
    }

    [TestMethod]
    public void AGlobalRootToolSavedWithItsBatteryTwiceKeepsTheBatteryThatKeptTheCharge()
    {
        GlobalRootEntity flashlight = new(new NitroxTransform(NitroxVector3.Zero, NitroxQuaternion.Identity, NitroxVector3.One), 0, "flashlight", false, new NitroxId(),
                                          new NitroxTechType("Flashlight"), null, null, []);
        NitroxId batteryId = new();
        flashlight.ChildEntities.Add(new PrefabChildEntity(batteryId, "battery", new NitroxTechType("Battery"), 0, new BatteryMetadata(82.2f), flashlight.Id));
        flashlight.ChildEntities.Add(new InstalledBatteryEntity(0, batteryId, new NitroxTechType("Battery"), new BatteryMetadata(45f), flashlight.Id, []));

        PersistedEntities.LoadedEntities loaded = PersistedEntities.PrepareLoaded([], [flashlight]);

        loaded.DuplicateChildren.Should().Be(1);
        flashlight.ChildEntities.Should().ContainSingle().Which.Should().BeOfType<InstalledBatteryEntity>();
    }

    [TestMethod]
    public void AnEntityWhoseParentWasNotSavedIsLeftOutWithItsChildren()
    {
        // The contents of a destroyed Cyclops: the Cyclops itself is not saved
        WorldEntity powerCell = NewWorldEntityUnder(new NitroxId());
        WorldEntity battery = NewWorldEntity(powerCell);
        WorldEntity kept = NewWorldEntity((WorldEntity?)null);

        PersistedEntities.LoadedEntities loaded = PersistedEntities.PrepareLoaded([kept, powerCell, battery], []);

        loaded.Entities.Should().ContainSingle().Which.Should().BeSameAs(kept);
        loaded.Orphans.Should().Be(2);
    }

    [TestMethod]
    public void ASaveLeavesOutWhatHangsUnderAnUnsavedParent()
    {
        WorldEntity wreckContent = NewWorldEntityUnder(new NitroxId());

        PersistedEntities.SelectForSave([wreckContent]).Should().BeEmpty();
    }

    [TestMethod]
    public void TheChildOfAGlobalRootEntityIsReadFromTheGlobalRootFileOnly()
    {
        GlobalRootEntity vehicle = new(new NitroxTransform(NitroxVector3.Zero, NitroxQuaternion.Identity, NitroxVector3.One), 0, "seamoth", false, new NitroxId(), new NitroxTechType("Seamoth"),
                                       null, null, []);
        InventoryItemEntity item = new(new NitroxId(), "titanium", new NitroxTechType("Titanium"), null, vehicle.Id, []);
        vehicle.ChildEntities.Add(item);

        PersistedEntities.LoadedEntities loaded = PersistedEntities.PrepareLoaded([item], [vehicle]);

        loaded.Entities.Should().BeEmpty();
        loaded.CopiesOfGlobalRootChildren.Should().Be(1);
        loaded.Orphans.Should().Be(0);
    }

    [TestMethod]
    public void ExactCopiesOfTheGamesLightsAreRemovedOnLoad()
    {
        AbsoluteEntityCell cell = new(new NitroxInt3(11, 10, 11), new NitroxInt3(1, 2, 2), 2);
        SerializedWorldEntity light = NewLight(cell, new NitroxVector3(-244.21f, -1385.42f, -223.66f), [1, 2, 3]);
        SerializedWorldEntity copy = NewLight(cell, new NitroxVector3(-244.21f, -1385.42f, -223.66f), [1, 2, 3]);
        SerializedWorldEntity otherLight = NewLight(cell, new NitroxVector3(-244.21f, -1385.42f, -223.66f), [4, 5, 6]);
        SerializedWorldEntity elsewhere = NewLight(cell, new NitroxVector3(-240f, -1385.42f, -223.66f), [1, 2, 3]);

        PersistedEntities.LoadedEntities loaded = PersistedEntities.PrepareLoaded(WriteAndRead([light, copy, otherLight, elsewhere]), []);

        loaded.SerializedDuplicates.Should().Be(1);
        loaded.Entities.Select(entity => entity.Id).Should().BeEquivalentTo([light.Id, otherLight.Id, elsewhere.Id]);
    }

    private List<Entity> WriteAndRead(List<Entity> entities)
    {
        string path = Path.Combine(folder, "EntityData.json");
        ServerJsonSerializer serializer = new(NullLogger<ServerJsonSerializer>.Instance);
        serializer.Serialize(path, EntityData.From(entities));
        return serializer.Deserialize<EntityData>(path).Entities;
    }

    private static SerializedWorldEntity NewLight(AbsoluteEntityCell cell, NitroxVector3 position, byte[] data)
    {
        return new SerializedWorldEntity([new SerializedComponent("UnityEngine.Light", true, data)], 0, new NitroxTransform(position, NitroxQuaternion.Identity, NitroxVector3.One), new NitroxId(),
                                         null, cell);
    }

    private static WorldEntity NewWorldEntity(WorldEntity? parent)
    {
        WorldEntity entity = new(new NitroxVector3(1, -2, 3), NitroxQuaternion.Identity, NitroxVector3.One, new NitroxTechType("Titanium"), 0, "titanium", true, new NitroxId(), parent);
        parent?.ChildEntities.Add(entity);
        return entity;
    }

    private static WorldEntity NewWorldEntityUnder(NitroxId parentId)
    {
        return new WorldEntity(new NitroxTransform(new NitroxVector3(1, -2, 3), NitroxQuaternion.Identity, NitroxVector3.One), 0, "titanium", true, new NitroxId(), new NitroxTechType("Titanium"),
                               null, parentId, []);
    }
}
