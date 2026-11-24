using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Nitrox.Model.Core;
using Nitrox.Model.Subnautica.Packets;
using Nitrox.Server.Subnautica.Models.Administration;
using Nitrox.Server.Subnautica.Models.Packets.Core;
using Nitrox.Test.Server.Helper;
using NSubstitute;

namespace Nitrox.Server.Subnautica.Models.Packets.Processors;

/// <summary>
///     Chat was passed on as the game sent it: with the sender the game named (the server, or anyone), any length and as
///     fast as a script could send.
/// </summary>
[TestClass]
public sealed class ChatMessageProcessorTest
{
    private readonly ConnectedPlayers players = new();
    private readonly IPacketSender packetSender = Substitute.For<IPacketSender>();
    private readonly ChatMessageProcessor processor = new(new ChatRateLimiter(), NullLogger<ChatMessageProcessor>.Instance);

    [TestMethod]
    public async Task AMessageCarriesItsRealSender()
    {
        Player bob = players.Connect("Bob");

        await processor.Process(new AuthProcessorContext(bob, packetSender), new ChatMessage(SessionId.SERVER_ID, "I am the server"));

        ConnectedPlayers.SentToAll<ChatMessage>(packetSender).Single().SessionId.Should().Be(bob.SessionId);
    }

    [TestMethod]
    public async Task ALongMessageIsCut()
    {
        Player bob = players.Connect("Bob");

        await processor.Process(new AuthProcessorContext(bob, packetSender), new ChatMessage(bob.SessionId, new string('a', 5000)));

        ConnectedPlayers.SentToAll<ChatMessage>(packetSender).Single().Text.Length.Should().Be(ChatMessageProcessor.MAX_MESSAGE_LENGTH);
    }

    [TestMethod]
    public async Task AFloodIsToldToSlowDown()
    {
        Player bob = players.Connect("Bob");
        for (int i = 0; i < ChatRateLimiter.BURST + 3; i++)
        {
            await processor.Process(new AuthProcessorContext(bob, packetSender), new ChatMessage(bob.SessionId, $"spam {i}"));
        }

        ConnectedPlayers.SentToAll<ChatMessage>(packetSender).Should().HaveCount(ChatRateLimiter.BURST);
        ConnectedPlayers.SentTo<ChatMessage>(packetSender, bob).Should().Contain(message => message.Text.Contains("Slow down"));
    }
}
