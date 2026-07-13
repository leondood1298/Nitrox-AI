using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;

namespace Nitrox.Server.Subnautica.Models.Serialization;

[TestClass]
public sealed class PowerSourceMetadataSerializationTest
{
    private readonly ServerJsonSerializer serializer = new(NullLogger<ServerJsonSerializer>.Instance);

    [TestMethod]
    public void JsonRoundTripPreservesCanonicalState()
    {
        PowerSourceMetadata expected = new(42.5f, 75f, BasePowerSourceType.SOLAR, 17);

        using MemoryStream output = new();
        serializer.Serialize(output, expected);
        using MemoryStream input = new(output.ToArray());
        PowerSourceMetadata actual = serializer.Deserialize<PowerSourceMetadata>(input);

        Assert.AreEqual(expected.Power, actual.Power);
        Assert.AreEqual(expected.MaxPower, actual.MaxPower);
        Assert.AreEqual(expected.SourceType, actual.SourceType);
        Assert.AreEqual(expected.Revision, actual.Revision);
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
    }
}
