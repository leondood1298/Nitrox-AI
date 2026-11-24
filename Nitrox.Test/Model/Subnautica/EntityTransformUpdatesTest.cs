using System.Collections.Generic;
using Nitrox.Model.DataStructures;
using Nitrox.Model.DataStructures.Unity;
using Nitrox.Model.Networking;
using Nitrox.Model.Packets;
using Nitrox.Model.Subnautica.Packets;
using static Nitrox.Model.Subnautica.Packets.EntityTransformUpdates;

namespace Nitrox.Test.Model.Subnautica;

[TestClass]
public sealed class EntityTransformUpdatesTest
{
    [ClassInitialize]
    public static void Init(TestContext context) => Packet.InitSerializer();

    [TestMethod]
    public void PositionsGoUnreliableOnTheMovementChannel()
    {
        EntityTransformUpdates packet = new([]);

        packet.DeliveryMethod.Should().Be(NitroxDeliveryMethod.DeliveryMethod.UNRELIABLE_SEQUENCED);
        packet.UdpChannel.Should().Be(Packet.UdpChannelId.MOVEMENTS);
    }

    [TestMethod]
    public void AFullPieceOfTheLargerUpdatesFitsOneDatagram()
    {
        List<EntityTransformUpdate> updates = Enumerable.Range(0, MAX_UPDATES_PER_PACKET).Select(_ => Spline()).ToList();

        new EntityTransformUpdates(updates).Serialize().Length.Should().BeLessThanOrEqualTo(NitroxDeliveryMethod.MAX_UNRELIABLE_PACKET_BYTES);
    }

    [TestMethod]
    [DataRow(0, 0)]
    [DataRow(1, 1)]
    [DataRow(MAX_UPDATES_PER_PACKET, 1)]
    [DataRow(MAX_UPDATES_PER_PACKET + 1, 2)]
    [DataRow(100, 9)]
    public void UpdatesAreSplitIntoPiecesThatEachFitOneDatagram(int count, int pieces)
    {
        List<EntityTransformUpdate> updates = Enumerable.Range(0, count).Select(_ => Spline()).ToList();

        List<EntityTransformUpdates> split = EntityTransformUpdates.Split(updates).ToList();

        split.Should().HaveCount(pieces);
        split.All(piece => piece.Updates.Count <= MAX_UPDATES_PER_PACKET).Should().BeTrue();
        split.SelectMany(piece => piece.Updates).Should().Equal(updates);
    }

    private static EntityTransformUpdate Spline() => new SplineTransformUpdate(new NitroxId(), new NitroxVector3(1, 2, 3), NitroxQuaternion.Identity, new NitroxVector3(4, 5, 6), new NitroxVector3(0, 0, 1), 3f);
}
