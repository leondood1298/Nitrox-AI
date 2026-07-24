using System.Globalization;
using System.Net;
using System.Net.Sockets;

namespace ScannerRoom.NetworkImpairmentProxy;

internal sealed record ProxyOptions(
    IPEndPoint ListenEndpoint,
    IPEndPoint ServerEndpoint,
    ImpairmentSettings Impairment,
    int Seed,
    int StatisticsIntervalSeconds)
{
    public void Validate(bool allowEphemeralListenPort = false)
    {
        ArgumentNullException.ThrowIfNull(ListenEndpoint);
        ArgumentNullException.ThrowIfNull(ServerEndpoint);
        ArgumentNullException.ThrowIfNull(Impairment);

        if ((!allowEphemeralListenPort && ListenEndpoint.Port == 0) || ListenEndpoint.Port is < 0 or > 65_535)
        {
            throw new ArgumentOutOfRangeException(nameof(ListenEndpoint), "The listen port must be between 1 and 65535.");
        }
        if (ServerEndpoint.Port is < 1 or > 65_535)
        {
            throw new ArgumentOutOfRangeException(nameof(ServerEndpoint), "The server port must be between 1 and 65535.");
        }
        if (ListenEndpoint.AddressFamily != ServerEndpoint.AddressFamily)
        {
            throw new ArgumentException("The listen and server endpoints must use the same address family.");
        }
        if (ListenEndpoint.Equals(ServerEndpoint))
        {
            throw new ArgumentException("The listen and server endpoints must be different to avoid a forwarding loop.");
        }
        if (IsMulticast(ListenEndpoint.Address) ||
            ListenEndpoint.Address.Equals(IPAddress.None) ||
            ListenEndpoint.Address.Equals(IPAddress.IPv6None) ||
            ListenEndpoint.Address.Equals(IPAddress.Broadcast))
        {
            throw new ArgumentException("The listen endpoint must be a local unicast or wildcard address.", nameof(ListenEndpoint));
        }
        if (ServerEndpoint.Address.Equals(IPAddress.Any) ||
            ServerEndpoint.Address.Equals(IPAddress.IPv6Any) ||
            ServerEndpoint.Address.Equals(IPAddress.None) ||
            ServerEndpoint.Address.Equals(IPAddress.IPv6None) ||
            ServerEndpoint.Address.Equals(IPAddress.Broadcast) ||
            IsMulticast(ServerEndpoint.Address))
        {
            throw new ArgumentException("The server endpoint must be a unicast address.", nameof(ServerEndpoint));
        }
        if (Impairment.DelayMilliseconds is < 0 or > 60_000)
        {
            throw new ArgumentOutOfRangeException(nameof(Impairment), "Delay must be between 0 and 60000 ms.");
        }
        if (Impairment.JitterMilliseconds < 0 || Impairment.JitterMilliseconds > Impairment.DelayMilliseconds)
        {
            throw new ArgumentOutOfRangeException(nameof(Impairment), "Jitter must be between 0 and the configured delay.");
        }
        if (Impairment.LossBasisPoints is < 0 or > 5_000)
        {
            throw new ArgumentOutOfRangeException(nameof(Impairment), "Loss must be between 0.00 and 50.00 percent.");
        }
        if (Impairment.ReorderEvery != 0 && Impairment.ReorderEvery is < 2 or > 1_000_000)
        {
            throw new ArgumentOutOfRangeException(nameof(Impairment), "Reorder-every must be 0 (disabled) or between 2 and 1000000.");
        }
        if (Impairment.ReorderHoldMilliseconds is < 1 or > 60_000)
        {
            throw new ArgumentOutOfRangeException(nameof(Impairment), "Reorder hold must be between 1 and 60000 ms.");
        }
        if (Impairment.MaximumQueuedPackets is < 2 or > 65_536)
        {
            throw new ArgumentOutOfRangeException(nameof(Impairment), "Maximum queued packets must be between 2 and 65536.");
        }
        if (StatisticsIntervalSeconds is < 1 or > 3_600)
        {
            throw new ArgumentOutOfRangeException(nameof(StatisticsIntervalSeconds), "Statistics interval must be between 1 and 3600 seconds.");
        }
    }

    private static bool IsMulticast(IPAddress address)
    {
        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            return address.IsIPv6Multicast;
        }
        byte firstOctet = address.GetAddressBytes()[0];
        return firstOctet is >= 224 and <= 239;
    }

    public string LossPercent => (Impairment.LossBasisPoints / 100m).ToString("0.00", CultureInfo.InvariantCulture);
}

