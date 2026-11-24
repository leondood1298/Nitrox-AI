using Nitrox.Model.DataStructures;
using Nitrox.Model.DataStructures.Unity;
using Nitrox.Model.Networking;
using Nitrox.Model.Packets;
using Nitrox.Model.Subnautica.Packets;

namespace Nitrox.Test.Model.Subnautica;

[TestClass]
public sealed class VehicleMovementsTest
{
    [ClassInitialize]
    public static void Init(TestContext context) => Packet.InitSerializer();

    [TestMethod]
    public void AFullPacketOfTheLargestMovementsFitsOneDatagram()
    {
        VehicleMovements packet = new(Enumerable.Range(0, VehicleMovements.MAX_MOVEMENTS_PER_PACKET)
                                                .Select(_ => (MovementData)new ExosuitMovementData(new NitroxId(), NitroxVector3.One, NitroxQuaternion.Identity, NitroxVector3.One, NitroxVector3.One, 1, 1, true, true))
                                                .ToList(), 12.5d);

        packet.Serialize().Length.Should().BeLessThanOrEqualTo(NitroxDeliveryMethod.MAX_UNRELIABLE_PACKET_BYTES);
    }
}
