using System.Collections.Generic;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Packets;
using Nitrox.Model.Subnautica.Packets;
using NitroxClient.Communication;

namespace Nitrox.Test.Client.Communication;

[TestClass]
public sealed class PacketReceiverTest
{
    [TestMethod]
    public void OutOfTimeTheRestWaitsInOrderForTheNextFrame()
    {
        PacketReceiver receiver = new();
        List<Packet> packets = Enumerable.Range(0, 5).Select(_ => (Packet)new EntityDestroyed(new NitroxId())).ToList();
        packets.ForEach(receiver.Add);
        List<Packet> handled = [];

        receiver.ConsumePackets(static (packet, list) => list.Add(packet), handled, static list => list.Count >= 2);

        handled.Should().Equal(packets[0], packets[1]);
        receiver.Count.Should().Be(3);

        receiver.ConsumePackets(static (packet, list) => list.Add(packet), handled, static _ => false);

        handled.Should().Equal(packets);
    }

    [TestMethod]
    public void AtLeastOnePacketIsHandledEvenWithNoTimeLeft()
    {
        PacketReceiver receiver = new();
        receiver.Add(new EntityDestroyed(new NitroxId()));
        receiver.Add(new EntityDestroyed(new NitroxId()));
        int handled = 0;

        receiver.ConsumePackets((_, _) => handled++, 0, static _ => true);

        handled.Should().Be(1);
        receiver.Count.Should().Be(1);
    }
}
