using Nitrox.Server.Subnautica.Models;
using Nitrox.Server.Subnautica.Models.Serialization.World;

namespace Nitrox.Server.Subnautica.Services;

/// <summary>
///     Holds the world folder's lock for the life of the server, taken before the world is read.
/// </summary>
internal sealed class WorldLockService(IOptions<ServerStartOptions> startOptions, ILogger<WorldLockService> logger) : IHostedLifecycleService, IDisposable
{
    private readonly ILogger<WorldLockService> logger = logger;
    private readonly IOptions<ServerStartOptions> startOptions = startOptions;
    private WorldFolderLock? worldLock;

    public async Task StartingAsync(CancellationToken cancellationToken)
    {
        string saveDir = startOptions.Value.GetServerSavePath();
        int? holderProcessId = null;
        // A server that restarts itself starts its successor while it is still letting go of the world
        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(HOLDER_EXIT_WAIT);
        while ((worldLock = WorldFolderLock.TryAcquire(saveDir, out holderProcessId)) == null && !timeout.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(250, timeout.Token);
            }
            catch (OperationCanceledException)
            {
                // Give up below
            }
        }
        if (worldLock == null)
        {
            string holder = holderProcessId is { } processId ? $" (process {processId})" : "";
            string message = $"The world \"{startOptions.Value.SaveName}\" is already open in another Nitrox server{holder}. Two servers on one world overwrite each other's saves, so this one stops.";
            logger.ZLogCritical($"{message}");
            throw new StartupFailureException(message, StartupFailureException.WORLD_IN_USE);
        }
    }

    private static readonly TimeSpan HOLDER_EXIT_WAIT = TimeSpan.FromSeconds(15);

    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StartedAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StoppingAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>
    ///     Released last, after the save on stop has been written.
    /// </summary>
    public Task StoppedAsync(CancellationToken cancellationToken)
    {
        Dispose();
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        worldLock?.Dispose();
        worldLock = null;
    }
}
