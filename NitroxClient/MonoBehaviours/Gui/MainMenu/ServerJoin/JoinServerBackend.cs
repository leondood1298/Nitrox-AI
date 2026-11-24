using System;
using System.Collections;
using System.Diagnostics;
using System.Net;
using System.Threading.Tasks;
using NitroxClient.Communication.Abstract;
using NitroxClient.Communication.Exceptions;
using NitroxClient.Communication.MultiplayerSession;
using NitroxClient.GameLogic.PlayerLogic.PlayerPreferences;
using NitroxClient.GameLogic.Settings;
using NitroxClient.MonoBehaviours.Gui.InGame;
using NitroxClient.MonoBehaviours.Gui.MainMenu.ServersList;
using NitroxClient.MonoBehaviours.Gui.Modals;
using Nitrox.Model.Core;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Helper;
using Nitrox.Model.MultiplayerSession;
using Nitrox.Model.Networking;
using Nitrox.Model.Subnautica.DataStructures;
using Nitrox.Model.Subnautica.MultiplayerSession;
using Nitrox.Model.Subnautica.Packets;
using UnityEngine;
using UWE;

namespace NitroxClient.MonoBehaviours.Gui.MainMenu.ServerJoin;

/// <summary>
///     Drives one attempt to join a server from the main menu. Every way an attempt can end before the world loads ends
///     in <see cref="StopMultiplayerClient" /> and a message on the notification panel, so the next attempt starts clean.
/// </summary>
public static class JoinServerBackend
{
    private static PlayerPreferenceManager preferencesManager;
    private static PlayerPreference activePlayerPreference;
    private static IMultiplayerSession multiplayerSession;

    private static GameObject multiplayerClient;

    private static string serverIp;
    private static int serverPort;

    /// <summary>
    ///     Changes whenever an attempt starts or stops, so a timer or a deferred failure of an attempt that already ended does
    ///     nothing to the next one.
    /// </summary>
    private static int attempt;
    private static bool stopping;
    private static bool gameStarted;

    /// <summary>
    ///     True from connecting until the world starts to load: a lost connection then belongs on the main menu, not in the
    ///     in-game menu.
    /// </summary>
    public static bool IsJoiningFromMenu => multiplayerClient && !gameStarted;

    public static void RequestSessionReservation(string playerName, Color playerColor)
    {
        // A second press while the server's reply is awaited would restart the negotiation and drop that reply
        if (multiplayerSession == null || multiplayerSession.CurrentState.CurrentStage != MultiplayerSessionConnectionStage.AWAITING_RESERVATION_CREDENTIALS)
        {
            return;
        }

        preferencesManager.SetPreference(serverIp, new PlayerPreference(playerName, playerColor));

        Optional<string> opPassword = JoinPassword.ToSend(multiplayerSession.SessionPolicy.RequiresServerPassword, MainMenuEnterPasswordPanel.LastEnteredPassword);
        IMultiplayerSession session = multiplayerSession;
        MultiplayerSessionPolicy policy = session.SessionPolicy;
        int provingAttempt = attempt;

        MainMenuNotificationPanel.ShowLoading();
        // Keyed by the world, whatever address it is reached through; the first time, the key takes a moment to make
        CoroutineHost.StartCoroutine(PlayerKeys.ProveAsync(policy.WorldIdentity, policy.JoinChallenge, proof =>
        {
            // The player may have left this attempt while the key was being made
            if (attempt != provingAttempt || session != multiplayerSession || session.CurrentState.CurrentStage != MultiplayerSessionConnectionStage.AWAITING_RESERVATION_CREDENTIALS)
            {
                return;
            }
            if (proof == null)
            {
                Fail(Language.main.Get("Nitrox_PlayerKeyFailed"));
                return;
            }
            try
            {
                AuthenticationContext authenticationContext = new(playerName, opPassword, proof.PublicKey, proof.Signature);
                session.RequestSessionReservation(new PlayerSettings(playerColor.ToDto()), authenticationContext);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Asking the server for a reservation failed");
                Fail(Language.main.Get("Nitrox_DisconnectedSession"));
            }
        }));
    }

