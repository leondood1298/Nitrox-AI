using LiteNetLib.Utils;
using Nitrox.Model.Packets;
using Nitrox.Model.Subnautica.Packets;
using Nitrox.Server.Subnautica.Services;

namespace Nitrox.Test.Server.Communication;

/// <summary>
///     The receive side reads what a peer sent: anything on the open port can send anything.
/// </summary>
[TestClass]
public sealed class LiteNetLibServerServiceTest
{
    [ClassInitialize]
    public static void Init(TestContext context) => Packet.InitSerializer();

    [TestMethod]
    public void PacketOfThisBuildIsRead()
    {
        byte[] data = new ServerStopped().Serialize();

        Packet packet = LiteNetLibServerService.ReadPacket(Framed(data.Length, data));

        packet.Should().BeOfType<ServerStopped>();
    }

    [TestMethod]
    [DataRow(-1, DisplayName = "Negative length")]
    [DataRow(0, DisplayName = "Zero length")]
    [DataRow(int.MaxValue, DisplayName = "Length asking for a 2 GB buffer")]
    [DataRow(9, DisplayName = "Length past the data received")]
    public void LengthThatDoesNotFitTheDataIsRefused(int length)
    {
        Action read = () => LiteNetLibServerService.ReadPacket(Framed(length, new byte[8]));

        read.Should().Throw<InvalidDataException>();
    }

    [TestMethod]
    public void TooShortForALengthIsRefused()
    {
        NetDataReader reader = new(new byte[] { 1, 2 });

        Action read = () => LiteNetLibServerService.ReadPacket(reader);

        read.Should().Throw<InvalidDataException>();
    }

    [TestMethod]
    public void GarbageThrowsInsteadOfBeingRead()
    {
        byte[] garbage = Enumerable.Range(0, 64).Select(i => (byte)(i * 37 + 11)).ToArray();

        Action read = () => LiteNetLibServerService.ReadPacket(Framed(garbage.Length, garbage));

        read.Should().Throw<Exception>();
    }

    private static NetDataReader Framed(int length, byte[] data)
    {
        NetDataWriter writer = new();
        writer.Put(length);
        writer.Put(data);
        return new NetDataReader(writer.CopyData());
    }
}
