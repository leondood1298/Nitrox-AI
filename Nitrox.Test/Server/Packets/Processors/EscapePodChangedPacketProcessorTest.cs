using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Nitrox.Model.DataStructures;
using Nitrox.Model.DataStructures.Unity;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Bases;
using Nitrox.Model.Subnautica.Packets;
using Nitrox.Server.Subnautica.Models.GameLogic.Entities;
using Nitrox.Server.Subnautica.Models.Packets.Core;
using Nitrox.Test.Server.Helper;
using NSubstitute;

namespace Nitrox.Server.Subnautica.Models.Packets.Processors;

/// <summary>
///     A player entering or leaving the Lifepod. The server puts the player's entity under the pod and tells the others,
///     so it checks who sent it and that the pod is one.
/// </summary>
[TestClass]
public sealed class EscapePodChangedPacketProcessorTest
{
    private ConnectedPlayers players;
    private EntityRegistry entityRegistry;
    private IPacketSender packetSender;
    private EscapePodChangedPacketProcessor processor;
    private Player sender;
    private Player otherPlayer;
    private PlayerEntity senderEntity;
    private EscapePodEntity pod;

    [TestInitialize]
    public void Init()
    {
        players = new ConnectedPlayers();
        entityRegistry = new EntityRegistry(NullLogger<EntityRegistry>.Instance);
        packetSender = Substitute.For<IPacketSender>();
        processor = new EscapePodChangedPacketProcessor(entityRegistry, NullLogger<EscapePodChangedPacketProcessor>.Instance);
        sender = players.Connect("Sender");
        otherPlayer = players.Connect("Other");
        senderEntity = new PlayerEntity(new NitroxTransform(NitroxVector3.Zero, NitroxQuaternion.Identity, NitroxVector3.One), 0, null, false, sender.GameObjectId, NitroxTechType.None, null, null, []);
        entityRegistry.AddEntity(senderEntity);
        pod = new EscapePodEntity(NitroxVector3.Zero, new NitroxId(), null);
        entityRegistry.AddEntity(pod);
    }

    [TestMethod]
    public async Task EnteringThePodMovesThePlayerIntoItAndTellsTheOthers()
    {
        await Send(new EscapePodChanged(sender.SessionId, Optional.Of(pod.Id)));

        senderEntity.ParentId.Should().Be(pod.Id);
        pod.ChildEntities.Should().Contain(senderEntity);
        sender.SubRootId.Value.Should().Be(pod.Id);
        await packetSender.Received(1).SendPacketToOthersAsync(Arg.Any<EscapePodChanged>(), sender.SessionId);
    }

    [TestMethod]
    public async Task LeavingThePodTakesThePlayerOutOfIt()
    {
        await Send(new EscapePodChanged(sender.SessionId, Optional.Of(pod.Id)));

        await Send(new EscapePodChanged(sender.SessionId, Optional.Empty));

        senderEntity.ParentId.Should().BeNull();
        pod.ChildEntities.Should().NotContain(senderEntity);
        sender.SubRootId.HasValue.Should().BeFalse();
    }

    [TestMethod]
    public async Task APacketThatNamesAnotherPlayerIsIgnored()
    {
        await Send(new EscapePodChanged(otherPlayer.SessionId, Optional.Of(pod.Id)));

        senderEntity.ParentId.Should().BeNull();
        packetSender.ReceivedCalls().Should().BeEmpty();
    }

    [TestMethod]
    public async Task SomethingThatIsNoEscapePodIsIgnored()
    {
        BuildEntity playerBase = BuildEntity.MakeEmpty();
        playerBase.Id = new NitroxId();
        entityRegistry.AddEntity(playerBase);

        await Send(new EscapePodChanged(sender.SessionId, Optional.Of(playerBase.Id)));
        await Send(new EscapePodChanged(sender.SessionId, Optional.Of(sender.GameObjectId)));
        await Send(new EscapePodChanged(sender.SessionId, Optional.Of(new NitroxId())));

        senderEntity.ParentId.Should().BeNull();
        sender.SubRootId.HasValue.Should().BeFalse();
        packetSender.ReceivedCalls().Should().BeEmpty();
    }

    private Task Send(EscapePodChanged packet)
    {
        return processor.Process(new AuthProcessorContext(sender, packetSender), packet);
    }
}
