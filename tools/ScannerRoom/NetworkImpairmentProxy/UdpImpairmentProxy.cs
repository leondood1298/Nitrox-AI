using System.Net;
using System.Net.Sockets;

namespace ScannerRoom.NetworkImpairmentProxy;

internal sealed class UdpImpairmentProxy : IAsyncDisposable
{
    private sealed record Datagram(byte[] Payload, IPEndPoint Target);

    private sealed class DirectionState(IMonotonicClock clock, ImpairmentSettings settings, ulong seed)
    {
        public ImpairmentScheduler<Datagram> Scheduler { get; } = new(clock, settings, seed);
        public long ReceivedPackets { get; set; }
        public long ReceivedBytes { get; set; }
        public long SentPackets { get; set; }
        public long SentBytes { get; set; }
        public long SendErrors { get; set; }
    }

    private const int MAXIMUM_UDP_PAYLOAD = 65_535;
    private const ulong UPSTREAM_SEED_SALT = 0x55505F4E495031UL;
    private const ulong DOWNSTREAM_SEED_SALT = 0x444E5F4E495031UL;

    private readonly ProxyOptions options;
    private readonly TextWriter output;
    private readonly IMonotonicClock clock;
    private readonly Socket socket;
    private readonly DirectionState upstream;
    private readonly DirectionState downstream;
    private readonly long statisticsIntervalMilliseconds;
    private IPEndPoint? clientEndpoint;
    private long nextStatisticsAt;
    private long foreignClientPackets;
    private long noClientPackets;
    private long receiveErrors;
    private int runStarted;

    public UdpImpairmentProxy(ProxyOptions options, TextWriter? output = null, IMonotonicClock? clock = null, bool allowEphemeralListenPort = false)
    {
        options.Validate(allowEphemeralListenPort);
        this.options = options;
        this.output = output ?? Console.Out;
        this.clock = clock ?? new StopwatchClock();
        statisticsIntervalMilliseconds = checked(options.StatisticsIntervalSeconds * 1_000L);
        nextStatisticsAt = statisticsIntervalMilliseconds;

        ulong seed = unchecked((ulong)(long)options.Seed);
        upstream = new DirectionState(this.clock, options.Impairment, seed ^ UPSTREAM_SEED_SALT);
        downstream = new DirectionState(this.clock, options.Impairment, seed ^ DOWNSTREAM_SEED_SALT);

        socket = new Socket(options.ListenEndpoint.AddressFamily, SocketType.Dgram, ProtocolType.Udp);
        if (options.ListenEndpoint.AddressFamily == AddressFamily.InterNetworkV6)
        {
            socket.DualMode = false;
        }
        socket.Bind(options.ListenEndpoint);
    }

    public IPEndPoint BoundEndpoint => (IPEndPoint)socket.LocalEndPoint!;

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        if (Interlocked.Exchange(ref runStarted, 1) != 0)
        {
            throw new InvalidOperationException("This proxy instance has already been run.");
        }

        WriteStart();
        byte[] receiveBuffer = new byte[MAXIMUM_UDP_PAYLOAD];
        Task<SocketReceiveFromResult> receiveTask = BeginReceive(receiveBuffer, cancellationToken);

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await FlushDueAsync(upstream, cancellationToken);
                await FlushDueAsync(downstream, cancellationToken);
                WritePeriodicStatisticsIfDue();

                int delayMilliseconds = GetNextWakeDelay();
                Task delayTask = Task.Delay(delayMilliseconds, cancellationToken);
                Task completed = await Task.WhenAny(receiveTask, delayTask);

                if (completed != receiveTask)
                {
                    continue;
                }

                try
                {
                    SocketReceiveFromResult received = await receiveTask;
                    ProcessReceived(receiveBuffer, received);
                }
                catch (SocketException exception) when (exception.SocketErrorCode == SocketError.ConnectionReset)
                {
                    receiveErrors++;
                }

