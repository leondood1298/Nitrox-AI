using System;
using System.Linq;
using DiscordGameSDKWrapper;
using Nitrox.Model;
using Nitrox.Model.Constants;
using Nitrox.Model.Core;
using Nitrox.Model.Subnautica.Packets;
using NitroxClient.Communication.Abstract;
using NitroxClient.MonoBehaviours.Gui.MainMenu.ServersList;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NitroxClient.MonoBehaviours.Discord;

public class DiscordClient : MonoBehaviour
{
    private const long CLIENT_ID = 405122994348752896;
    private const int RETRY_INTERVAL = 60;

    private static DiscordClient? main;
    private static DiscordGameSDKWrapper.Discord? discord;
    private static ActivityManager? activityManager;
    private static Activity activity;
    private static bool showingWindow;

    private static bool? isDiscordRequested;
    private static bool IsDiscordRequested => isDiscordRequested ??= Array.Exists(Environment.GetCommandLineArgs(), arg => arg.Equals(DiscordConstants.ENABLE_ARG, StringComparison.OrdinalIgnoreCase));

    private void Awake()
    {
        if (!IsDiscordRequested)
        {
            Log.Info("[Discord] Skipping initialization, not enabled by user");
            Destroy(this);
            return;
        }
        if (main)
        {
            Log.Error($"[Discord] Tried to instantiate a second {nameof(DiscordClient)}");
            return;
        }

        if (NitroxEnvironment.IsWine)
        {
            Log.Warn("[Discord] Unable to start RPC inside wine environment");
            return;
        }

        main = this;
        DontDestroyOnLoad(gameObject);
        Log.Info("[Discord] Starting Discord client");
        StartDiscordHook();
    }

    private void Update()
    {
        try
        {
            discord?.RunCallbacks();
        }
        catch (Exception ex)
        {
            // Happens when Discord is closed while Nitrox has its Discord hook running (and for other reason)
            DisposeAndScheduleHookRestart();
            Log.ErrorOnce($"An error occurred while running callbacks for Discord, will retry every {RETRY_INTERVAL} seconds: {ex.Message}");
        }
    }

    private void OnDisable()
    {
        Log.Info("[Discord] Shutdown client");
        discord?.Dispose();
    }

    private void OnDestroy()
    {
        if (main == this)
        {
            main = null;
            activity = default;
        }
    }

    private void StartDiscordHook()
    {
        try
        {
            discord = new DiscordGameSDKWrapper.Discord(CLIENT_ID, (ulong)CreateFlags.NoRequireDiscord);
            discord.SetLogHook(DiscordGameSDKWrapper.LogLevel.Debug, (level, message) => Log.Write((Nitrox.Model.Logger.LogLevel)level, $"[Discord] {message}"));

            activityManager = discord.GetActivityManager();

            if (activityManager == null)
            {
                Log.Error("[Discord] Failed to get activity manager from Discord");
                return;
            }

            activityManager.RegisterSteam((uint)GameInfo.Subnautica.SteamAppId);
            activityManager.OnActivityJoinRequest += ActivityJoinRequest;
            activityManager.OnActivityJoin += ActivityJoin;

            if (!string.IsNullOrEmpty(activity.State))
            {
                UpdateActivity();
            }
        }
        catch (Exception ex)
        {
            DisposeAndScheduleHookRestart();
            Log.ErrorOnce($"Encountered an error while starting Discord hook, will retry every {RETRY_INTERVAL} seconds: {ex.Message}");
        }
    }

    private void DisposeAndScheduleHookRestart()
    {
        discord?.Dispose();
        discord = null;
        Invoke(nameof(StartDiscordHook), RETRY_INTERVAL);
    }

    private void ActivityJoin(string secret)
    {
        Log.Info("[Discord] Joining Server");

        if (SceneManager.GetActiveScene().name != "StartScreen" || !MainMenuServerListPanel.Main)
        {
            Log.InGame(Language.main.Get("Nitrox_DiscordMultiplayerMenu"));
            Log.Warn("[Discord] Can't join a server outside of the main-menu.");
            return;
        }

        if (!DiscordJoinSecret.TryParse(secret, out string ip, out int port))
        {
            Log.Error("[Discord] The join secret does not hold an address and port");
            return;
        }
        if (MainMenuServerListPanel.Main.IsJoining)
        {
            return;
        }
        MainMenuServerButton.OpenJoinServerMenuAsync(ip, port).ContinueWithHandleError(true);
    }

    private void ActivityJoinRequest(ref User user)
    {
        if (!showingWindow && Multiplayer.Active)
        {
            Log.Info($"[Discord] JoinRequest: Name:{user.Username}#{user.Discriminator} UserID:{user.Id}");
            StartCoroutine(DiscordJoinRequestGui.SpawnGui(user));
            showingWindow = true;
        }
        else
        {
            // Answered at once so the asking friend is not left waiting on a request nobody sees
            Log.Warn("[Discord] Request window is already active or this game is not in a world: ignoring the request");
            activityManager?.SendRequestReply(user.Id, ActivityJoinRequestReply.Ignore, _ => { });
        }
    }

    public static void InitializeRPMenu()
    {
        if (!IsDiscordRequested)
        {
            return;
        }
        activity.State = Language.main.Get("Nitrox_DiscordMainMenuState");
        activity.Details = string.Empty;
        activity.Party = default;
        activity.Secrets = default;
        activity.Assets.LargeImage = "icon";
        UpdateActivity();
    }

    public static void InitializeRPInGame(string username, int playerCount, int maxConnections)
    {
        if (!IsDiscordRequested)
        {
            return;
        }
        activity.State = Language.main.Get("Nitrox_DiscordInGameState");
        activity.Details = Language.main.Get("Nitrox_DiscordInGame").Replace("{PLAYER}", username);
        activity.Timestamps.Start = 0;
        activity.Party.Size.CurrentSize = playerCount;
        activity.Party.Size.MaxSize = maxConnections;
        UpdateActivity();

        NitroxServiceLocator.LocateService<IPacketSender>().Send(new DiscordRequestIP(string.Empty));
    }

    public static void UpdateIpPort(string ipPort)
    {
        if (!IsDiscordRequested)
        {
            return;
        }
        // Friends see the party id; only someone this player accepts gets the join secret, so the address goes there alone
        activity.Party.Id = $"NitroxParty:{Guid.NewGuid():N}";
        activity.Secrets.Join = ipPort;
        UpdateActivity();
    }

    public static void UpdatePartySize(int size)
    {
        if (!IsDiscordRequested)
        {
            return;
        }
        activity.Party.Size.CurrentSize = size;
        UpdateActivity();
    }

    /// <summary>
    ///     The request window is gone, whether it was answered or not, so the next request may open one.
    /// </summary>
    public static void OnJoinRequestWindowClosed() => showingWindow = false;

    public static void RespondJoinRequest(long userID, ActivityJoinRequestReply reply)
    {
        if (!IsDiscordRequested)
        {
            return;
        }
        showingWindow = false;
        activityManager?.SendRequestReply(userID, reply, result =>
        {
            if (result == Result.Ok)
            {
                Log.Info($"[Discord] Responded successfully {reply} to {userID}");
            }
            else
            {
                Log.InGame($"[Discord] {Language.main.Get("Nitrox_Failure")}");
                Log.Error($"[Discord] {result}: Failed to send join response");
            }
        });
    }

    private static void UpdateActivity()
    {
        activityManager?.UpdateActivity(activity, result =>
        {
            if (result != Result.Ok)
            {
                Log.Error($"[Discord] {result}: Updating Activity failed");
            }
        });
    }
}
