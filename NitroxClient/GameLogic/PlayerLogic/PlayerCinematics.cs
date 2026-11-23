using System.Collections.Generic;
using Nitrox.Model.Core;
using NitroxClient.Communication.Abstract;
using NitroxClient.MonoBehaviours;
using NitroxClient.MonoBehaviours.CinematicController;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.Packets;
using UnityEngine;

namespace NitroxClient.GameLogic.PlayerLogic;

public class PlayerCinematics
{
    private readonly IPacketSender packetSender;
    private readonly LocalPlayer localPlayer;
    private readonly PlayerManager playerManager;

    private IntroCinematicMode lastModeToSend = IntroCinematicMode.NONE;

    /// <summary>
    ///     The other player whose character plays the second seat of this game's intro.
    /// </summary>
    private RemotePlayer? introPartner;

    public SessionId? IntroCinematicPartnerId = null;

    /// <summary>
    /// Some cinematics should not be played. Example the intro as it's completely handled by a dedicated system.
    /// </summary>
    private readonly HashSet<string> blacklistedKeys = ["escapepod_intro", "reaper_attack"];

    public PlayerCinematics(IPacketSender packetSender, LocalPlayer localPlayer, PlayerManager playerManager)
    {
        this.packetSender = packetSender;
        this.localPlayer = localPlayer;
        this.playerManager = playerManager;
    }

    public void StartCinematicMode(SessionId sessionId, NitroxId controllerID, int controllerNameHash, string key, Dictionary<string, bool> animationParameters = null)
    {
        if (!blacklistedKeys.Contains(key))
        {
            packetSender.Send(new PlayerCinematicControllerCall(sessionId, controllerID, controllerNameHash, key, true, animationParameters));
        }
    }

    public void EndCinematicMode(SessionId sessionId, NitroxId controllerID, int controllerNameHash, string key)
    {
        if (!blacklistedKeys.Contains(key))
        {
            packetSender.Send(new PlayerCinematicControllerCall(sessionId, controllerID, controllerNameHash, key, false));
        }
    }

    /// <summary>
    ///     Whether another player is in the middle of the cinematic of <paramref name="cinematicController" />, as seen in
    ///     this game.
    /// </summary>
    public bool IsUsedByOtherPlayer(PlayerCinematicController cinematicController)
    {
        return cinematicController &&
               cinematicController.TryGetComponent(out MultiplayerCinematicController multiplayerController) &&
               multiplayerController.IsRemoteCinematicActive();
    }

    public void SetLocalIntroCinematicMode(IntroCinematicMode introCinematicMode)
    {
        if (!localPlayer.SessionId.HasValue)
        {
            Log.Error($"{nameof(SessionId)} was null while setting IntroCinematicMode to {introCinematicMode}");
            return;
        }

        if (localPlayer.IntroCinematicMode == introCinematicMode)
        {
            return;
        }

        localPlayer.IntroCinematicMode = introCinematicMode;

        // This method can be called before client is joined. To prevent sending as an unauthenticated packet we delay it.
        if (Multiplayer.Joined)
        {
            packetSender.Send(new SetIntroCinematicMode(localPlayer.SessionId.Value, introCinematicMode));
            return;
        }

        if (lastModeToSend == IntroCinematicMode.NONE)
        {
            Multiplayer.OnLoadingComplete += SendModeSetBeforeJoining;
        }

        lastModeToSend = introCinematicMode;
    }

    /// <summary>
    ///     Sends the last mode set before this player joined. Going through <see cref="SetLocalIntroCinematicMode" /> again
    ///     would send nothing: the local mode is that mode already.
    /// </summary>
    private void SendModeSetBeforeJoining()
    {
        Multiplayer.OnLoadingComplete -= SendModeSetBeforeJoining;
        IntroCinematicMode mode = lastModeToSend;
        lastModeToSend = IntroCinematicMode.NONE;
        if (mode == IntroCinematicMode.NONE || !localPlayer.SessionId.HasValue)
        {
            return;
        }
        packetSender.Send(new SetIntroCinematicMode(localPlayer.SessionId.Value, mode));
    }

    /// <summary>
    ///     Makes another player's character this game's partner in the intro: the intro animates and places it
    ///     (<see cref="IntroCinematicUpdater" />), mirrored.
    /// </summary>
    public void StartIntroWithPartner(RemotePlayer partner)
    {
        introPartner = partner;
        partner.PlayerModel.transform.localScale = new Vector3(-1, 1, 1);
        partner.ArmsController.enabled = false;
        partner.AnimationController.UpdatePlayerAnimations = false;
        partner.AnimationController["cinematics_enabled"] = true;
        partner.AnimationController["escapepod_intro"] = true;

        IntroCinematicUpdater.Partner = partner;
        partner.Body.AddComponent<IntroCinematicUpdater>();
    }

    /// <summary>
    ///     Gives the intro partner's character back to that player, when the intro ends or the partner turns out to play
    ///     the intro alone.
    /// </summary>
    public void EndIntroWithPartner()
    {
        RemotePlayer partner = introPartner;
        introPartner = null;
        // A partner who left has no character any more
        if (partner == null || !playerManager.Find(partner.SessionId).HasValue)
        {
            return;
        }

        IntroCinematicUpdater introCinematicUpdater = partner.Body.GetComponent<IntroCinematicUpdater>();
        if (introCinematicUpdater)
        {
            Object.DestroyImmediate(introCinematicUpdater);
            IntroCinematicUpdater.Partner = null;
        }

        partner.PlayerModel.transform.localScale = new Vector3(1, 1, 1);
        partner.AnimationController["cinematics_enabled"] = false;
        partner.AnimationController["escapepod_intro"] = false;
        partner.ArmsController.enabled = true;
        partner.AnimationController.UpdatePlayerAnimations = true;
    }

    /// <summary>
    ///     Called when another player's intro mode changes. The server pairs players who wait; one of them may have chosen
    ///     to play alone at that moment, after this game had begun the intro with it.
    /// </summary>
    public void RemoteIntroModeChanged(SessionId sessionId, IntroCinematicMode mode)
    {
        if (IsPartnerPlayingAlone(introPartner?.SessionId, sessionId, mode))
        {
            Log.Info($"The intro partner {sessionId} plays the intro alone; this game plays on without it");
            EndIntroWithPartner();
        }
    }

    internal static bool IsPartnerPlayingAlone(SessionId? partnerId, SessionId sessionId, IntroCinematicMode mode)
    {
        return mode == IntroCinematicMode.SINGLEPLAYER && partnerId.HasValue && partnerId.Value.Equals(sessionId);
    }
}
