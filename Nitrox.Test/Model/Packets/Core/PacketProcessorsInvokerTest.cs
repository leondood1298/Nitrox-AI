using System.Threading.Tasks;
using Nitrox.Model.DataStructures.Unity;
using Nitrox.Model.Packets;
using Nitrox.Model.Packets.Core;
using Nitrox.Model.Subnautica.Packets;

namespace Nitrox.Test.Model.Packets.Core;

[TestClass]
public sealed class PacketProcessorsInvokerTest
{
    [TestMethod]
    public void APacketWithoutItsOwnProcessorGoesToTheProcessorOfItsBaseType()
    {
        MovementProcessor movementProcessor = new();
        PacketProcessorsInvoker invoker = new([movementProcessor]);

        PacketProcessorsInvoker.Entry? entry = invoker.GetProcessor(typeof(PlayerMovement));

        entry.Should().NotBeNull();
        entry!.Processor.Should().BeSameAs(movementProcessor);
    }

    [TestMethod]
    public void APacketsOwnProcessorComesBeforeTheOneOfItsBaseType()
    {
        PlayerMovementProcessor playerMovementProcessor = new();
        PacketProcessorsInvoker invoker = new([new AnyPacketProcessor(), playerMovementProcessor]);

        invoker.GetProcessor(typeof(PlayerMovement))!.Processor.Should().BeSameAs(playerMovementProcessor);
    }

    [TestMethod]
    public void APacketWithNoProcessorOnAnyOfItsTypesHasNone()
    {
        PacketProcessorsInvoker invoker = new([new MovementProcessor()]);

        invoker.GetProcessor(typeof(ServerStopped)).Should().BeNull();
    }

    [TestMethod]
    public void AProcessorFoundThroughTheBaseTypeRunsWithThePacket()
    {
        MovementProcessor movementProcessor = new();
        PacketProcessorsInvoker invoker = new([movementProcessor]);
        PlayerMovement packet = new((ushort)1, NitroxVector3.Zero, NitroxVector3.Zero, NitroxQuaternion.Identity, NitroxQuaternion.Identity);

        invoker.GetProcessor(typeof(PlayerMovement))!.Execute(new TestContext(), packet);

        movementProcessor.Processed.Should().BeSameAs(packet);
    }

    public sealed class TestContext : IPacketProcessContext;

    public sealed class MovementProcessor : IPacketProcessor<TestContext, Movement>
    {
        public Movement? Processed { get; private set; }

        public Task Process(TestContext context, Movement packet)
        {
            Processed = packet;
            return Task.CompletedTask;
        }
    }

    public sealed class PlayerMovementProcessor : IPacketProcessor<TestContext, PlayerMovement>
    {
        public Task Process(TestContext context, PlayerMovement packet) => Task.CompletedTask;
    }

    public sealed class AnyPacketProcessor : IPacketProcessor<TestContext, Packet>
    {
        public Task Process(TestContext context, Packet packet) => Task.CompletedTask;
    }
}
