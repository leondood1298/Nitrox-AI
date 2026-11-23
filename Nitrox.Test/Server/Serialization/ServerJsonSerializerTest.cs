using Microsoft.Extensions.Logging.Abstractions;
using Nitrox.Model.DataStructures;
using Nitrox.Model.DataStructures.Unity;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Bases;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;
using Nitrox.Server.Subnautica.Models.Serialization;

namespace Nitrox.Test.Server.Serialization;

[TestClass]
public sealed class ServerJsonSerializerTest
{
    private readonly ServerJsonSerializer serializer = new(NullLogger<ServerJsonSerializer>.Instance);

    [TestMethod]
    public void JsonRoundTripPreservesCanonicalState()
    {
        PowerSourceMetadata expected = new(216.5f, 1234.5f, BasePowerSourceType.NUCLEAR, 61.25f);

        using MemoryStream output = new();
        serializer.Serialize(output, expected);
        using MemoryStream input = new(output.ToArray());
        PowerSourceMetadata actual = serializer.Deserialize<PowerSourceMetadata>(input);

        Assert.AreEqual(expected.Power, actual.Power);
        Assert.AreEqual(expected.SourceType, actual.SourceType);
        Assert.AreEqual(expected.FuelConsumed, actual.FuelConsumed);
        Assert.AreEqual(expected.Temperature, actual.Temperature, "A thermal plant's temperature is saved with its power state.");
    }

    [TestMethod]
    public void JsonRoundTripPreservesThePowerStateInsideItsEntity()
    {
        // This is how it is saved: as the metadata of a base piece, where the serializer has to restore the right type.
        ModuleEntity plant = ModuleEntity.MakeEmpty();
        plant.Id = new NitroxId();
        plant.Metadata = new PowerSourceMetadata(88f, 0f, BasePowerSourceType.THERMAL, 73.5f);

        using MemoryStream output = new();
        serializer.Serialize(output, plant);
        using MemoryStream input = new(output.ToArray());
        ModuleEntity restored = serializer.Deserialize<ModuleEntity>(input);

        Assert.AreEqual(plant.Id, restored.Id);
        PowerSourceMetadata actual = (PowerSourceMetadata)restored.Metadata;
        Assert.AreEqual(88f, actual.Power);
        Assert.AreEqual(BasePowerSourceType.THERMAL, actual.SourceType);
        Assert.AreEqual(73.5f, actual.Temperature);
    }

    [TestMethod]
    public void JsonRoundTripPreservesTheLifepodsPowerCellsInsideThePod()
    {
        // The pod is saved as a global root entity with its cells as children.
        EscapePodEntity pod = new(NitroxVector3.Zero, new NitroxId(), new EscapePodMetadata(true, false));
        EscapePodPowerCellEntity cell = new(new NitroxId(), 2, pod.Id) { Metadata = new PowerSourceMetadata(14.5f, 0f, BasePowerSourceType.ESCAPE_POD) };
        pod.ChildEntities.Add(cell);

        using MemoryStream output = new();
        serializer.Serialize(output, pod);
        using MemoryStream input = new(output.ToArray());
        EscapePodEntity restored = serializer.Deserialize<EscapePodEntity>(input);

        EscapePodPowerCellEntity restoredCell = restored.ChildEntities.OfType<EscapePodPowerCellEntity>().Single();
        Assert.AreEqual(cell.Id, restoredCell.Id);
        Assert.AreEqual(2, restoredCell.CellIndex);
        Assert.AreEqual(pod.Id, restoredCell.ParentId);
        PowerSourceMetadata state = (PowerSourceMetadata)restoredCell.Metadata;
        Assert.AreEqual(14.5f, state.Power);
        Assert.AreEqual(BasePowerSourceType.ESCAPE_POD, state.SourceType);
    }

    [TestMethod]
    public void JsonRoundTripPreservesTheRocketsTimeCapsuleWithItsTextAndItems()
    {
        // The rocket is saved as a global root entity, its capsule as a child, and what was put into it as the capsule's children.
        VehicleEntity rocket = new(null, 0f, new NitroxTransform(), "", false, new NitroxId(), new NitroxTechType("RocketBase"), null);
        PlayerTimeCapsuleEntity capsule = new(new NitroxId(), rocket.Id, new PlayerTimeCapsuleMetadata("Hello from 4546B", ""));
        InventoryItemEntity item = new(new NitroxId(), "titanium-class-id", new NitroxTechType("Titanium"), null, capsule.Id, []);
        capsule.ChildEntities.Add(item);
        rocket.ChildEntities.Add(capsule);

        using MemoryStream output = new();
        serializer.Serialize(output, rocket);
        using MemoryStream input = new(output.ToArray());
        VehicleEntity restored = serializer.Deserialize<VehicleEntity>(input);

        PlayerTimeCapsuleEntity restoredCapsule = restored.ChildEntities.OfType<PlayerTimeCapsuleEntity>().Single();
        Assert.AreEqual(capsule.Id, restoredCapsule.Id);
        Assert.AreEqual(rocket.Id, restoredCapsule.ParentId);
        Assert.AreEqual("Hello from 4546B", ((PlayerTimeCapsuleMetadata)restoredCapsule.Metadata).Text);
        InventoryItemEntity restoredItem = restoredCapsule.ChildEntities.OfType<InventoryItemEntity>().Single();
        Assert.AreEqual(item.Id, restoredItem.Id);
        Assert.AreEqual(capsule.Id, restoredItem.ParentId);
    }

    [TestMethod]
    public void JsonRoundTripPreservesTheKeptContentOfATimeCapsuleInTheWorld()
    {
        WorldEntity capsule = new(NitroxVector3.Zero, NitroxQuaternion.Identity, NitroxVector3.One, NitroxTechType.None, 0, "time-capsule", true, new NitroxId(), null)
        {
            Metadata = new TimeCapsuleMetadata(true, "instance-1", "content-1", "Hi", "From the surface", "a.jpg",
                                               [new TimeCapsuleMetadata.Item(new NitroxTechType("Flashlight"), true, new NitroxTechType("Battery"), 75f)])
        };

        using MemoryStream output = new();
        serializer.Serialize(output, capsule);
        using MemoryStream input = new(output.ToArray());
        WorldEntity restored = serializer.Deserialize<WorldEntity>(input);

        TimeCapsuleMetadata content = (TimeCapsuleMetadata)restored.Metadata;
        Assert.IsTrue(content.HasCapsule);
        Assert.AreEqual("instance-1", content.InstanceId);
        Assert.AreEqual("content-1", content.ContentId);
        Assert.AreEqual("From the surface", content.Text);
        TimeCapsuleMetadata.Item item = content.Items.Single();
        Assert.AreEqual(new NitroxTechType("Flashlight"), item.TechType);
        Assert.IsTrue(item.HasBattery);
        Assert.AreEqual(new NitroxTechType("Battery"), item.BatteryType);
        Assert.AreEqual(75f, item.BatteryCharge);
    }

    [TestMethod]
    public void JsonRoundTripPreservesAPlainTemperatureReport()
    {
        // What a thermal plant holds until the first level update of the player simulating it.
        using MemoryStream output = new();
        serializer.Serialize(output, new ThermalPlantMetadata(48.5f));
        using MemoryStream input = new(output.ToArray());

        Assert.AreEqual(48.5f, serializer.Deserialize<ThermalPlantMetadata>(input).Temperature);
    }
}
