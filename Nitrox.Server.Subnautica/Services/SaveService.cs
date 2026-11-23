using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using Nitrox.Model.Serialization;
using Nitrox.Server.Subnautica.Models.AppEvents;
using Nitrox.Server.Subnautica.Models.AppEvents.Core;
using Nitrox.Server.Subnautica.Models.Serialization.World;
using Nitrox.Server.Subnautica.Services.Core;

namespace Nitrox.Server.Subnautica.Services;

internal sealed class SaveService(Func<WorldService> worldServiceProvider, ISaveState.Trigger saveStateTrigger, IOptions<SubnauticaServerOptions> options, IOptions<ServerStartOptions> startOptions, ILogger<SaveService> logger)
    : QueuingBackgroundService<SaveService.ServiceAction>, IHibernate
{
    private readonly ILogger<SaveService> logger = logger;
    private readonly IOptions<SubnauticaServerOptions> options = options;
    private readonly ISaveState.Trigger saveStateTrigger = saveStateTrigger;
    private readonly IOptions<ServerStartOptions> startOptions = startOptions;
    private readonly Func<WorldService> worldServiceProvider = worldServiceProvider;

    public async Task QueueSaveAsync(CancellationToken cancellationToken, [CallerFilePath] string callerFilePath = "", [CallerLineNumber] int callerLineNumber = -1) => await QueueActionAsync(new ServiceAction(ServiceActionType.SAVE, callerFilePath, callerLineNumber), cancellationToken);

    protected override async Task ExecuteQueuedActionAsync(ServiceAction action, CancellationToken stoppingToken)
    {
        logger.ZLogTrace($"Save requested by {action.CallerFilePath}:{action.CallerLineNumber}");
        switch (action.Type)
        {
            case ServiceActionType.SAVE:
                string savePath = startOptions.Value.GetServerSavePath();
                WorldService worldService = worldServiceProvider();
                if (!await worldService.SaveAsync(savePath, stoppingToken))
                {
                    return;
                }
                await saveStateTrigger.InvokeAsync(new ISaveState.Args(savePath));
                if (!worldService.LastSaveChangedWorld)
                {
                    // Nothing new to hand to an external tool or to back up
                    logger.ZLogTrace($"World unchanged since the last save; no post-save command or backup");
                    return;
                }
                ExecutePostSaveCommand();
                BackUp(savePath);
                break;
        }
    }

    private void ExecutePostSaveCommand()
    {
        string postSaveCommandPath = options.Value.PostSaveCommandPath;
        if (string.IsNullOrWhiteSpace(postSaveCommandPath))
        {
            return;
        }
        if (!File.Exists(postSaveCommandPath))
        {
            logger.ZLogError($"Post-save file does not exist: {postSaveCommandPath}");
            return;
        }
        try
        {
            Process? process = Process.Start(CreatePostSaveStartInfo(postSaveCommandPath));
            if (process == null)
            {
                logger.ZLogError($"Post-save command did not start: {postSaveCommandPath}");
                return;
            }
            logger.ZLogInformation($"Post-save command started: {postSaveCommandPath}");
            // Reported when it ends, without holding up the next save
            _ = ReportWhenDoneAsync(process, postSaveCommandPath);
        }
        catch (Exception ex)
        {
            logger.ZLogError(ex, $"Post-save command failed");
        }
    }

    private async Task ReportWhenDoneAsync(Process process, string commandPath)
    {
        using (process)
        {
            try
            {
                await process.WaitForExitAsync();
                if (process.ExitCode == 0)
                {
                    logger.ZLogInformation($"Post-save command completed successfully: {commandPath}");
                }
                else
                {
                    logger.ZLogError($"Post-save command ended with exit code {process.ExitCode}: {commandPath}");
                }
            }
            catch (Exception ex)
            {
                logger.ZLogError(ex, $"Post-save command could not be followed");
            }
        }
    }

    /// <summary>
    ///     How to run the configured command. A PowerShell script, which the option's description promises, could not be
    ///     started as a program, and opening it through the shell only showed it in an editor.
    /// </summary>
    internal static ProcessStartInfo CreatePostSaveStartInfo(string commandPath)
    {
        ProcessStartInfo startInfo = Path.GetExtension(commandPath).ToLowerInvariant() switch
        {
            ".ps1" => new ProcessStartInfo(OperatingSystem.IsWindows() ? "powershell.exe" : "pwsh") { ArgumentList = { "-NoProfile", "-File", commandPath } },
            ".bat" or ".cmd" => new ProcessStartInfo("cmd.exe") { ArgumentList = { "/c", commandPath } },
            _ => new ProcessStartInfo(commandPath)
        };
        startInfo.UseShellExecute = false;
        startInfo.CreateNoWindow = true;
        startInfo.WorkingDirectory = Path.GetDirectoryName(Path.GetFullPath(commandPath)) ?? "";
        return startInfo;
    }

    /// <summary>
    ///     Zips the generation just saved, without the settings (they hold the passwords) or temporary files, and thins out
    ///     old backups. The save queue holds the next save back until this is done, so the files cannot change meanwhile.
    /// </summary>
    private void BackUp(string saveDir)
    {
        if (options.Value.MaxBackups < 1)
        {
            logger.ZLogInformation($"No backup was made (\"{nameof(options.Value.MaxBackups)}\" is equal to 0)");
            return;
        }

        try
        {
            DateTime now = DateTime.Now;
            WorldBackups.Create(saveDir, now);
            IReadOnlyList<string> removed = WorldBackups.Prune(saveDir, options.Value.MaxBackups, now);
            logger.ZLogInformation($"World backed up{(removed.Count > 0 ? $", {removed.Count} older backup(s) thinned out" : "")}");
        }
        catch (Exception ex)
        {
            logger.ZLogError(ex, $"Error while backing up world");
        }
    }

    async Task IEvent<IHibernate.SleepArgs>.OnEventAsync(IHibernate.SleepArgs args) => await QueueSaveAsync(CancellationToken.None);

    Task IEvent<IHibernate.WakeArgs>.OnEventAsync(IHibernate.WakeArgs args) => Task.CompletedTask;

    internal record ServiceAction(ServiceActionType Type, string CallerFilePath = "", int CallerLineNumber = -1);

    internal enum ServiceActionType
    {
        SAVE
    }
}
