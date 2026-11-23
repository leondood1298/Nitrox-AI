using Microsoft.Extensions.Logging.Abstractions;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Packets;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;
using Nitrox.Model.Subnautica.Packets;
using Nitrox.Server.Subnautica.Models.Serialization;

namespace Nitrox.Test.Model.Subnautica;

[TestClass]
public sealed class StarshipDoorMetadataTest
{
    [TestMethod]
    public void SaveKeepsTheCutStateOfADoorThatWasOpenedAfterwards()
    {
        // A laser-cut door that somebody then opened: one metadata has to carry both, or the door is sealed again after a restart
        StarshipDoorMetadata expected = new(false, true, false, 100f);
        ServerJsonSerializer serializer = new(NullLogger<ServerJsonSerializer>.Instance);

        using MemoryStream output = new();
        serializer.Serialize(output, expected);
        using MemoryStream input = new(output.ToArray());
        StarshipDoorMetadata actual = serializer.Deserialize<StarshipDoorMetadata>(input);

        actual.DoorLocked.Should().BeFalse();
        actual.DoorOpen.Should().BeTrue();
        actual.Sealed.Should().BeFalse();
        actual.OpenedAmount.Should().Be(100f);
    }

    [TestMethod]
    public void UpdateCarriesTheCutProgressToOtherPlayers()
    {
        Packet.InitSerializer();
        EntityMetadataUpdate update = new(new NitroxId(), new StarshipDoorMetadata(true, false, true, 42.5f));

        EntityMetadataUpdate received = (EntityMetadataUpdate)Packet.Deserialize(update.Serialize());

        StarshipDoorMetadata actual = (StarshipDoorMetadata)received.NewValue;
        actual.DoorLocked.Should().BeTrue();
        actual.DoorOpen.Should().BeFalse();
        actual.Sealed.Should().BeTrue();
        actual.OpenedAmount.Should().Be(42.5f);
    }
}