    private static void SessionConnectionStateChangedHandler(IMultiplayerSessionConnectionState state)
    {
        switch (state.CurrentStage)
        {
            case MultiplayerSessionConnectionStage.ESTABLISHING_SERVER_POLICY:
                Log.Info("Requesting session policy info");
                Log.InGame(Language.main.Get("Nitrox_RequestingSessionPolicy"));
                WatchForReply(state.CurrentStage);
                break;

            case MultiplayerSessionConnectionStage.AWAITING_RESERVATION_CREDENTIALS:
                Color.RGBToHSV(activePlayerPreference.PreferredColor(), out float hue, out float saturation, out float brightness); // HSV => Hue Saturation Value, HSB => Hue Saturation Brightness
                MainMenuJoinServerPanel.Instance.UpdatePlayerPanelValues(activePlayerPreference.PlayerName, new Vector3(hue, saturation, brightness));

                if (multiplayerSession.SessionPolicy.RequiresServerPassword)
                {
                    Log.Info("Waiting for server password input");
                    Log.InGame(Language.main.Get("Nitrox_WaitingPassword"));
                    MainMenuEnterPasswordPanel.ResetLastEnteredPassword();
                    MainMenuRightSide.main.OpenGroup(MainMenuEnterPasswordPanel.NAME);
                    MainMenuEnterPasswordPanel.Instance.FocusPasswordField();
                    break;
                }

                Log.Info("Waiting for user input");
                Log.InGame(Language.main.Get("Nitrox_WaitingUserInput"));
                MainMenuRightSide.main.OpenGroup(MainMenuJoinServerPanel.NAME);
                MainMenuJoinServerPanel.Instance.FocusNameInputField();
                break;

            case MultiplayerSessionConnectionStage.AWAITING_SESSION_RESERVATION:
                WatchForReply(state.CurrentStage);
                break;

            case MultiplayerSessionConnectionStage.SESSION_RESERVED:
                Log.Info("Launching game");
                Log.InGame(Language.main.Get("Nitrox_LaunchGame"));
                multiplayerSession.ConnectionStateChanged -= SessionConnectionStateChangedHandler;
                preferencesManager.Save();
                StartGame();
                break;

            case MultiplayerSessionConnectionStage.SESSION_RESERVATION_REJECTED:
                Log.Info("Reservation rejected");
                Log.InGame(Language.main.Get("Nitrox_RejectedSessionPolicy"));

                MultiplayerSessionReservationState reservationState = multiplayerSession.Reservation.ReservationState;
                string reservationRejectionNotification = ReservationRefusalText.Describe(reservationState, multiplayerSession.Reservation.RejectionReason, Language.main.Get);
                string ip = serverIp;
                int port = serverPort;

                // Every refusal ends this attempt; trying again is a new one, from a fresh connection
                StopMultiplayerClient();
                if (reservationState.HasStateFlag(MultiplayerSessionReservationState.PLAYER_BANNED))
                {
                    // Reconnecting can't clear a ban, so send the player back to the server list
                    MainMenuNotificationPanel.ShowMessage(reservationRejectionNotification, MainMenuServerListPanel.NAME);
                    break;
                }

                MainMenuNotificationPanel.ShowMessage(reservationRejectionNotification, null, () => MainMenuServerButton.OpenJoinServerMenuAsync(ip, port).ContinueWithHandleError(true));
                break;

            case MultiplayerSessionConnectionStage.DISCONNECTED:
                Log.Info(Language.main.Get("Nitrox_DisconnectedSession"));
                if (!stopping)
                {
                    // The session gave up on its own: an incompatible version, or a step that could not go on
                    Fail(multiplayerSession.DisconnectReason ?? Language.main.Get("Nitrox_DisconnectedSession"));
                }
                break;
        }
    }

    public static async Task StartMultiplayerClientAsync(IPAddress ip, int port)
    {
        serverIp = ip.ToString();
        serverPort = port;
        attempt++;
        stopping = false;
        gameStarted = false;
        Modal.ClearCurrent();
        NitroxServiceLocator.BeginNewLifetimeScope();

        preferencesManager = NitroxServiceLocator.LocateService<PlayerPreferenceManager>();
        activePlayerPreference = preferencesManager.GetPreference(serverIp);
        multiplayerSession = NitroxServiceLocator.LocateService<IMultiplayerSession>();

        if (!multiplayerClient)
        {
            multiplayerClient = new GameObject("Nitrox Multiplayer Client");
            multiplayerClient.AddComponent<Multiplayer>();
            multiplayerSession.ConnectionStateChanged += SessionConnectionStateChangedHandler;
        }

        try
        {
            await multiplayerSession.ConnectAsync(serverIp, serverPort);
        }
        catch (ClientConnectionFailedException ex)
        {
            Log.ErrorSensitive("Unable to contact the remote server at: {ip}:{port}", serverIp, serverPort);
            string msg;

            if (ex is MultiplayerProtocolMismatchException protocolMismatch && NitroxNetworkProtocol.TryGetEpoch(protocolMismatch.ServerConnectionKey, out int serverEpoch))
            {
                string languageKey = serverEpoch > NitroxNetworkProtocol.EPOCH ? "Nitrox_OutOfDateClient" : "Nitrox_OutOfDateServer";
                msg = Language.main.Get(languageKey)
                              .Replace("{serverVersion}", protocolMismatch.ServerConnectionKey)
                              .Replace("{localVersion}", protocolMismatch.ClientConnectionKey);
                Log.Error(ex);
            }
            else
            {
                msg = $"{Language.main.Get("Nitrox_UnableToConnect")} {ServerAddressText.Format(serverIp, serverPort, NitroxPrefs.HideIp.Value)}";
                if (ip.IsLocalhost())
                {
                    if (Process.GetProcessesByName("Nitrox.Server.Subnautica").Length == 0)
                    {
                        Log.Error("No server process was found while address was localhost");
                        msg += $"\n{Language.main.Get("Nitrox_StartServer")}";
                    }
                    else
                    {
                        Log.Error(ex);
                        msg += $"\n{Language.main.Get("Nitrox_FirewallInterfering")}";
                    }
                }
            }

            Log.InGame(msg);
            StopMultiplayerClient();
            MainMenuNotificationPanel.ShowMessage(msg, MainMenuServerListPanel.NAME);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Joining stopped on an unexpected error");
            StopMultiplayerClient();
            MainMenuNotificationPanel.ShowMessage(Language.main.Get("Nitrox_JoinUnexpectedError"), MainMenuServerListPanel.NAME);
        }
    }

