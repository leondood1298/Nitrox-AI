using System.Net;
using System.Net.Sockets;
using System.Text;

namespace ScannerRoom.NetworkImpairmentProxy;

internal static class SelfTests
{
    public static async Task RunAsync()
    {
        (string Name, Func<Task> Test)[] tests =
        {
            ("command-line validation", () => RunSync(TestCommandLineValidation)),
            ("seeded scheduling determinism", () => RunSync(TestSeededSchedulingDeterminism)),
            ("jitter bounds", () => RunSync(TestJitterBounds)),
            ("bounded reorder and expiry", () => RunSync(TestReorderAndExpiry)),
            ("bounded queue", () => RunSync(TestQueueBound)),
            ("localhost bidirectional echo", TestLocalhostEchoAsync)
        };

        foreach ((string name, Func<Task> test) in tests)
        {
            await test();
            Console.WriteLine($"[SELFTEST] PASS {name}");
        }
        Console.WriteLine($"[SELFTEST] PASS all={tests.Length}");
    }

    private static void TestCommandLineValidation()
    {
        ProxyOptions valid = CommandLine.Parse(
        [
            "--listen", "127.0.0.1:12001", "--server", "127.0.0.1:12000",
            "--delay-ms", "120", "--jitter-ms", "30", "--loss-percent", "2.50",
            "--reorder-every", "20", "--reorder-hold-ms", "250", "--seed", "53"
        ]);
        Equal(250, valid.Impairment.LossBasisPoints, "loss conversion");
        Equal(20, valid.Impairment.ReorderEvery, "reorder frequency");

        Throws<ArgumentException>(() => CommandLine.Parse(["--listen", "127.0.0.1:1"]), "missing server");
        Throws<ArgumentException>(() => CommandLine.Parse(["--listen", "127.0.0.1:1", "--server", "127.0.0.1:1"]), "loop endpoint");
        Throws<ArgumentOutOfRangeException>(
            () => CommandLine.Parse(["--listen", "127.0.0.1:1", "--server", "127.0.0.1:2", "--delay-ms", "10", "--jitter-ms", "11"]),
            "jitter over delay");
        Throws<ArgumentOutOfRangeException>(
            () => CommandLine.Parse(["--listen", "127.0.0.1:1", "--server", "127.0.0.1:2", "--loss-percent", "50.01"]),
            "loss cap");
    }

    private static void TestSeededSchedulingDeterminism()
    {
        ManualClock firstClock = new();
        ManualClock secondClock = new();
        ImpairmentSettings settings = new(100, 30, 1_000, 4, 250, 128);
        ImpairmentScheduler<int> first = new(firstClock, settings, 0x12345678UL);
        ImpairmentScheduler<int> second = new(secondClock, settings, 0x12345678UL);
        List<ScheduledItem<int>> firstOutput = new();
        List<ScheduledItem<int>> secondOutput = new();

        for (int packet = 0; packet < 100; packet++)
        {
            Equal(first.Enqueue(packet), second.Enqueue(packet), $"outcome {packet}");
            if (packet % 3 == 2)
            {
                firstClock.Advance(17);
                secondClock.Advance(17);
                firstOutput.AddRange(first.DrainDue());
                secondOutput.AddRange(second.DrainDue());
            }
        }
        firstClock.Advance(1_000);
        secondClock.Advance(1_000);
        firstOutput.AddRange(first.DrainDue());
        secondOutput.AddRange(second.DrainDue());

        SequenceEqual(firstOutput, secondOutput, "scheduled output");
        Equal(first.ImpairmentDroppedPackets, second.ImpairmentDroppedPackets, "loss count");
        Equal(first.ReorderedPairs, second.ReorderedPairs, "reorder count");
        True(first.ImpairmentDroppedPackets is > 0 and < 100, "seeded loss should drop some but not all packets");
    }

    private static void TestJitterBounds()
    {
        ManualClock clock = new();
        ImpairmentScheduler<int> scheduler = new(clock, new ImpairmentSettings(100, 40, 0, 0, 50, 256), 99);
        for (int packet = 0; packet < 200; packet++)
        {
            scheduler.Enqueue(packet);
        }
        clock.Advance(200);
        IReadOnlyList<ScheduledItem<int>> output = scheduler.DrainDue();
        Equal(200, output.Count, "jitter output count");
        True(output.All(item => item.DueMilliseconds is >= 60 and <= 140), "jitter must remain inside configured bounds");
    }

