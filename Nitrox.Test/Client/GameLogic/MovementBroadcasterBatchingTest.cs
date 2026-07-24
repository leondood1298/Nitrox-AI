using Nitrox.Model.DataStructures;
using Nitrox.Model.DataStructures.Unity;
using Nitrox.Model.Subnautica.Packets;
using Nitrox.Server.Subnautica.Models.Packets.Processors;
using NitroxClient.MonoBehaviours;

namespace Nitrox.Test.Client.GameLogic;

[TestClass]
public sealed class MovementBroadcasterBatchingTest
{
    [TestMethod]
    public void ClientChunkLimitMatchesServerAuthorityLimit()
    {
        Assert.AreEqual(VehicleMovementsPacketProcessor.MAX_MOVEMENTS_PER_PACKET,
            MovementBroadcaster.MAX_MOVEMENTS_PER_PACKET);
    }

    [DataTestMethod]
    [DataRow(0, 0)]
    [DataRow(1, 1)]
    [DataRow(MovementBroadcaster.MAX_MOVEMENTS_PER_PACKET, 1)]
    [DataRow(MovementBroadcaster.MAX_MOVEMENTS_PER_PACKET + 1, 2)]
    [DataRow(MovementBroadcaster.MAX_MOVEMENTS_PER_PACKET * 2, 2)]
    public void MovementPacketsAreChunkedAtServerAuthorityLimit(int movementCount, int expectedPackets)
    {
        List<MovementData> movements = Enumerable.Range(0, movementCount)
            .Select(index => (MovementData)new SimpleMovementData(new NitroxId(),
                new NitroxVector3(index, 0f, 0f), NitroxQuaternion.Identity))
            .ToList();

        VehicleMovements[] packets = MovementBroadcaster.CreateMovementPackets(movements, 1533d).ToArray();

        Assert.AreEqual(expectedPackets, packets.Length);
        Assert.IsTrue(packets.All(packet => packet.Data.Count <= MovementBroadcaster.MAX_MOVEMENTS_PER_PACKET));
        CollectionAssert.AreEqual(movements.Select(movement => movement.Id).ToArray(),
            packets.SelectMany(packet => packet.Data).Select(movement => movement.Id).ToArray());
    }
}
