using System.Net;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Nitrox.Model.Configuration;
using Nitrox.Model.DataStructures;
using Nitrox.Model.DataStructures.Unity;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities;
using Nitrox.Model.Subnautica.Packets;
using Nitrox.Server.Subnautica.Models.AppEvents;
using Nitrox.Server.Subnautica.Models.Communication;
using Nitrox.Server.Subnautica.Models.Factories;
using Nitrox.Server.Subnautica.Models.GameLogic.Entities;
using Nitrox.Server.Subnautica.Models.Packets.Core;
using Nitrox.Test.Server.Helper;
using NSubstitute;

namespace Nitrox.Server.Subnautica.Models.GameLogic;

/// <summary>
///     Two new players who wait for the intro play it together. A player whose partner leaves before both play is put
///     back to waiting; its game would otherwise wait for a partner who is gone.
/// </summary>
[TestClass]
public sealed class IntroCinematicPairingTest
{
    private ConnectedPlayers players;
    private IPacketSender packetSender;
    private IntroCinematicPairing pairing;
    private Player first;
    private Player second;
    private Player third;

    [TestInitialize]
    public async Task Init()
    {
        players = new ConnectedPlayers();
        packetSender = Substitute.For<IPacketSender>();
        first = players.Connect("First");
        second = players.Connect("Second");
        third = players.Connect("Third");

        IOptions<SubnauticaServerOptions> options = Options.Create(new SubnauticaServerOptions { Seed = "TESTSEED" });
        EscapePodManager escapePodManager = new(new RandomFactory(options), new EntityRegistry(NullLogger<EntityRegistry>.Instance), null, options, packetSender);
        EscapePodEntity pod = new(NitroxVector3.Zero, new NitroxId(), null);
        pod.Players.AddRange([first.PeerId, second.PeerId, third.PeerId]);
        await escapePodManager.AddKnownPodsAsync([pod]);

        pairing = new IntroCinematicPairing(players.PlayerManager, escapePodManager, packetSender, NullLogger<IntroCinematicPairing>.Instance);
    }

    [TestMethod]
    [DataRow(IntroCinematicMode.LOADING, IntroCinematicMode.WAITING)]
    [DataRow(IntroCinematicMode.LOADING, IntroCinematicMode.COMPLETED)]
    [DataRow(IntroCinematicMode.LOADING, IntroCinematicMode.SINGLEPLAYER)]
    [DataRow(IntroCinematicMode.WAITING, IntroCinematicMode.SINGLEPLAYER)]
    [DataRow(IntroCinematicMode.WAITING, IntroCinematicMode.PLAYING)]
    [DataRow(IntroCinematicMode.START, IntroCinematicMode.PLAYING)]
    [DataRow(IntroCinematicMode.START, IntroCinematicMode.SINGLEPLAYER)]
    [DataRow(IntroCinematicMode.PLAYING, IntroCinematicMode.COMPLETED)]
    [DataRow(IntroCinematicMode.SINGLEPLAYER, IntroCinematicMode.COMPLETED)]
    public void TheStepsAGameTakesThroughTheIntroAreAllowed(IntroCinematicMode current, IntroCinematicMode requested)
    {
        Assert.IsTrue(IntroCinematicPairing.IsTransitionAllowed(current, requested));
    }

    [TestMethod]
    [DataRow(IntroCinematicMode.COMPLETED, IntroCinematicMode.WAITING)]
    [DataRow(IntroCinematicMode.COMPLETED, IntroCinematicMode.PLAYING)]
    [DataRow(IntroCinematicMode.COMPLETED, IntroCinematicMode.SINGLEPLAYER)]
    [DataRow(IntroCinematicMode.PLAYING, IntroCinematicMode.WAITING)]
    [DataRow(IntroCinematicMode.SINGLEPLAYER, IntroCinematicMode.WAITING)]
    [DataRow(IntroCinematicMode.LOADING, IntroCinematicMode.START)]
    [DataRow(IntroCinematicMode.WAITING, IntroCinematicMode.START)]
    [DataRow(IntroCinematicMode.WAITING, IntroCinematicMode.LOADING)]
    [DataRow(IntroCinematicMode.LOADING, IntroCinematicMode.PLAYING)]
    public void StepsNoGameTakesAreRefused(IntroCinematicMode current, IntroCinematicMode requested)
    {
        // START is the server's to give; an intro that is over does not start again.
        Assert.IsFalse(IntroCinematicPairing.IsTransitionAllowed(current, requested));
    }

