using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Nitrox.Model.Configuration;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.Packets;
using Nitrox.Server.Subnautica.Models.Factories;
using Nitrox.Server.Subnautica.Models.GameLogic;
using Nitrox.Server.Subnautica.Models.GameLogic.Entities;
using Nitrox.Server.Subnautica.Models.Packets.Core;
using Nitrox.Test.Server.Helper;
using NSubstitute;

namespace Nitrox.Server.Subnautica.Models.Packets.Processors;

/// <summary>
///     A client telling the server where its intro is. Only steps a game really takes are stored and passed on.
/// </summary>
[TestClass]
public sealed class SetIntroCinematicModeProcessorTest
{
    private ConnectedPlayers players;
    private IPacketSender packetSender;
    private SetIntroCinematicModeProcessor processor;
    private Player sender;
    private Player otherPlayer;

    [TestInitialize]
    public void Init()
    {
        players = new ConnectedPlayers();
        packetSender = Substitute.For<IPacketSender>();
        IOptions<SubnauticaServerOptions> options = Options.Create(new SubnauticaServerOptions { Seed = "TESTSEED" });
        EscapePodManager escapePodManager = new(new RandomFactory(options), new EntityRegistry(NullLogger<EntityRegistry>.Instance), null, options, packetSender);
        IntroCinematicPairing pairing = new(players.PlayerManager, escapePodManager, packetSender, NullLogger<IntroCinematicPairing>.Instance);
        processor = new SetIntroCinematicModeProcessor(pairing, NullLogger<SetIntroCinematicModeProcessor>.Instance);
        sender = players.Connect("Sender");
        otherPlayer = players.Connect("Other");
    }

    [TestMethod]
    public async Task AStepTheGameTakesIsStoredAndPassedOn()
    {
        sender.PlayerContext.IntroCinematicMode = IntroCinematicMode.LOADING;

        await Send(new SetIntroCinematicMode(sender.SessionId, IntroCinematicMode.WAITING));

        Assert.AreEqual(IntroCinematicMode.WAITING, sender.PlayerContext.IntroCinematicMode);
        await packetSender.Received(1).SendPacketToOthersAsync(Arg.Is<SetIntroCinematicMode>(packet => packet.Mode == IntroCinematicMode.WAITING), sender.SessionId);
    }

    [TestMethod]
    public async Task WaitingFromAPlayerWhoseIntroIsOverIsIgnored()
    {
        // Paired with a new player, that player would wait for an intro that never starts on this side.
        sender.PlayerContext.IntroCinematicMode = IntroCinematicMode.COMPLETED;
        otherPlayer.PlayerContext.IntroCinematicMode = IntroCinematicMode.WAITING;

        await Send(new SetIntroCinematicMode(sender.SessionId, IntroCinematicMode.WAITING));

        Assert.AreEqual(IntroCinematicMode.COMPLETED, sender.PlayerContext.IntroCinematicMode);
        Assert.AreEqual(IntroCinematicMode.WAITING, otherPlayer.PlayerContext.IntroCinematicMode);
        Assert.AreEqual(0, packetSender.ReceivedCalls().Count());
    }

    [TestMethod]
    public async Task StartFromAClientIsIgnored()
    {
        sender.PlayerContext.IntroCinematicMode = IntroCinematicMode.WAITING;

        await Send(new SetIntroCinematicMode(sender.SessionId, IntroCinematicMode.START));

        Assert.AreEqual(IntroCinematicMode.WAITING, sender.PlayerContext.IntroCinematicMode);
        Assert.AreEqual(0, packetSender.ReceivedCalls().Count());
    }

    [TestMethod]
    public async Task APacketForAnotherPlayerIsIgnored()
    {
        otherPlayer.PlayerContext.IntroCinematicMode = IntroCinematicMode.LOADING;

        await Send(new SetIntroCinematicMode(otherPlayer.SessionId, IntroCinematicMode.WAITING));

        Assert.AreEqual(IntroCinematicMode.LOADING, otherPlayer.PlayerContext.IntroCinematicMode);
        Assert.AreEqual(0, packetSender.ReceivedCalls().Count());
    }

    private Task Send(SetIntroCinematicMode packet)
    {
        return processor.Process(new AuthProcessorContext(sender, packetSender), packet);
    }
}
