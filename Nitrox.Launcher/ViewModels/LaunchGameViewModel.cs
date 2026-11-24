using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nitrox.Launcher.Models.Design;
using Nitrox.Launcher.Models.Services;
using Nitrox.Launcher.Models.Utils;
using Nitrox.Launcher.ViewModels.Abstract;
using Nitrox.Model.Constants;
using Nitrox.Model.Core;
using Nitrox.Model.Helper;
using Nitrox.Model.Logger;
using Nitrox.Model.Platforms.Discovery.InstallationFinders;
using Nitrox.Model.Platforms.Discovery.InstallationFinders.Core;
using Nitrox.Model.Platforms.Discovery.Models;
using Nitrox.Model.Platforms.OS.Shared;
using Nitrox.Model.Platforms.Store;

namespace Nitrox.Launcher.ViewModels;

internal partial class LaunchGameViewModel(DialogService dialogService, ServerService serverService, OptionsViewModel optionsViewModel, IKeyValueStore keyValueStore)
    : RoutableViewModelBase
{
    private static bool hasInstantLaunched;
    private readonly DialogService dialogService = dialogService;
    private readonly IKeyValueStore keyValueStore = keyValueStore;

    private readonly ServerService serverService = serverService;

    [ObservableProperty]
    public partial Platform GamePlatform { get; set; }

    [ObservableProperty]
    public partial string? PlatformToolTip { get; set; }

    public Bitmap[] GalleryImageSources { get; } =
    [
        AssetHelper.GetAssetFromStream("/Assets/Images/gallery/image-1.png", static stream => new Bitmap(stream)),
        AssetHelper.GetAssetFromStream("/Assets/Images/gallery/image-2.png", static stream => new Bitmap(stream)),
        AssetHelper.GetAssetFromStream("/Assets/Images/gallery/image-3.png", static stream => new Bitmap(stream)),
        AssetHelper.GetAssetFromStream("/Assets/Images/gallery/image-4.png", static stream => new Bitmap(stream))
    ];

    public string Version => $"{NitroxEnvironment.ReleasePhase} {NitroxEnvironment.Version}";

    internal override async Task ViewContentLoadAsync(CancellationToken cancellationToken = default)
    {
        await Task.Run(() =>
        {
            GamePlatform = NitroxUser.GamePlatform?.Platform ?? Platform.NONE;
            PlatformToolTip = GamePlatform.GetAttribute<DescriptionAttribute>()?.Description ?? "";
            HandleInstantLaunchForDevelopment();
        }, cancellationToken);
    }

    internal override Task ViewContentUnloadAsync()
    {
        return Task.CompletedTask;
    }

    [RelayCommand]
    private async Task StartSingleplayerAsync()
    {
        if (GameInspect.WarnIfGameProcessExists(GameInfo.Subnautica) && !keyValueStore.GetIsMultipleGameInstancesAllowed())
        {
            return;
        }

        Log.Info("Launching Subnautica in singleplayer mode");
        try
        {
            if (string.IsNullOrWhiteSpace(NitroxUser.GamePath) || !Directory.Exists(NitroxUser.GamePath))
            {
                ChangeView(optionsViewModel);
                LauncherNotifier.Warning("Location of Subnautica is unknown. Set the path to it in settings");
                return;
            }

            // The game's code file stays patched: started without the launcher path, Nitrox does not load into it.
            await StartGameAsync(GameInfo.Subnautica, false);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error while starting game in singleplayer mode:");
            await dialogService.ShowErrorAsync(ex, "Error while starting game in singleplayer mode");
        }
    }

    /// <summary>
    ///     Prepares Subnautica to load Nitrox, then starts Subnautica.
    /// </summary>
    [RelayCommand]
    private async Task StartMultiplayerAsync(string[]? args = null)
    {
        Log.Info("Launching Subnautica in multiplayer mode");
        try
        {
            bool setupResult = await Task.Run(async () =>
            {
                if (string.IsNullOrWhiteSpace(NitroxUser.GamePath) || !Directory.Exists(NitroxUser.GamePath))
                {
                    ChangeView(optionsViewModel);
                    LauncherNotifier.Warning("Location of Subnautica is unknown. Set the path to it in settings");
                    return false;
                }
                if (PirateDetection.HasTriggered)
                {
                    LauncherNotifier.Error("Aarrr! Nitrox has walked the plank :(");
                    return false;
                }
                if (GameInspect.WarnIfGameProcessExists(GameInfo.Subnautica) && !keyValueStore.GetIsMultipleGameInstancesAllowed())
                {
                    return false;
                }
                if (await GameInspect.IsOutdatedGameAndNotify(NitroxUser.GamePath, dialogService))
                {
                    return false;
                }

                // TODO: The launcher should override FileRead win32 API for the Subnautica process to give it the modified Assembly-CSharp from memory
                try
                {
                    string patcherDllPath = Path.Combine(NitroxUser.ExecutableRootPath, "lib", "net472", NitroxEntryPatch.NITROX_ASSEMBLY_NAME);
                    NitroxEntryPatch.InstallPatcher(patcherDllPath, NitroxUser.GamePath);
                    CopyDebugSymbols(patcherDllPath, Path.Combine(NitroxEntryPatch.GetManagedPath(NitroxUser.GamePath), NitroxEntryPatch.NITROX_ASSEMBLY_NAME));

                    // Try inject Nitrox into Subnautica code.
                    await NitroxEntryPatch.Apply(NitroxUser.GamePath);
                }
                catch (EntryPatchException ex)
                {
                    Log.Error(ex, "Preparing the game's files failed");
                    await dialogService.ShowAsync<DialogBoxViewModel>(model =>
                    {
                        model.Title = "Could not prepare Subnautica for multiplayer";
                        model.Description = ex.Message;
                        model.ButtonOptions = ButtonOptions.Ok;
                    });
                    return false;
                }

                if (QModHelper.IsQModInstalled(NitroxUser.GamePath))
                {
                    Log.Warn("Seems like QModManager is installed");
                    LauncherNotifier.Warning("QModManager Detected in the game folder");
                }
                GameInspect.WarnIfBepInExMods(NitroxUser.GamePath);

                return true;
            });

            if (!setupResult)
            {
                return;
            }

            DateTime launchedUtc = DateTime.UtcNow;
            await StartGameAsync(GameInfo.Subnautica, true, args);
            _ = GameStartCheck.WatchAsync(launchedUtc).ContinueWithHandleError();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error while starting game in multiplayer mode:");
            await dialogService.ShowErrorAsync(ex, "Error while starting game in multiplayer mode");
        }
    }

    [RelayCommand]
    private void OpenContributionsOfYear()
    {
        string fromValue = HttpUtility.UrlEncode($"{DateTime.UtcNow.AddYears(-1):M/d/yyyy}");
        string toValue = HttpUtility.UrlEncode($"{DateTime.UtcNow:M/d/yyyy}");
        OpenUri($"github.com/SubnauticaNitrox/Nitrox/graphs/contributors?from={fromValue}&to={toValue}");
    }

    /// <summary>
    ///     Keeps the .pdb symbols (read by the game's Mono debugger) next to the copied dll so breakpoints bind.
    ///     A missing source pdb removes a stale one, as a pdb from another build is silently ignored by Mono.
    /// </summary>
    [Conditional("DEBUG")]
    private static void CopyDebugSymbols(string sourceDllPath, string targetDllPath)
    {
        try
        {
            string sourcePdb = Path.ChangeExtension(sourceDllPath, ".pdb");
            string targetPdb = Path.ChangeExtension(targetDllPath, ".pdb");
            if (File.Exists(sourcePdb))
            {
                File.Copy(sourcePdb, targetPdb, true);
            }
            else if (File.Exists(targetPdb))
            {
                File.Delete(targetPdb);
            }
        }
        catch (IOException ex)
        {
            Log.Error(ex, "Unable to copy debug symbols next to the patcher dll");
        }
    }

    /// <summary>
    ///     Launches the server and Subnautica immediately if instant launch is active.
    /// </summary>
    [Conditional("DEBUG")]
    private void HandleInstantLaunchForDevelopment()
    {
        if (hasInstantLaunched)
        {
            return;
        }
        hasInstantLaunched = true;
        if (App.InstantLaunch == null)
        {
            return;
        }
        Task.Run(async () =>
        {
            // Start the server
            ServerEntry? server = await serverService.GetOrCreateServerAsync(App.InstantLaunch.SaveName);
            if (server == null)
            {
                throw new Exception("Failed to create new server save files");
            }
            server.Name = App.InstantLaunch.SaveName;
            Task serverStartTask = Dispatcher.UIThread.InvokeAsync(async () => await serverService.StartServerAsync(server)).ContinueWithHandleError();

            // Start a game in multiplayer for each player
            foreach (string playerName in App.InstantLaunch.PlayerNames)
            {
                await StartMultiplayerAsync(["--instantlaunch", playerName]).ContinueWithHandleError();
            }

            await serverStartTask;
        }).ContinueWithHandleError();
    }

    /// <param name="loadNitrox">False for a single-player start: the game then gets no launcher path and stays vanilla.</param>
    /// <param name="extraArgs">Arguments for the game besides the player's own (development instant launch).</param>
    private async Task StartGameAsync(GameInfo gameInfo, bool loadNitrox, string[]? extraArgs = null)
    {
        LauncherNotifier.Info("Starting game");

        string gameExePathSuffix = RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "MacOS" : string.Empty;
        string gameExePath = Path.Combine(NitroxUser.GamePath, gameExePathSuffix, gameInfo.ExeName);
        if (!File.Exists(gameExePath))
        {
            throw new FileNotFoundException($"Unable to find {gameInfo.ExeName}");
        }

        // Only the data folder is passed on from the launcher's own command line, so the game logs where the launcher does.
        string launchArguments = GameLaunchArguments.Build(keyValueStore.GetLaunchArguments(gameInfo), NitroxDirectory.UserDataPath, extraArgs);

        if (keyValueStore.GetIsDiscordEnabled() && !launchArguments.Contains(DiscordConstants.ENABLE_ARG, StringComparison.OrdinalIgnoreCase))
        {
            launchArguments = $"{launchArguments} {DiscordConstants.ENABLE_ARG}";
        }

        string? launcherPath = loadNitrox ? NitroxUser.LauncherPath : null;
        ProcessEx game = NitroxUser.GamePlatform switch
        {
            // TODO: Pass ILogger instead of "onWarning" Action<string>.
            Steam => await Steam.StartGameAsync(gameExePath, launchArguments, gameInfo.SteamAppId, ShouldSkipSteam(gameInfo, launchArguments), keyValueStore.GetUseBigPictureMode(), launcherPath, message =>
            {
                Log.Warn(message);
                LauncherNotifier.Warning(message);
            }),
            EpicGames => await EpicGames.StartGameAsync(gameExePath, launchArguments, launcherPath),
            HeroicGames => await HeroicGames.StartGameAsync(gameInfo.EgsNamespace, launchArguments, launcherPath),
            MSStore => await MSStore.StartGameAsync(gameExePath, launchArguments, launcherPath),
            Discord => await Discord.StartGameAsync(gameExePath, launchArguments, launcherPath),
            _ => await Standalone.StartGameAsync(gameExePath, launchArguments, launcherPath),
        };

        if (game is null)
        {
            throw new Exception($"Game failed to start through {NitroxUser.GamePlatform?.Name ?? "Standalone"}");
        }
    }

    private bool ShouldSkipSteam(GameInfo gameInfo, string launchArguments)
    {
        bool isSteamsOwnInstall = true;
        try
        {
            GameFinderResult steamInstall = new SteamFinder().FindGame(gameInfo);
            isSteamsOwnInstall = Steam.IsSteamsOwnInstall(NitroxUser.GamePath, steamInstall.IsOk ? steamInstall.Path : null);
            if (!isSteamsOwnInstall)
            {
                LauncherNotifier.Warning($"The chosen install is not the one Steam knows ({(steamInstall.IsOk ? steamInstall.Path : "none")}), so it is started directly, without Steam's overlay.");
            }
        }
        catch (Exception ex)
        {
            // Steam's records being unreadable is no reason to stop; Steam then starts its own install as before.
            Log.Warn($"Could not compare the chosen install with Steam's: {ex.Message}");
        }
        return GameLaunchRoute.ShouldSkipSteam(launchArguments,
                                               keyValueStore.GetUseBigPictureMode(),
                                               keyValueStore.GetIsMultipleGameInstancesAllowed(),
                                               ProcessEx.ProcessExists(gameInfo.ExeName),
                                               App.InstantLaunch?.PlayerNames.Length ?? 0,
                                               isSteamsOwnInstall);
    }
}
