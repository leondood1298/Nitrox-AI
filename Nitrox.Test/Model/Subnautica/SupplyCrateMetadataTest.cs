using Microsoft.Extensions.Logging.Abstractions;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Packets;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;
using Nitrox.Model.Subnautica.Packets;
using Nitrox.Server.Subnautica.Models.Serialization;

namespace Nitrox.Test.Model.Subnautica;

[TestClass]
public sealed class SupplyCrateMetadataTest
{
    [TestMethod]
    public void SaveKeepsACrateThatWasCutOpenOpenAndUncut()
    {
        // Both states share one metadata, or the crate would be sealed or closed again after a restart
        SupplyCrateMetadata expected = new(true, false, 100f);
        ServerJsonSerializer serializer = new(NullLogger<ServerJsonSerializer>.Instance);

        using MemoryStream output = new();
        serializer.Serialize(output, expected);
        using MemoryStream input = new(output.ToArray());
        SupplyCrateMetadata actual = serializer.Deserialize<SupplyCrateMetadata>(input);

        actual.Open.Should().BeTrue();
        actual.Sealed.Should().BeFalse();
        actual.OpenedAmount.Should().Be(100f);
    }

    [TestMethod]
    public void UpdateCarriesTheOpenStateAndTheCutProgressToOtherPlayers()
    {
        Packet.InitSerializer();
        EntityMetadataUpdate update = new(new NitroxId(), new SupplyCrateMetadata(false, true, 42.5f));

        EntityMetadataUpdate received = (EntityMetadataUpdate)Packet.Deserialize(update.Serialize());

        SupplyCrateMetadata actual = (SupplyCrateMetadata)received.NewValue;
        actual.Open.Should().BeFalse();
        actual.Sealed.Should().BeTrue();
        actual.OpenedAmount.Should().Be(42.5f);
    }
}