    /// <summary>
    ///     This method starts a connection with the provided server but leaves handling the session negotiation for the caller.
    /// </summary>
    public static async Task StartDetachedMultiplayerClientAsync(IPAddress ip, int port, MultiplayerSessionConnectionStateChangedEventHandler sessionHandler)
    {
        multiplayerClient = new GameObject("Nitrox Multiplayer Client");
        Task task = StartMultiplayerClientAsync(ip, port);
        multiplayerClient.AddComponent<Multiplayer>();
        multiplayerSession.ConnectionStateChanged += sessionHandler;
        await task;
    }

    public static void StartGame()
    {
        gameStarted = true;
#pragma warning disable CS0618 // God Damn it UWE...
        Multiplayer.SubnauticaLoadingStarted();
        IEnumerator startNewGame = uGUI_MainMenu.main.StartNewGame(GameMode.Survival);
#pragma warning restore CS0618 // God damn it UWE...
        UWE.CoroutineHost.StartCoroutine(startNewGame);
        TopRightWatermarkText.Initialize();
    }

    /// <summary>
    ///     The connection ended while the player was still in the main menu.
    /// </summary>
    public static void OnConnectionLost()
    {
        Log.Info("The connection to the server ended before joining");
        Fail(Language.main.Get("Nitrox_LostConnection"));
    }

    public static void StopMultiplayerClient()
    {
        // Whatever ends an attempt, the password typed for it is not kept for the next server
        MainMenuEnterPasswordPanel.ResetLastEnteredPassword();
        attempt++;

        if (!multiplayerClient || !Multiplayer.Main)
        {
            return;
        }

        stopping = true;
        try
        {
            if (multiplayerSession.CurrentState.CurrentStage != MultiplayerSessionConnectionStage.DISCONNECTED)
            {
                multiplayerSession.Disconnect();
            }
            multiplayerSession.ConnectionStateChanged -= SessionConnectionStateChangedHandler;

            Multiplayer.Main.StopCurrentSession();
            Modal.ClearCurrent();
            NitroxServiceLocator.EndCurrentLifetimeScope(); //Always do this last.

            UnityEngine.Object.Destroy(multiplayerClient);
            multiplayerClient = null;
        }
        finally
        {
            stopping = false;
            gameStarted = false;
        }
    }

    /// <summary>
    ///     Ends the attempt with a message. Deferred a frame: this is called from the network and session callbacks, which
    ///     must not stop the client they are running in.
    /// </summary>
    private static void Fail(string message)
    {
        int failedAttempt = attempt;
        CoroutineHost.StartCoroutine(FailNextFrame());

        IEnumerator FailNextFrame()
        {
            yield return null;
            if (attempt != failedAttempt || !IsJoiningFromMenu)
            {
                yield break;
            }
            StopMultiplayerClient();
            MainMenuNotificationPanel.ShowMessage(message, MainMenuServerListPanel.NAME);
        }
    }

    private static void WatchForReply(MultiplayerSessionConnectionStage stage)
    {
        if (JoinReplyTimeout.For(stage) is not { } limit)
        {
            return;
        }
        int watchedAttempt = attempt;
        CoroutineHost.StartCoroutine(Watch());

        IEnumerator Watch()
        {
            yield return new WaitForSecondsRealtime((float)limit.TotalSeconds);
            if (attempt == watchedAttempt && multiplayerSession != null && multiplayerSession.CurrentState.CurrentStage == stage)
            {
                Log.Warn($"The server did not answer within {limit.TotalSeconds} seconds while at {stage}");
                Fail(Language.main.Get("Nitrox_JoinTimedOut"));
            }
        }
    }
}
