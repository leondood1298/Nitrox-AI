namespace Nitrox.Server.Subnautica.Services;

internal sealed class AutoSaveService(SaveService saveService, HibernateService hibernateService, IOptions<SubnauticaServerOptions> options, ILogger<AutoSaveService> logger) : BackgroundService, IHostedLifecycleService
{
    /// <summary>
    ///     How often the setting is looked at while saving is off, so that "autosave on" takes effect without a restart.
    /// </summary>
    internal static readonly TimeSpan DISABLED_CHECK_PERIOD = TimeSpan.FromSeconds(10);

    private readonly HibernateService hibernateService = hibernateService;
    private readonly ILogger<AutoSaveService> logger = logger;
    private readonly IOptions<SubnauticaServerOptions> options = options;
    private readonly SaveService saveService = saveService;

    public Task StartingAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StartedAsync(CancellationToken cancellationToken)
    {
        logger.ZLogTrace($"{(options.Value.ShouldAutoSave() ? $"ENABLED ({TimeSpan.FromMilliseconds(options.Value.SaveInterval).TotalMinutes} min)" : "DISABLED")}");
        return Task.CompletedTask;
    }

    public Task StoppingAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StoppedAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>
    ///     The time between ticks: the save interval while saving is on, else a short period to notice it being turned on.
    /// </summary>
    internal static TimeSpan GetTickPeriod(SubnauticaServerOptions options) => options.ShouldAutoSave() ? TimeSpan.FromMilliseconds(options.SaveInterval) : DISABLED_CHECK_PERIOD;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // The timer always runs: the autosave command may turn saving on or change the interval while the server runs
        using PeriodicTimer autoSaveTimer = new(GetTickPeriod(options.Value));
        while (!stoppingToken.IsCancellationRequested)
        {
            await autoSaveTimer.WaitForNextTickAsync(stoppingToken);
            TimeSpan period = GetTickPeriod(options.Value);
            if (autoSaveTimer.Period != period)
            {
                // A changed interval counts from now; the first save after turning saving on comes one interval later
                autoSaveTimer.Period = period;
                continue;
            }
            if (!options.Value.ShouldAutoSave())
            {
                continue;
            }
            if (hibernateService.IsSleeping)
            {
                logger.ZLogTrace($"Auto saving skipped because server is in power saving mode");
                continue;
            }

            logger.ZLogTrace($"Requesting to save...");
            await saveService.QueueSaveAsync(stoppingToken);
        }
    }
}
