using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Nitrox.Model;
using Nitrox.Model.Helper;
using Nitrox.Model.Logger;
using Nitrox.Model.Platforms.OS.Shared;

namespace Nitrox.Launcher.Models.Utils;

/// <summary>
///     After a multiplayer start, waits for the game to report that Nitrox loaded, and tells the player when it did not.
/// </summary>
internal static class GameStartCheck
{
    public enum Outcome
    {
        LOADED,
        LOADED_WITH_PROBLEMS,
        FAILED,
        NOT_LOADED,
        GAME_NOT_RUNNING
    }

    public static readonly TimeSpan TIMEOUT = TimeSpan.FromMinutes(3);
    private static readonly TimeSpan pollInterval = TimeSpan.FromSeconds(2);

    public static Outcome Judge(GameStartReport? report, bool gameRunning) =>
        report switch
        {
            { Loaded: true, Problems.Count: 0 } => Outcome.LOADED,
            { Loaded: true } => Outcome.LOADED_WITH_PROBLEMS,
            { Loaded: false } => Outcome.FAILED,
            null when gameRunning => Outcome.NOT_LOADED,
            _ => Outcome.GAME_NOT_RUNNING
        };

    public static string? Describe(Outcome outcome, GameStartReport? report) =>
        outcome switch
        {
            Outcome.LOADED_WITH_PROBLEMS => $"Nitrox loaded into Subnautica, but parts of it failed and multiplayer may not stay in sync: {string.Join(" ", report!.Problems.Take(5))}",
            Outcome.FAILED => $"Nitrox failed to start inside Subnautica, so the game runs without multiplayer. {string.Join(" ", report!.Problems.Take(3))} Restart the game from the launcher; if it happens again, reinstall Nitrox.",
            Outcome.NOT_LOADED => "Subnautica is running, but Nitrox did not load into it. Likely causes: Steam started a different Subnautica install than the one chosen in the launcher's options; the game could not reach the Nitrox folder (on Linux keep Nitrox inside your home folder); Nitrox's own files are incomplete (reinstall Nitrox); or the game failed early. The game's Player.log names the reason on its lines starting with [Nitrox].",
            _ => null
        };

    /// <param name="launchedUtc">When the launcher started the game: older reports belong to earlier runs.</param>
    public static async Task WatchAsync(DateTime launchedUtc, CancellationToken cancellationToken = default)
    {
        // Steam may take a while to hand the start to the game, so a little slack before the launch time is allowed.
        DateTime sinceUtc = launchedUtc - TimeSpan.FromSeconds(2);
        GameStartReport? report = null;
        DateTime deadline = DateTime.UtcNow + TIMEOUT;
        while (DateTime.UtcNow < deadline && !cancellationToken.IsCancellationRequested)
        {
            report = GameStartReport.TryReadSince(GameStartReport.DefaultFilePath, sinceUtc);
            if (report != null)
            {
                break;
            }
            await Task.Delay(pollInterval, cancellationToken);
        }

        bool gameRunning = ProcessEx.ProcessExists(GameInfo.Subnautica.ExeName);
        Outcome outcome = Judge(report, gameRunning);
        string? message = Describe(outcome, report);
        Log.Info($"Game start check: {outcome}{(message == null ? "" : $" - {message}")}");
        switch (outcome)
        {
            case Outcome.LOADED_WITH_PROBLEMS or Outcome.NOT_LOADED:
                LauncherNotifier.Warning(message!);
                break;
            case Outcome.FAILED:
                LauncherNotifier.Error(message!);
                break;
        }
    }
}
