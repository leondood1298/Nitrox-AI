using System.Collections.Generic;
using Nitrox.Model.DataStructures;
using Nitrox.Model.DataStructures.GameLogic;
using Nitrox.Model.Packets;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities;
using Nitrox.Model.Subnautica.Packets;
using BinaryConverter = global::BinaryPack.BinaryConverter;
using BinaryReader = global::BinaryPack.Serialization.Buffers.BinaryReader;

namespace Nitrox.Test.Model.Packets;

/// <summary>
///     Packets come from other machines: a length or a nesting in them must not make the reader allocate gigabytes, run
///     off the end of the data or exhaust the thread's stack. Each such packet is refused with an
///     <see cref="InvalidDataException" />.
/// </summary>
[TestClass]
public sealed class BinaryReaderTest
{
    private const int HUGE_LENGTH = 0x7FFFFFF0;

    [ClassInitialize]
    public static void Init(TestContext context) => Packet.InitSerializer();

    [TestMethod]
    public void AHolderWithEveryKindOfLengthIsReadBackWhole()
    {
        Holder read = BinaryConverter.Deserialize<Holder>(BinaryConverter.Serialize(Sample()));

        read.Text.Should().Be("TEXTMARK");
        read.Numbers.Should().Equal(0x11223344, 0x55667788);
        read.Bytes.Should().Equal(0xA1, 0xA2, 0xA3, 0xA4);
        read.Names.Should().Equal("NAMEMARK");
        read.Counts.Should().ContainKey("DICTMARK").WhoseValue.Should().Be(5);
        read.Grid.Should().BeEquivalentTo(new[,] { { 1, 2 }, { 3, 4 } });
    }

    [TestMethod]
    public void AStringLongerThanTheDataIsRefused()
    {
        byte[] data = BinaryConverter.Serialize(Sample());
        PatchInt(data, IndexOf(data, "TEXTMARK"u8.ToArray()) - 4, HUGE_LENGTH);

        RefusesToRead(data);
    }

    [TestMethod]
    public void AnArrayLongerThanTheDataIsRefused()
    {
        byte[] data = BinaryConverter.Serialize(Sample());
        PatchInt(data, IndexOf(data, [0x44, 0x33, 0x22, 0x11, 0x88, 0x77, 0x66, 0x55]) - 4, HUGE_LENGTH);

        RefusesToRead(data);
    }

    [TestMethod]
    public void AByteArrayLongerThanTheDataIsRefused()
    {
        byte[] data = BinaryConverter.Serialize(Sample());
        PatchInt(data, IndexOf(data, [0xA1, 0xA2, 0xA3, 0xA4]) - 4, HUGE_LENGTH);

        RefusesToRead(data);
    }

    [TestMethod]
    public void AListLongerThanTheDataIsRefused()
    {
        byte[] data = BinaryConverter.Serialize(Sample());
        // The list's count comes before the length of its one string
        PatchInt(data, IndexOf(data, "NAMEMARK"u8.ToArray()) - 8, HUGE_LENGTH);

        RefusesToRead(data);
    }

    [TestMethod]
    public void ADictionaryLongerThanTheDataIsRefused()
    {
        byte[] data = BinaryConverter.Serialize(Sample());
        PatchInt(data, IndexOf(data, "DICTMARK"u8.ToArray()) - 8, HUGE_LENGTH);

        RefusesToRead(data);
    }

    [TestMethod]
    [DataRow(-2, DisplayName = "Negative length")]
    [DataRow(HUGE_LENGTH, DisplayName = "Huge length")]
    public void ALengthThatIsNeitherNullNorAPossibleCountIsRefused(int length)
    {
        byte[] data = BinaryConverter.Serialize(Sample());
        PatchInt(data, IndexOf(data, "NAMEMARK"u8.ToArray()) - 4, length);

        RefusesToRead(data);
    }

    [TestMethod]
    public void AMultidimensionalArrayWhoseDimensionsDoNotMatchItsLengthIsRefused()
    {
        byte[] data = BinaryConverter.Serialize(new GridHolder { Grid = new[,] { { 1, 2 }, { 3, 4 } } });
        // Length 4, then dimensions 2 and 2: claim 2 and 0x10000 instead, which would allocate far more than the data
        int lengthAt = IndexOf(data, [4, 0, 0, 0, 2, 0, 0, 0, 2, 0, 0, 0]);
        PatchInt(data, lengthAt + 8, 0x10000);

        RefusesToRead<GridHolder>(data);
    }

