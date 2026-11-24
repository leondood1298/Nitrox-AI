using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Nitrox.Server.Subnautica.Services;

namespace Nitrox.Test.Server.Services;

[TestClass]
public sealed class PreventMultiServerInitServiceTest
{
    /// <summary>
    ///     A server that fails to start (a world it refuses, a port in use) is never told that it started or stopped. It
    ///     kept its hold then: the process never ended and every other server on the machine waited for it.
    /// </summary>
    [TestMethod]
    public async Task HoldEndsWhenTheServerFailedToStart()
    {
        PreventMultiServerInitService service = new(NullLogger<PreventMultiServerInitService>.Instance);
        await service.StartingAsync(CancellationToken.None);
        CanTakeHold().Should().BeFalse("this server is starting");

        service.Dispose();

        CanTakeHold().Should().BeTrue();
    }

    [TestMethod]
    public async Task HoldEndsWhenTheServerHasStarted()
    {
        PreventMultiServerInitService service = new(NullLogger<PreventMultiServerInitService>.Instance);
        await service.StartingAsync(CancellationToken.None);

        await service.StartedAsync(CancellationToken.None);

        CanTakeHold().Should().BeTrue();
    }

    /// <summary>
    ///     Whether another server could start now. A mutex belongs to the thread that took it, so it is taken and given
    ///     back on a thread of its own.
    /// </summary>
    private static bool CanTakeHold()
    {
        bool taken = false;
        Thread thread = new(() =>
        {
            using Mutex mutex = new(false, typeof(PreventMultiServerInitService).Assembly.FullName);
            try
            {
                taken = mutex.WaitOne(TimeSpan.FromSeconds(2));
            }
            catch (AbandonedMutexException)
            {
                taken = true;
            }
            if (taken)
            {
                mutex.ReleaseMutex();
            }
        });
        thread.Start();
        thread.Join();
        return taken;
    }
}