    private static void TestReorderAndExpiry()
    {
        ManualClock clock = new();
        ImpairmentSettings settings = new(10, 0, 0, 2, 50, 32);
        ImpairmentScheduler<int> scheduler = new(clock, settings, 1);
        Equal(ScheduleOutcome.Queued, scheduler.Enqueue(1), "first packet");
        Equal(ScheduleOutcome.HeldForReorder, scheduler.Enqueue(2), "held packet");
        Equal(ScheduleOutcome.Queued, scheduler.Enqueue(3), "pair release");
        clock.Advance(10);
        SequenceEqual([1, 3, 2], scheduler.DrainDue().Select(item => item.Value).ToArray(), "pair swap");
        Equal(1L, scheduler.ReorderedPairs, "reorder count");

        ManualClock expiryClock = new();
        ImpairmentScheduler<int> expiry = new(expiryClock, settings, 1);
        expiry.Enqueue(1);
        expiry.Enqueue(2);
        expiryClock.Advance(49);
        SequenceEqual([1], expiry.DrainDue().Select(item => item.Value).ToArray(), "pre-expiry output");
        expiryClock.Advance(1);
        SequenceEqual([2], expiry.DrainDue().Select(item => item.Value).ToArray(), "expired hold output");
        Equal(1L, expiry.ExpiredReorderHolds, "expired hold count");
    }

    private static void TestQueueBound()
    {
        ManualClock clock = new();
        ImpairmentScheduler<int> scheduler = new(clock, new ImpairmentSettings(100, 0, 0, 0, 50, 2), 5);
        Equal(ScheduleOutcome.Queued, scheduler.Enqueue(1), "queue packet one");
        Equal(ScheduleOutcome.Queued, scheduler.Enqueue(2), "queue packet two");
        Equal(ScheduleOutcome.QueueOverflowDrop, scheduler.Enqueue(3), "overflow packet");
        Equal(2, scheduler.QueuedPackets, "bounded queue size");
        Equal(1L, scheduler.OverflowDroppedPackets, "overflow counter");
    }

    private static async Task TestLocalhostEchoAsync()
    {
        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(5));
        using CancellationTokenSource proxyStop = CancellationTokenSource.CreateLinkedTokenSource(timeout.Token);
        using Socket echo = new(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        echo.Bind(new IPEndPoint(IPAddress.Loopback, 0));
        IPEndPoint echoEndpoint = (IPEndPoint)echo.LocalEndPoint!;

        ProxyOptions options = new(
            new IPEndPoint(IPAddress.Loopback, 0),
            echoEndpoint,
            new ImpairmentSettings(1, 0, 0, 0, 50, 32),
            1425,
            60);
        StringWriter proxyLog = new();
        await using UdpImpairmentProxy proxy = new(options, proxyLog, allowEphemeralListenPort: true);
        Task proxyTask = proxy.RunAsync(proxyStop.Token);
        Task echoTask = EchoOneAsync(echo, timeout.Token);

        using Socket client = new(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        client.Bind(new IPEndPoint(IPAddress.Loopback, 0));
        byte[] expected = Encoding.ASCII.GetBytes("scanner-n1-echo");
        await client.SendToAsync(expected, SocketFlags.None, proxy.BoundEndpoint, timeout.Token);

        byte[] responseBuffer = new byte[256];
        SocketReceiveFromResult response = await client.ReceiveFromAsync(
            responseBuffer,
            SocketFlags.None,
            new IPEndPoint(IPAddress.Any, 0),
            timeout.Token);
        SequenceEqual(expected, responseBuffer.AsSpan(0, response.ReceivedBytes).ToArray(), "echo payload");

        await echoTask;
        proxyStop.Cancel();
        await proxyTask;
        True(proxyLog.ToString().Contains("up.tx=1", StringComparison.Ordinal), "upstream transmit statistic");
        True(proxyLog.ToString().Contains("dn.tx=1", StringComparison.Ordinal), "downstream transmit statistic");
    }

    private static async Task EchoOneAsync(Socket echo, CancellationToken cancellationToken)
    {
        byte[] buffer = new byte[MAXIMUM_ECHO_PAYLOAD];
        SocketReceiveFromResult received = await echo.ReceiveFromAsync(
            buffer,
            SocketFlags.None,
            new IPEndPoint(IPAddress.Any, 0),
            cancellationToken);
        await echo.SendToAsync(
            buffer.AsMemory(0, received.ReceivedBytes),
            SocketFlags.None,
            received.RemoteEndPoint,
            cancellationToken);
    }

    private const int MAXIMUM_ECHO_PAYLOAD = 65_535;

    private static Task RunSync(Action action)
    {
        action();
        return Task.CompletedTask;
    }

    private static void True(bool condition, string description)
    {
        if (!condition)
        {
            throw new InvalidOperationException($"Assertion failed: {description}.");
        }
    }

    private static void Equal<T>(T expected, T actual, string description) where T : notnull
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new InvalidOperationException($"Assertion failed ({description}): expected '{expected}', actual '{actual}'.");
        }
    }

    private static void SequenceEqual<T>(IEnumerable<T> expected, IEnumerable<T> actual, string description)
    {
        if (!expected.SequenceEqual(actual))
        {
            throw new InvalidOperationException($"Assertion failed ({description}): sequences differ.");
        }
    }

    private static void Throws<TException>(Action action, string description) where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException)
        {
            return;
        }
        throw new InvalidOperationException($"Assertion failed ({description}): expected {typeof(TException).Name}.");
    }
}