    [TestMethod]
    public void DataCutShortIsRefusedInsteadOfReadPastItsEnd()
    {
        byte[] data = BinaryConverter.Serialize(Sample());
        for (int cut = 1; cut <= 4; cut++)
        {
            byte[] shorter = data.Take(data.Length - cut).ToArray();
            RefusesToRead(shorter);
        }
    }

    [TestMethod]
    public void AValueAtTheVeryEndIsNotReadPartlyPastIt()
    {
        // The object is there (1), then one byte is left where its int needs four
        Action read = () => BinaryConverter.Deserialize<IntHolder>(new byte[] { 1, 7 });

        read.Should().Throw<InvalidDataException>();
    }

    [TestMethod]
    public void ObjectsNestedDeeperThanTheLimitAreRefused()
    {
        Holder root = new();
        Holder current = root;
        for (int i = 0; i < BinaryReader.MaxDepth + 10; i++)
        {
            Holder child = new();
            current.Children = [child];
            current = child;
        }

        RefusesToRead(BinaryConverter.Serialize(root));
    }

    [TestMethod]
    public void ObjectsNestedAsDeepAsRealDataAreRead()
    {
        Holder root = new();
        Holder current = root;
        for (int i = 0; i < 30; i++)
        {
            Holder child = new() { Text = $"level {i + 1}" };
            current.Children = [child];
            current = child;
        }

        Holder read = BinaryConverter.Deserialize<Holder>(BinaryConverter.Serialize(root));

        for (int i = 0; i < 30; i++)
        {
            read = read.Children.Single();
        }
        read.Text.Should().Be("level 30");
    }

    [TestMethod]
    public void APacketWithAnEntityChainDeeperThanTheLimitIsRefused()
    {
        Entity root = ChildEntity(null);
        Entity current = root;
        for (int i = 0; i < BinaryReader.MaxDepth + 10; i++)
        {
            Entity child = ChildEntity(current.Id);
            current.ChildEntities.Add(child);
            current = child;
        }
        byte[] data = new EntitySpawnedByClient(root).Serialize();

        Action read = () => Packet.Deserialize(data, data.Length);

        read.Should().Throw<InvalidDataException>();
    }

    [TestMethod]
    public void APacketIsReadFromThePartOfAPooledBufferThatHoldsIt()
    {
        byte[] packet = new ChatMessage((ushort)3, "hello").Serialize();
        byte[] pooled = new byte[packet.Length + 100];
        packet.CopyTo(pooled, 0);

        Packet read = Packet.Deserialize(pooled, packet.Length);

        read.Should().BeOfType<ChatMessage>().Which.Text.Should().Be("hello");
    }

    private static Entity ChildEntity(NitroxId? parentId) => new PrefabChildEntity(new NitroxId(), "class", NitroxTechType.None, 0, null, parentId);

    private static Holder Sample() => new()
    {
        Text = "TEXTMARK",
        Numbers = [0x11223344, 0x55667788],
        Bytes = [0xA1, 0xA2, 0xA3, 0xA4],
        Names = ["NAMEMARK"],
        Counts = new Dictionary<string, int> { ["DICTMARK"] = 5 },
        Grid = new[,] { { 1, 2 }, { 3, 4 } }
    };

    private static void RefusesToRead(byte[] data) => RefusesToRead<Holder>(data);

    private static void RefusesToRead<T>(byte[] data)
    {
        Action read = () => BinaryConverter.Deserialize<T>(data);

        read.Should().Throw<InvalidDataException>();
    }

    private static int IndexOf(byte[] data, byte[] pattern)
    {
        for (int i = 0; i <= data.Length - pattern.Length; i++)
        {
            if (data.AsSpan(i, pattern.Length).SequenceEqual(pattern))
            {
                return i;
            }
        }
        throw new InvalidOperationException("Marker not found in the serialized data");
    }

    private static void PatchInt(byte[] data, int at, int value) => BitConverter.GetBytes(value).CopyTo(data, at);

    public sealed class Holder
    {
        public string? Text { get; set; }
        public int[]? Numbers { get; set; }
        public byte[]? Bytes { get; set; }
        public List<string>? Names { get; set; }
        public Dictionary<string, int>? Counts { get; set; }
        public int[,]? Grid { get; set; }
        public List<Holder>? Children { get; set; }
    }

    public sealed class GridHolder
    {
        public int[,]? Grid { get; set; }
    }

    public sealed class IntHolder
    {
        public int Value { get; set; }
    }
}