    [TestMethod]
    public async Task TwoWaitingPlayersAreToldToStartTogether()
    {
        first.PlayerContext.IntroCinematicMode = IntroCinematicMode.WAITING;
        second.PlayerContext.IntroCinematicMode = IntroCinematicMode.WAITING;

        await pairing.PairWaitingPlayersAsync();

        Assert.AreEqual(IntroCinematicMode.START, first.PlayerContext.IntroCinematicMode);
        Assert.AreEqual(IntroCinematicMode.START, second.PlayerContext.IntroCinematicMode);
        List<SetIntroCinematicMode> sent = ConnectedPlayers.SentToAll<SetIntroCinematicMode>(packetSender);
        Assert.AreEqual(second.SessionId, sent.Single(packet => packet.SessionId == first.SessionId).PartnerId);
        Assert.AreEqual(first.SessionId, sent.Single(packet => packet.SessionId == second.SessionId).PartnerId);
    }

    [TestMethod]
    public async Task OneWaitingPlayerKeepsWaiting()
    {
        first.PlayerContext.IntroCinematicMode = IntroCinematicMode.WAITING;

        await pairing.PairWaitingPlayersAsync();

        Assert.AreEqual(IntroCinematicMode.WAITING, first.PlayerContext.IntroCinematicMode);
        Assert.AreEqual(0, packetSender.ReceivedCalls().Count());
    }

    [TestMethod]
    public async Task APlayerWhosePartnerLeftBeforeTheIntroStartedWaitsAgainAndIsPairedWithTheNext()
    {
        first.PlayerContext.IntroCinematicMode = IntroCinematicMode.WAITING;
        second.PlayerContext.IntroCinematicMode = IntroCinematicMode.WAITING;
        await pairing.PairWaitingPlayersAsync();
        third.PlayerContext.IntroCinematicMode = IntroCinematicMode.WAITING;
        packetSender.ClearReceivedCalls();

        await Leave(second);

        List<SetIntroCinematicMode> sent = ConnectedPlayers.SentToAll<SetIntroCinematicMode>(packetSender);
        SetIntroCinematicMode waitAgain = sent.First();
        Assert.AreEqual((first.SessionId, IntroCinematicMode.WAITING), (waitAgain.SessionId, waitAgain.Mode));
        Assert.IsNull(waitAgain.PartnerId, "The client takes a WAITING without a partner as 'wait again'.");
        Assert.AreEqual(third.SessionId, sent.Single(packet => packet.SessionId == first.SessionId && packet.Mode == IntroCinematicMode.START).PartnerId);
        Assert.AreEqual(IntroCinematicMode.START, first.PlayerContext.IntroCinematicMode);
        Assert.AreEqual(IntroCinematicMode.START, third.PlayerContext.IntroCinematicMode);
    }

    [TestMethod]
    public async Task APlayerWhosePartnerLeftDuringTheIntroPlaysOn()
    {
        first.PlayerContext.IntroCinematicMode = IntroCinematicMode.WAITING;
        second.PlayerContext.IntroCinematicMode = IntroCinematicMode.WAITING;
        await pairing.PairWaitingPlayersAsync();
        first.PlayerContext.IntroCinematicMode = IntroCinematicMode.PLAYING;
        packetSender.ClearReceivedCalls();

        await Leave(second);

        Assert.AreEqual(IntroCinematicMode.PLAYING, first.PlayerContext.IntroCinematicMode);
        Assert.AreEqual(0, packetSender.ReceivedCalls().Count());
    }

    [TestMethod]
    public async Task APlayerWhoWasNeverPairedLeavesWithoutEffect()
    {
        first.PlayerContext.IntroCinematicMode = IntroCinematicMode.START;

        await Leave(second);

        Assert.AreEqual(IntroCinematicMode.START, first.PlayerContext.IntroCinematicMode);
        Assert.AreEqual(0, packetSender.ReceivedCalls().Count());
    }

    private Task Leave(Player player)
    {
        return pairing.OnEventAsync(new ISessionCleaner.Args(new SessionManager.Session(player.SessionId, new IPEndPoint(IPAddress.Loopback, 11000)), 2));
    }
}
