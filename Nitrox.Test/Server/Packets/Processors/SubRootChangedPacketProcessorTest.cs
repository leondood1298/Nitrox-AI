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
///     A player entering or leaving a base or Cyclops. The server puts the player's entity under the sub and tells the
///     others, so it checks who sent it and what the sub is.
/// </summary>
[TestClass]
public sealed class SubRootChangedPacketProcessorTest
{
    private ConnectedPlayers players;
    private EntityRegistry entityRegistry;
    private IPacketSender packetSender;
    private SubRootChangedPacketProcessor processor;
    private Player sender;
    private Player otherPlayer;
    private PlayerEntity senderEntity;

    [TestInitialize]
    public void Init()
    {
        players = new ConnectedPlayers();
        entityRegistry = new EntityRegistry(NullLogger<EntityRegistry>.Instance);
        packetSender = Substitute.For<IPacketSender>();
        processor = new SubRootChangedPacketProcessor(entityRegistry, NullLogger<SubRootChangedPacketProcessor>.Instance);
        sender = players.Connect("Sender");
        otherPlayer = players.Connect("Other");
        senderEntity = AddPlayerEntity(sender);
        AddPlayerEntity(otherPlayer);
    }

    [TestMethod]
    public async Task EnteringABaseMovesThePlayerIntoItAndTellsTheOthers()
    {
        BuildEntity playerBase = AddBase();

        await Send(sender, new SubRootChanged(sender.SessionId, Optional.Of(playerBase.Id)));

        senderEntity.ParentId.Should().Be(playerBase.Id);
        playerBase.ChildEntities.Should().Contain(senderEntity);
        sender.SubRootId.Value.Should().Be(playerBase.Id);
        await packetSender.Received(1).SendPacketToOthersAsync(Arg.Any<SubRootChanged>(), sender.SessionId);
    }

    [TestMethod]
    public async Task EnteringACyclopsMovesThePlayerIntoIt()
    {
        VehicleEntity cyclops = AddVehicle("Cyclops");

        await Send(sender, new SubRootChanged(sender.SessionId, Optional.Of(cyclops.Id)));

        senderEntity.ParentId.Should().Be(cyclops.Id);
        sender.SubRootId.Value.Should().Be(cyclops.Id);
    }

    [TestMethod]
    public async Task LeavingASubTakesThePlayerOutOfIt()
    {
        BuildEntity playerBase = AddBase();
        await Send(sender, new SubRootChanged(sender.SessionId, Optional.Of(playerBase.Id)));
        packetSender.ClearReceivedCalls();

        await Send(sender, new SubRootChanged(sender.SessionId, Optional.Empty));

        senderEntity.ParentId.Should().BeNull();
        playerBase.ChildEntities.Should().NotContain(senderEntity);
        sender.SubRootId.HasValue.Should().BeFalse();
        await packetSender.Received(1).SendPacketToOthersAsync(Arg.Any<SubRootChanged>(), sender.SessionId);
    }

    [TestMethod]
    public async Task APacketThatNamesAnotherPlayerIsIgnored()
    {
        // The others would move that player into the sub.
        BuildEntity playerBase = AddBase();

        await Send(sender, new SubRootChanged(otherPlayer.SessionId, Optional.Of(playerBase.Id)));

        senderEntity.ParentId.Should().BeNull();
        sender.SubRootId.HasValue.Should().BeFalse();
        packetSender.ReceivedCalls().Should().BeEmpty();
    }

    [TestMethod]
    public async Task ASubTheServerDoesNotKnowIsIgnored()
    {
        await Send(sender, new SubRootChanged(sender.SessionId, Optional.Of(new NitroxId())));

        senderEntity.ParentId.Should().BeNull();
        sender.SubRootId.HasValue.Should().BeFalse();
        packetSender.ReceivedCalls().Should().BeEmpty();
    }

    [TestMethod]
    public async Task SomethingThatIsNoBaseOrCyclopsIsIgnored()
    {
        // A player is never inside a Seamoth's or a locker's SubRoot; under them its entity would be saved in the wrong place.
        VehicleEntity seamoth = AddVehicle("Seamoth");
        EscapePodEntity pod = new(NitroxVector3.Zero, new NitroxId(), null);
        entityRegistry.AddEntity(pod);

        await Send(sender, new SubRootChanged(sender.SessionId, Optional.Of(seamoth.Id)));
        await Send(sender, new SubRootChanged(sender.SessionId, Optional.Of(pod.Id)));

        senderEntity.ParentId.Should().BeNull();
        packetSender.ReceivedCalls().Should().BeEmpty();
    }

    [TestMethod]
    public async Task ThePlayersOwnEntityOrSomethingItCarriesIsIgnored()
    {
        // The player would become its own ancestor.
        VehicleEntity carried = AddVehicle("Cyclops");
        entityRegistry.ReparentEntity(carried, senderEntity);

        await Send(sender, new SubRootChanged(sender.SessionId, Optional.Of(sender.GameObjectId)));
        await Send(sender, new SubRootChanged(sender.SessionId, Optional.Of(carried.Id)));

        senderEntity.ParentId.Should().BeNull();
        carried.ParentId.Should().Be(senderEntity.Id);
        packetSender.ReceivedCalls().Should().BeEmpty();
    }

    private Task Send(Player player, SubRootChanged packet)
    {
        return processor.Process(new AuthProcessorContext(player, packetSender), packet);
    }

    private PlayerEntity AddPlayerEntity(Player player)
    {
        PlayerEntity entity = new(new NitroxTransform(NitroxVector3.Zero, NitroxQuaternion.Identity, NitroxVector3.One), 0, null, false, player.GameObjectId, NitroxTechType.None, null, null, []);
        entityRegistry.AddEntity(entity);
        return entity;
    }

    private BuildEntity AddBase()
    {
        BuildEntity playerBase = BuildEntity.MakeEmpty();
        playerBase.Id = new NitroxId();
        playerBase.Transform = new NitroxTransform(NitroxVector3.Zero, NitroxQuaternion.Identity, NitroxVector3.One);
        entityRegistry.AddEntity(playerBase);
        return playerBase;
    }

    private VehicleEntity AddVehicle(string techType)
    {
        VehicleEntity vehicle = new(new NitroxId(), 0f, new NitroxTransform(NitroxVector3.Zero, NitroxQuaternion.Identity, NitroxVector3.One), null, false, new NitroxId(), new NitroxTechType(techType), null);
        entityRegistry.AddEntity(vehicle);
        return vehicle;
    }
}