                receiveBuffer = new byte[MAXIMUM_UDP_PAYLOAD];
                receiveTask = BeginReceive(receiveBuffer, cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        finally
        {
            if (cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await receiveTask;
                }
                catch (OperationCanceledException)
                {
                }
                catch (SocketException exception) when (exception.SocketErrorCode is SocketError.OperationAborted or SocketError.Interrupted)
                {
                }
            }
            WriteStatistics("stop");
        }
    }

    public ValueTask DisposeAsync()
    {
        socket.Dispose();
        return ValueTask.CompletedTask;
    }

    private Task<SocketReceiveFromResult> BeginReceive(byte[] buffer, CancellationToken cancellationToken)
    {
        EndPoint remoteTemplate = options.ListenEndpoint.AddressFamily == AddressFamily.InterNetwork
            ? new IPEndPoint(IPAddress.Any, 0)
            : new IPEndPoint(IPAddress.IPv6Any, 0);
        return socket.ReceiveFromAsync(buffer, SocketFlags.None, remoteTemplate, cancellationToken).AsTask();
    }

    private void ProcessReceived(byte[] receiveBuffer, SocketReceiveFromResult received)
    {
        if (received.RemoteEndPoint is not IPEndPoint source)
        {
            return;
        }

        byte[] payload = receiveBuffer.AsSpan(0, received.ReceivedBytes).ToArray();
        if (source.Equals(options.ServerEndpoint))
        {
            downstream.ReceivedPackets++;
            downstream.ReceivedBytes += payload.Length;
            if (clientEndpoint is null)
            {
                noClientPackets++;
                return;
            }
            downstream.Scheduler.Enqueue(new Datagram(payload, clientEndpoint));
            return;
        }

        if (clientEndpoint is null)
        {
            clientEndpoint = new IPEndPoint(source.Address, source.Port);
            output.WriteLine($"[NIP1] ev=client endpoint={clientEndpoint}");
        }
        else if (!source.Equals(clientEndpoint))
        {
            foreignClientPackets++;
            return;
        }

        upstream.ReceivedPackets++;
        upstream.ReceivedBytes += payload.Length;
        upstream.Scheduler.Enqueue(new Datagram(payload, options.ServerEndpoint));
    }

    private async Task FlushDueAsync(DirectionState direction, CancellationToken cancellationToken)
    {
        foreach (ScheduledItem<Datagram> scheduled in direction.Scheduler.DrainDue())
        {
            try
            {
                int sent = await socket.SendToAsync(scheduled.Value.Payload, SocketFlags.None, scheduled.Value.Target, cancellationToken);
                direction.SentPackets++;
                direction.SentBytes += sent;
            }
            catch (SocketException)
            {
                direction.SendErrors++;
            }
        }
    }

    private int GetNextWakeDelay()
    {
        long untilStats = Math.Max(0, nextStatisticsAt - clock.Milliseconds);
        long next = untilStats;
        long? upstreamDue = upstream.Scheduler.MillisecondsUntilNext();
        long? downstreamDue = downstream.Scheduler.MillisecondsUntilNext();
        if (upstreamDue is not null)
        {
            next = Math.Min(next, upstreamDue.Value);
        }
        if (downstreamDue is not null)
        {
            next = Math.Min(next, downstreamDue.Value);
        }
        return (int)Math.Min(int.MaxValue, next);
    }

    private void WritePeriodicStatisticsIfDue()
    {
        if (clock.Milliseconds < nextStatisticsAt)
        {
            return;
        }
        WriteStatistics("stats");
        nextStatisticsAt = checked(clock.Milliseconds + statisticsIntervalMilliseconds);
    }

    private void WriteStart()
    {
        ImpairmentSettings impairment = options.Impairment;
        output.WriteLine(
            $"[NIP1] ev=start listen={BoundEndpoint} server={options.ServerEndpoint} delay={impairment.DelayMilliseconds} jitter={impairment.JitterMilliseconds} " +
            $"loss={options.LossPercent} reorderEvery={impairment.ReorderEvery} hold={impairment.ReorderHoldMilliseconds} seed={options.Seed} maxq={impairment.MaximumQueuedPackets}");
    }

    private void WriteStatistics(string eventName)
    {
        output.WriteLine(
            $"[NIP1] ev={eventName} t={clock.Milliseconds} " +
            FormatDirection("up", upstream) + " " + FormatDirection("dn", downstream) +
            $" foreign={foreignClientPackets} noclient={noClientPackets} rxerr={receiveErrors} client={clientEndpoint?.ToString() ?? "-"}");
    }

    private static string FormatDirection(string prefix, DirectionState direction)
    {
        ImpairmentScheduler<Datagram> scheduler = direction.Scheduler;
        return $"{prefix}.rx={direction.ReceivedPackets}/{direction.ReceivedBytes} {prefix}.tx={direction.SentPackets}/{direction.SentBytes} " +
               $"{prefix}.loss={scheduler.ImpairmentDroppedPackets} {prefix}.overflow={scheduler.OverflowDroppedPackets} " +
               $"{prefix}.reorder={scheduler.ReorderedPairs} {prefix}.expire={scheduler.ExpiredReorderHolds} " +
               $"{prefix}.q={scheduler.QueuedPackets} {prefix}.err={direction.SendErrors}";
    }
}
