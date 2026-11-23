using System.Collections.Generic;
using System.Linq;
using Nitrox.Model.Core;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.Packets;
using Nitrox.Server.Subnautica.Models.AppEvents;
using Nitrox.Server.Subnautica.Models.Packets.Core;

namespace Nitrox.Server.Subnautica.Models.GameLogic;

/// <summary>
///     Pairs two new players who wait for the intro so that they play it together, and puts a player whose partner
///     leaves or chooses to play alone before both play back to waiting: its game would otherwise start the intro with a
///     partner who is not in it.
/// </summary>
internal sealed class IntroCinematicPairing(PlayerManager playerManager, EscapePodManager escapePodManager, IPacketSender packetSender, ILogger<IntroCinematicPairing> logger) : ISessionCleaner
{
    private readonly PlayerManager playerManager = playerManager;
    private readonly EscapePodManager escapePodManager = escapePodManager;
    private readonly IPacketSender packetSender = packetSender;
    private readonly ILogger<IntroCinematicPairing> logger = logger;
    private readonly Dictionary<SessionId, SessionId> partnerBySession = [];
    private readonly Lock partnersLock = new();

    /// <summary>
    ///     Whether a client moves its intro from <paramref name="current" /> to <paramref name="requested" />
    ///     (uGUI_SceneIntro_IntroSequence_Patch, uGUI_SceneIntro_HandleInput_Patch). START is only given by the server.
    /// </summary>
    public static bool IsTransitionAllowed(IntroCinematicMode current, IntroCinematicMode requested) => requested switch
    {
        IntroCinematicMode.WAITING => current is IntroCinematicMode.NONE or IntroCinematicMode.LOADING,
        // From LOADING too: a mode set before the client has joined is sent once it has, and only the last one.
        IntroCinematicMode.SINGLEPLAYER => current is IntroCinematicMode.NONE or IntroCinematicMode.LOADING or IntroCinematicMode.WAITING or IntroCinematicMode.START,
        // From WAITING too: the server may have put the player back to waiting when the partner left while its game
        // was starting the intro already.
        IntroCinematicMode.PLAYING => current is IntroCinematicMode.START or IntroCinematicMode.WAITING,
        // From NONE or LOADING: a game that skips the intro (creative, debug) goes there straight away.
        IntroCinematicMode.COMPLETED => current is IntroCinematicMode.NONE or IntroCinematicMode.LOADING or IntroCinematicMode.PLAYING or IntroCinematicMode.SINGLEPLAYER,
        _ => false
    };

    /// <summary>
    ///     Starts the intro for the first two players who wait for it.
    /// </summary>
    public async Task PairWaitingPlayersAsync()
    {
        Player[] allWaitingPlayers = playerManager.ConnectedPlayers().Where(p => p.PlayerContext.IntroCinematicMode == IntroCinematicMode.WAITING).ToArray();
        if (allWaitingPlayers.Length < 2)
        {
            return;
        }

        Player playerA = allWaitingPlayers[0];
        Player playerB = allWaitingPlayers[1];

        logger.ZLogInformation($"Starting IntroCinematic for {playerA.PlayerContext.PlayerName} and {playerB.PlayerContext.PlayerName}");

        playerA.PlayerContext.IntroCinematicMode = playerB.PlayerContext.IntroCinematicMode = IntroCinematicMode.START;
        lock (partnersLock)
        {
            partnerBySession[playerA.SessionId] = playerB.SessionId;
            partnerBySession[playerB.SessionId] = playerA.SessionId;
        }

        await packetSender.SendPacketToAllAsync(new SetIntroCinematicMode(playerA.SessionId, IntroCinematicMode.START, playerB.SessionId));
        await packetSender.SendPacketToAllAsync(new SetIntroCinematicMode(playerB.SessionId, IntroCinematicMode.START, playerA.SessionId));

        await escapePodManager.SetupIntroSequenceAsync(playerA, playerB);
    }

    public Task OnEventAsync(ISessionCleaner.Args args) => ReleasePartnerAsync(args.Session.Id);

    /// <summary>
    ///     Ends the pairing of a player who leaves, or who chose to play the intro alone at the moment it was paired. Its
    ///     partner waits again if its intro has not started yet.
    /// </summary>
    public async Task ReleasePartnerAsync(SessionId sessionId)
    {
        SessionId partnerId;
        lock (partnersLock)
        {
            if (!partnerBySession.Remove(sessionId, out partnerId))
            {
                return;
            }
            partnerBySession.Remove(partnerId);
        }

        // A partner who plays already plays on alone; the game copes with the other player's model being gone.
        if (!playerManager.TryGetPlayerBySessionId(partnerId, out Player? partner) || partner.PlayerContext.IntroCinematicMode != IntroCinematicMode.START)
        {
            return;
        }

        logger.ZLogInformation($"The intro partner of {partner.PlayerContext.PlayerName} is gone before the intro started; waiting for another one");
        partner.PlayerContext.IntroCinematicMode = IntroCinematicMode.WAITING;
        // Without a partner id: the client takes it as "wait again" (SetIntroCinematicModeProcessor).
        await packetSender.SendPacketToAllAsync(new SetIntroCinematicMode(partner.SessionId, IntroCinematicMode.WAITING));
        await PairWaitingPlayersAsync();
    }
}
