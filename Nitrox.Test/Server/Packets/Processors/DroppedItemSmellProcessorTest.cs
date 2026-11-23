using System.Threading.Tasks;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.Packets;
using Nitrox.Server.Subnautica.Models.Packets.Core;
using Nitrox.Test.Server.Helper;
using NSubstitute;

namespace Nitrox.Server.Subnautica.Models.Packets.Processors;

[TestClass]
public sealed class DroppedItemSmellProcessorTest
{
    private readonly NitroxId itemId = new();
    private readonly DroppedItemSmellProcessor processor = new();
    private ConnectedPlayers players;
    private IPacketSender packetSender;
    private Player dropper;
    private Player other;

    [TestInitialize]
    public void Init()
    {
        players = new ConnectedPlayers();
        packetSender = Substitute.For<IPacketSender>();
        dropper = players.Connect("Dropper");
        other = players.Connect("Other");
    }

    [TestMethod]
    public async Task OwnSmellOnADroppedItemReachesTheOtherPlayers()
    {
        await processor.Process(new AuthProcessorContext(dropper, packetSender), new DroppedItemSmell(itemId, dropper.GameObjectId, 1f, 0.05f));

        // Whoever simulates a Stalker that picks the item up needs it, whether or not they see the item now
        await packetSender.Received(1).SendPacketToOthersAsync(Arg.Any<DroppedItemSmell>(), dropper.SessionId);
    }

    [TestMethod]
    public void PlayerCannotLeaveSomebodyElsesSmell()
    {
        Assert.IsFalse(DroppedItemSmellProcessor.IsValid(new DroppedItemSmell(itemId, other.GameObjectId, 1f, 0.05f), dropper));
    }

    [TestMethod]
    [DataRow(0f, 0.05f)]
    [DataRow(2f, 0.05f)]
    [DataRow(1f, 0f)]
    [DataRow(float.NaN, 0.05f)]
    public void SmellUnlikeTheOneTheGameLeavesIsRefused(float strength, float falloff)
    {
        Assert.IsFalse(DroppedItemSmellProcessor.IsValid(new DroppedItemSmell(itemId, dropper.GameObjectId, strength, falloff), dropper));
    }
}
