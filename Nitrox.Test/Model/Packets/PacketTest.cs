using Nitrox.Model.DataStructures;
using Nitrox.Model.Packets;
using Nitrox.Model.Subnautica.Packets;

namespace Nitrox.Test.Model.Packets;

[TestClass]
public sealed class PacketTest
{
    [ClassInitialize]
    public static void Init(TestContext context) => Packet.InitSerializer();

    [TestMethod]
    public void TheTypeOfAPacketIsKnownFromItsFirstBytes()
    {
        foreach (Packet packet in new Packet[] { new ServerStopped(), new ChatMessage((ushort)2, "hi"), new EntityDestroyed(new NitroxId()) })
        {
            byte[] data = packet.Serialize();

            Packet.TryPeekType(data, out Type? type).Should().BeTrue();
            type.Should().Be(packet.GetType());
        }
    }

    [TestMethod]
    [DataRow(new byte[] { 1, 2 }, DisplayName = "Too short")]
    [DataRow(new byte[] { 0xFF, 0xFF, 0xFF, 0x7F }, DisplayName = "Index past the packet types")]
    [DataRow(new byte[] { 0xFE, 0xFF, 0xFF, 0xFF }, DisplayName = "Negative index")]
    public void DataThatNamesNoPacketTypeHasNone(byte[] data)
    {
        Packet.TryPeekType(data, out Type? type).Should().BeFalse();
        type.Should().BeNull();
    }
}
