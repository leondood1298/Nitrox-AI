using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Nitrox.Model.Configuration;
using Nitrox.Model.Subnautica.Packets;
using Nitrox.Server.Subnautica.Models.Packets.Core;
using Nitrox.Test.Server.Helper;
using NSubstitute;

namespace Nitrox.Server.Subnautica.Models.Packets.Processors;

/// <summary>
///     A game asking for the server's address to publish on Discord. The address reaches whoever a player accepts there,
///     so the host decides.
/// </summary>
[TestClass]
public sealed class DiscordRequestIPProcessorTest
{
    [TestMethod]
    public async Task AServerThatDoesNotAllowDiscordInvitesGivesNoGameItsAddress()
    {
        ConnectedPlayers players = new();
        Player sender = players.Connect("Sender");
        IPacketSender packetSender = Substitute.For<IPacketSender>();
        DiscordRequestIPProcessor processor = new(Options.Create(new SubnauticaServerOptions()), NullLogger<DiscordRequestIPProcessor>.Instance);

        await processor.Process(new AuthProcessorContext(sender, packetSender), new DiscordRequestIP(string.Empty));

        ConnectedPlayers.SentTo<DiscordRequestIP>(packetSender, sender).Should().BeEmpty();
    }

    [TestMethod]
    public void DiscordInvitesAreOffUntilTheHostTurnsThemOn()
    {
        new SubnauticaServerOptions().AllowDiscordInvites.Should().BeFalse();
    }
}
