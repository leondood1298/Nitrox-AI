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
                if (!await worldServiceProvider().SaveAsync(savePath, stoppingToken))
                {
                    return;
                }
                await saveStateTrigger.InvokeAsync(new ISaveState.Args(savePath));
                ExecutePostSaveCommand();
                BackUp(savePath);
                break;
        }
    }

    private void ExecutePostSaveCommand()
    {
        string postSaveCommandPath = options.Value.PostSaveCommandPath;
        if (!string.IsNullOrWhiteSpace(postSaveCommandPath))
        {
            try
            {
                // Call external tool for backups, etc
                if (File.Exists(postSaveCommandPath))
                {
                    using Process process = Process.Start(new ProcessStartInfo
                    {
                        FileName = postSaveCommandPath,
                        Verb = "open"
                    });
                    logger.ZLogInformation($"Post-save command completed successfully: {postSaveCommandPath}");
                }
                else
                {
                    logger.ZLogError($"Post-save file does not exist: {postSaveCommandPath}");
                }
            }
            catch (Exception ex)
            {
                logger.ZLogError(ex, $"Post-save command failed");
            }
        }
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
