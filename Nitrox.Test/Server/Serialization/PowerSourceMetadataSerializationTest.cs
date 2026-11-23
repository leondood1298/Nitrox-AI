using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Bases;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;

namespace Nitrox.Server.Subnautica.Models.Serialization;

[TestClass]
public sealed class PowerSourceMetadataSerializationTest
{
    private readonly ServerJsonSerializer serializer = new(NullLogger<ServerJsonSerializer>.Instance);

    [TestMethod]
    public void JsonRoundTripPreservesCanonicalState()
    {
        PowerSourceMetadata expected = new(216.5f, 2500f, BasePowerSourceType.NUCLEAR, 17, 1234.5f, 61.25f);

        using MemoryStream output = new();
        serializer.Serialize(output, expected);
        using MemoryStream input = new(output.ToArray());
        PowerSourceMetadata actual = serializer.Deserialize<PowerSourceMetadata>(input);

        Assert.AreEqual(expected.Power, actual.Power);
        Assert.AreEqual(expected.MaxPower, actual.MaxPower);
        Assert.AreEqual(expected.SourceType, actual.SourceType);
        Assert.AreEqual(expected.Revision, actual.Revision);
        Assert.AreEqual(expected.FuelConsumed, actual.FuelConsumed);
        Assert.AreEqual(expected.Temperature, actual.Temperature, "A thermal plant's temperature is saved with its power state.");
    }

    [TestMethod]
    public void JsonRoundTripPreservesThePowerStateInsideItsEntity()
    {
        // This is how it is saved: as the metadata of a base piece, where the serializer has to restore the right type.
        ModuleEntity plant = ModuleEntity.MakeEmpty();
        plant.Id = new NitroxId();
        plant.Metadata = new PowerSourceMetadata(88f, 250f, BasePowerSourceType.THERMAL, 412, temperature: 73.5f);

        using MemoryStream output = new();
        serializer.Serialize(output, plant);
        using MemoryStream input = new(output.ToArray());
        ModuleEntity restored = serializer.Deserialize<ModuleEntity>(input);

        Assert.AreEqual(plant.Id, restored.Id);
        PowerSourceMetadata actual = (PowerSourceMetadata)restored.Metadata;
        Assert.AreEqual(88f, actual.Power);
        Assert.AreEqual(BasePowerSourceType.THERMAL, actual.SourceType);
        Assert.AreEqual(412, actual.Revision);
        Assert.AreEqual(73.5f, actual.Temperature);
    }

    [TestMethod]
    public void JsonRoundTripPreservesAPlainTemperatureReport()
    {
        // What a thermal plant holds until its owner's first power update.
        using MemoryStream output = new();
        serializer.Serialize(output, new ThermalPlantMetadata(48.5f));
        using MemoryStream input = new(output.ToArray());

        Assert.AreEqual(48.5f, serializer.Deserialize<ThermalPlantMetadata>(input).Temperature);
    }

    [TestMethod]
    public void LegacyJsonPreservesPowerAndDefaultsNewFields()
    {
        using MemoryStream stream = new(Encoding.UTF8.GetBytes("{\"Power\":43.092995}"));

        PowerSourceMetadata actual = serializer.Deserialize<PowerSourceMetadata>(stream);

        Assert.AreEqual(43.092995f, actual.Power);
        Assert.AreEqual(0f, actual.MaxPower);
        Assert.AreEqual(BasePowerSourceType.UNKNOWN, actual.SourceType);
        Assert.AreEqual(0, actual.Revision);
        Assert.AreEqual(0f, actual.FuelConsumed);
    }
}