internal static class CommandLine
{
    public const string USAGE = """
        Scanner Room deterministic UDP network-impairment proxy

        Usage:
          ScannerRoom.NetworkImpairmentProxy.exe --listen <ip:port> --server <ip:port> [options]
          ScannerRoom.NetworkImpairmentProxy.exe --self-test

        Required:
          --listen <ip:port>          Endpoint clients connect to; use [ipv6]:port for IPv6.
          --server <ip:port>          Real Nitrox server endpoint (unicast IP literal).

        Impairment options (applied independently in both directions):
          --delay-ms <0..60000>       Base latency. Default: 100.
          --jitter-ms <0..delay>      Seeded +/- jitter. Default: 20.
          --loss-percent <0..50>      Seeded loss, at most 2 decimals. Default: 0.
          --reorder-every <0|2..N>    Hold every Nth accepted packet for pair reorder. Default: 0.
          --reorder-hold-ms <1..60000> Maximum pair-wait time. Default: 250.
          --seed <int32>              Reproduction seed. Default: 1425.
          --max-queue <2..65536>      Per-direction memory bound. Default: 8192.
          --stats-seconds <1..3600>   Compact statistics interval. Default: 10.

        Control:
          Ctrl+C cleanly stops the proxy and prints final [NIP1] statistics.
        """;

    public static ProxyOptions Parse(string[] args)
    {
        Dictionary<string, string> values = new(StringComparer.Ordinal);
        HashSet<string> known = new(StringComparer.Ordinal)
        {
            "--listen", "--server", "--delay-ms", "--jitter-ms", "--loss-percent",
            "--reorder-every", "--reorder-hold-ms", "--seed", "--max-queue", "--stats-seconds"
        };

        for (int i = 0; i < args.Length; i++)
        {
            string name = args[i];
            if (!known.Contains(name))
            {
                throw new ArgumentException($"Unknown option '{name}'.");
            }
            if (!values.TryAdd(name, i + 1 < args.Length ? args[++i] : throw new ArgumentException($"Missing value for '{name}'.")))
            {
                throw new ArgumentException($"Option '{name}' was specified more than once.");
            }
        }

        IPEndPoint listen = ParseEndpoint(Required(values, "--listen"), "--listen");
        IPEndPoint server = ParseEndpoint(Required(values, "--server"), "--server");
        int delay = ParseInt(values, "--delay-ms", 100);
        int jitter = ParseInt(values, "--jitter-ms", 20);
        int loss = ParseLossBasisPoints(values.GetValueOrDefault("--loss-percent", "0"));
        int reorderEvery = ParseInt(values, "--reorder-every", 0);
        int reorderHold = ParseInt(values, "--reorder-hold-ms", 250);
        int seed = ParseInt(values, "--seed", 1425);
        int maxQueue = ParseInt(values, "--max-queue", 8192);
        int statsSeconds = ParseInt(values, "--stats-seconds", 10);

        ProxyOptions result = new(
            listen,
            server,
            new ImpairmentSettings(delay, jitter, loss, reorderEvery, reorderHold, maxQueue),
            seed,
            statsSeconds);
        result.Validate();
        return result;
    }

    private static string Required(IReadOnlyDictionary<string, string> values, string name) =>
        values.TryGetValue(name, out string? value) ? value : throw new ArgumentException($"Required option '{name}' is missing.");

    private static int ParseInt(IReadOnlyDictionary<string, string> values, string name, int defaultValue)
    {
        if (!values.TryGetValue(name, out string? text))
        {
            return defaultValue;
        }
        return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value)
            ? value
            : throw new ArgumentException($"'{text}' is not a valid integer for '{name}'.");
    }

    private static int ParseLossBasisPoints(string text)
    {
        if (!decimal.TryParse(text, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out decimal percent))
        {
            throw new ArgumentException($"'{text}' is not a valid loss percentage.");
        }
        decimal basisPoints = percent * 100m;
        if (basisPoints != decimal.Truncate(basisPoints))
        {
            throw new ArgumentException("Loss percentage supports at most two decimal places.");
        }
        if (basisPoints is < int.MinValue or > int.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(text), "Loss percentage is outside the supported range.");
        }
        return (int)basisPoints;
    }

    private static IPEndPoint ParseEndpoint(string text, string name)
    {
        if (!IPEndPoint.TryParse(text, out IPEndPoint? endpoint))
        {
            throw new ArgumentException($"'{text}' is not a valid numeric IP endpoint for '{name}'.");
        }
        if (endpoint.AddressFamily is not (AddressFamily.InterNetwork or AddressFamily.InterNetworkV6))
        {
            throw new ArgumentException($"'{name}' must use IPv4 or IPv6.");
        }
        return endpoint;
    }
}
