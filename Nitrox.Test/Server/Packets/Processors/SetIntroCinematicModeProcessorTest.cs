using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Nitrox.Model.Configuration;
using Nitrox.Model.DataStructures;
using Nitrox.Model.DataStructures.Unity;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities;
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
    private IntroCinematicPairing pairing;
    private SetIntroCinematicModeProcessor processor;
    private Player sender;
    private Player otherPlayer;
    private Player thirdPlayer;

    [TestInitialize]
    public async Task Init()
    {
        players = new ConnectedPlayers();
        packetSender = Substitute.For<IPacketSender>();
        sender = players.Connect("Sender");
        otherPlayer = players.Connect("Other");
        thirdPlayer = players.Connect("Third");
        IOptions<SubnauticaServerOptions> options = Options.Create(new SubnauticaServerOptions { Seed = "TESTSEED" });
        EscapePodManager escapePodManager = new(new RandomFactory(options), new EntityRegistry(NullLogger<EntityRegistry>.Instance), null, options, packetSender);
        EscapePodEntity pod = new(NitroxVector3.Zero, new NitroxId(), null);
        pod.Players.AddRange([sender.PeerId, otherPlayer.PeerId, thirdPlayer.PeerId]);
        await escapePodManager.AddKnownPodsAsync([pod]);
        pairing = new(players.PlayerManager, escapePodManager, packetSender, NullLogger<IntroCinematicPairing>.Instance);
        processor = new SetIntroCinematicModeProcessor(pairing, NullLogger<SetIntroCinematicModeProcessor>.Instance);
    }

    [TestMethod]
    public async Task APlayerWhoseNewPartnerChoseToPlayAloneWaitsAgainAndIsPairedWithTheNext()
    {
        // The server pairs both while the sender's game is already on its way to playing alone
        sender.PlayerContext.IntroCinematicMode = IntroCinematicMode.WAITING;
        otherPlayer.PlayerContext.IntroCinematicMode = IntroCinematicMode.WAITING;
        await pairing.PairWaitingPlayersAsync();
        thirdPlayer.PlayerContext.IntroCinematicMode = IntroCinematicMode.WAITING;
        packetSender.ClearReceivedCalls();

        await Send(new SetIntroCinematicMode(sender.SessionId, IntroCinematicMode.SINGLEPLAYER));

        Assert.AreEqual(IntroCinematicMode.SINGLEPLAYER, sender.PlayerContext.IntroCinematicMode);
        List<SetIntroCinematicMode> sent = ConnectedPlayers.SentToAll<SetIntroCinematicMode>(packetSender);
        SetIntroCinematicMode waitAgain = sent.First();
        Assert.AreEqual((otherPlayer.SessionId, IntroCinematicMode.WAITING), (waitAgain.SessionId, waitAgain.Mode));
        Assert.IsNull(waitAgain.PartnerId, "The client takes a WAITING without a partner as 'wait again'.");
        Assert.AreEqual(thirdPlayer.SessionId, sent.Single(packet => packet.SessionId == otherPlayer.SessionId && packet.Mode == IntroCinematicMode.START).PartnerId);
    }

    [TestMethod]
    public async Task APartnerWhoseIntroPlaysAlreadyPlaysOnWhenTheOtherChoseToPlayAlone()
    {
        sender.PlayerContext.IntroCinematicMode = IntroCinematicMode.WAITING;
        otherPlayer.PlayerContext.IntroCinematicMode = IntroCinematicMode.WAITING;
        await pairing.PairWaitingPlayersAsync();
        otherPlayer.PlayerContext.IntroCinematicMode = IntroCinematicMode.PLAYING;
        packetSender.ClearReceivedCalls();

        await Send(new SetIntroCinematicMode(sender.SessionId, IntroCinematicMode.SINGLEPLAYER));

        Assert.AreEqual(IntroCinematicMode.PLAYING, otherPlayer.PlayerContext.IntroCinematicMode);
        Assert.AreEqual(0, ConnectedPlayers.SentToAll<SetIntroCinematicMode>(packetSender).Count);
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
