using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Nitrox.Launcher.Models.Utils;
using Nitrox.Model.Constants;
using Nitrox.Model.Helper;
using Nitrox.Model.Logger;
using Nitrox.Model.Platforms.OS.Shared;

namespace Nitrox.Launcher.Models.Services;

/// <summary>
///     Brings an already open launcher of the same installation forward instead of opening a second one.
/// </summary>
/// <remarks>
///     Every build's launcher is called Nitrox.Launcher. Matching by that name alone made a newly built launcher quietly
///     close and bring up the older build that was still open, so the old build got tested. A launcher of another
///     installation is now let through with a warning; each installation has its own pipe to its servers.
/// </remarks>
internal sealed class PreventMultipleAppInstancesService : IHostedLifecycleService
{
    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StartingAsync(CancellationToken cancellationToken)
    {
        if (App.allowInstances || RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return Task.CompletedTask;
        }

        try
        {
            string ownFolder = NitroxUser.ExecutableRootPath;
            using ProcessEx? sameInstallation = ProcessEx.GetFirstProcess(NitroxConstants.LAUNCHER_APP_NAME,
                process => process.Id != Environment.ProcessId && process.IsRunning && IsSameInstallation(FolderOf(process), ownFolder));
            if (sameInstallation is not null)
            {
                sameInstallation.SetForegroundWindowAndRestore();
                Environment.Exit(0);
            }

            using ProcessEx? otherInstallation = ProcessEx.GetFirstProcess(NitroxConstants.LAUNCHER_APP_NAME, process => process.Id != Environment.ProcessId && process.IsRunning);
            if (otherInstallation is not null)
            {
                string otherFolder = FolderOf(otherInstallation) ?? "another folder";
                string message = $"Another Nitrox launcher is open, from {otherFolder}. Both change the same game folder when they start the game; close the one you are not testing.";
                Log.Warn(message);
                // The main window is not listening for notifications yet this early
                _ = Task.Delay(TimeSpan.FromSeconds(3), CancellationToken.None).ContinueWith(_ => LauncherNotifier.Warning(message), TaskScheduler.Default);
            }
        }
        catch (Exception)
        {
            // Ignore
        }
        return Task.CompletedTask;

        static string? FolderOf(ProcessEx process)
        {
            try
            {
                return Path.GetDirectoryName(process.MainModuleFileName);
            }
            catch (Exception)
            {
                return null;
            }
        }
    }

    /// <summary>
    ///     Whether two launcher folders are one installation. An unreadable folder counts as the same, which keeps the old
    ///     behaviour of one launcher at a time.
    /// </summary>
    internal static bool IsSameInstallation(string? otherFolder, string ownFolder) =>
        otherFolder == null || LauncherConstants.GetInstallationId(otherFolder) == LauncherConstants.GetInstallationId(ownFolder);

    public Task StartedAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StoppingAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StoppedAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
