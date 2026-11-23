using System.Threading.Tasks;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.Packets;
using Nitrox.Server.Subnautica.Models.GameLogic;
using Nitrox.Server.Subnautica.Models.Packets.Core;
using Nitrox.Test.Server.Helper;
using NSubstitute;

namespace Nitrox.Server.Subnautica.Models.Packets.Processors;

/// <summary>
///     A found time capsule's encyclopedia entry reaches the other players once, and is kept for the ones who join later.
/// </summary>
[TestClass]
public sealed class TimeCapsuleCollectedProcessorTest
{
    private readonly ConnectedPlayers players = new();
    private readonly CollectedTimeCapsules collectedTimeCapsules = new();
    private readonly IPacketSender packetSender = Substitute.For<IPacketSender>();
    private TimeCapsuleCollectedProcessor processor;

    [TestInitialize]
    public void Init()
    {
        processor = new TimeCapsuleCollectedProcessor(collectedTimeCapsules);
    }

    [TestMethod]
    public async Task CollectedCapsuleIsKeptAndSentToTheOthers()
    {
        Player collector = players.Connect("Collector");
        TimeCapsuleCollected packet = new(new CollectedTimeCapsule("capsule-a", "Greetings", "We were here", "a.jpg"));

        await processor.Process(new AuthProcessorContext(collector, packetSender), packet);

        collectedTimeCapsules.GetAll().Should().ContainSingle().Which.Should().BeSameAs(packet.Capsule);
        await packetSender.Received(1).SendPacketToOthersAsync(packet, collector.SessionId);
    }

    [TestMethod]
    public async Task CapsuleWithContentCollectedBeforeIsNotSentAgain()
    {
        Player first = players.Connect("First");
        Player second = players.Connect("Second");
        await processor.Process(new AuthProcessorContext(first, packetSender), new TimeCapsuleCollected(new CollectedTimeCapsule("capsule-a", "Greetings", "", "")));
        packetSender.ClearReceivedCalls();

        await processor.Process(new AuthProcessorContext(second, packetSender), new TimeCapsuleCollected(new CollectedTimeCapsule("capsule-a", "Greetings", "", "")));

        packetSender.ReceivedCalls().Should().BeEmpty("every player has the entry already");
        collectedTimeCapsules.GetAll().Should().ContainSingle();
    }
}
