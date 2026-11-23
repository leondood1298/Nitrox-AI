using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Channels;
using Mono.Nat;

namespace Nitrox.Server.Subnautica.Services;

/// <summary>
///     Uses <see cref="SubnauticaServerOptions" /> to automatically open server listening port on network router via
///     <a href="https://en.wikipedia.org/wiki/Universal_Plug_and_Play">UPnP</a>.
/// </summary>
/// <remarks>
///     <para>
///         By port forwarding, incoming connections will be forwarded to the host machine running the game server.<br /><br />
///         <b>Trivia</b>: Routers are by default configured to block incoming connections coming from WAN, for security.
///     </para>
///     <para>
///         Mappings are leased and renewed while the server runs, so the router closes the port by itself after a crash.
///         Only mappings this server made are removed when it stops: one that was already there, such as a forward set up
///         by hand, is left alone.
///     </para>
/// </remarks>
internal class PortForwardService(IOptionsMonitor<SubnauticaServerOptions> optionsProvider, ILogger<PortForwardService> logger) : BackgroundService
{
    /// <summary>
    ///     Renewed well before the lease ends, so that a router that is slow to answer once does not drop the port.
    /// </summary>
    private static readonly TimeSpan RENEW_INTERVAL = TimeSpan.FromSeconds(NatHelper.LEASE_SECONDS / 4.0);

    private readonly string portMappingDescription = $"{MAPPING_DESCRIPTION_PREFIX} with process id {Environment.ProcessId}";
    private readonly ILogger<PortForwardService> logger = logger;
    private readonly ConcurrentDictionary<ushort, int> createdPorts = [];
    private readonly IOptionsMonitor<SubnauticaServerOptions> optionsProvider = optionsProvider;

    private readonly Channel<PortForwardAction> portForwardChannel = Channel.CreateBounded<PortForwardAction>(new BoundedChannelOptions(10)
    {
        AllowSynchronousContinuations = false,
        FullMode = BoundedChannelFullMode.DropOldest,
        SingleReader = true,
        SingleWriter = false
    });

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        ListenForOptionsChange();
        await QueueInitialPortOpenAsync();
        _ = QueueRenewalsAsync();

        try
        {
            await foreach (PortForwardAction action in portForwardChannel.Reader.ReadAllAsync(stoppingToken))
            {
                switch (action)
                {
                    case { Kind: ActionKind.OPEN, Port: var port } when !createdPorts.ContainsKey(port):
                        await OpenPortAsync(port, stoppingToken);
                        break;
                    case { Kind: ActionKind.RENEW, Port: var port } when createdPorts.TryGetValue(port, out int lifetime) && lifetime != NatHelper.PERMANENT:
                        await RenewPortAsync(port, stoppingToken);
                        break;
                    case { Kind: ActionKind.CLOSE, Port: var port } when createdPorts.ContainsKey(port):
                        await ClosePortAsync(port, stoppingToken);
                        createdPorts.TryRemove(port, out int _);
                        break;
                }
            }
        }
        catch (OperationCanceledException)
        {
            foreach (KeyValuePair<ushort, int> pair in createdPorts)
            {
                await ClosePortAsync(pair.Key, CancellationToken.None);
            }
            createdPorts.Clear();
            throw;
        }

        void ListenForOptionsChange()
        {
            IDisposable optionsMonitorDisposable = optionsProvider.OnChange(options =>
            {
                if (options.PortForward)
                {
                    // Remove ports opened prior. TODO: Delay closing port if player connections are still active?
                    foreach (KeyValuePair<ushort, int> pair in createdPorts)
                    {
                        if (pair.Key == options.ServerPort)
                        {
                            continue;
                        }
                        portForwardChannel.Writer.TryWrite(new PortForwardAction(pair.Key, ActionKind.CLOSE));
                    }
                    // Open the new port.
                    portForwardChannel.Writer.TryWrite(new PortForwardAction(options.ServerPort, ActionKind.OPEN));
                }
                else
                {
                    // Close all ports as port forwarding option is disabled.
                    foreach (KeyValuePair<ushort, int> pair in createdPorts)
                    {
                        portForwardChannel.Writer.TryWrite(new PortForwardAction(pair.Key, ActionKind.CLOSE));
                    }
                }
            });
            stoppingToken.Register(() => optionsMonitorDisposable?.Dispose());
        }

        async Task QueueInitialPortOpenAsync()
        {
            if (optionsProvider.CurrentValue is { PortForward: true, ServerPort: var port })
            {
                await portForwardChannel.Writer.WriteAsync(new PortForwardAction(port, ActionKind.OPEN), stoppingToken);
            }
        }

        async Task QueueRenewalsAsync()
        {
            try
            {
                using PeriodicTimer timer = new(RENEW_INTERVAL);
                while (await timer.WaitForNextTickAsync(stoppingToken))
                {
                    foreach (ushort port in createdPorts.Keys)
                    {
                        portForwardChannel.Writer.TryWrite(new PortForwardAction(port, ActionKind.RENEW));
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Stopping
            }
        }
    }

    private async Task OpenPortAsync(ushort port, CancellationToken cancellationToken = default)
    {
        if (await NatHelper.GetPortMappingAsync(port, Protocol.Udp, cancellationToken) is { } existing && !IsLeftOfNitroxServer(existing.Description))
        {
            // Not made by Nitrox (a forward set up by hand, say), so it is neither renewed nor removed
            logger.ZLogInformation($"Port {port:@Port} UDP is already port forwarded");
            return;
        }

        int lifetime = NatHelper.LEASE_SECONDS;
        NatHelper.ResultCodes mappingResult = await NatHelper.AddPortMappingAsync(port, Protocol.Udp, portMappingDescription, cancellationToken, lifetime);
        if (mappingResult == NatHelper.ResultCodes.UNKNOWN_ERROR && !cancellationToken.IsCancellationRequested)
        {
            // Some routers only accept permanent mappings
            lifetime = NatHelper.PERMANENT;
            mappingResult = await NatHelper.AddPortMappingAsync(port, Protocol.Udp, portMappingDescription, cancellationToken, lifetime);
        }
        if (!cancellationToken.IsCancellationRequested)
        {
            switch (mappingResult)
            {
                case NatHelper.ResultCodes.SUCCESS:
                    createdPorts[port] = lifetime;
                    logger.ZLogInformation($"Server port {port:@Port} UDP has been automatically opened on your router (port is closed when server closes)");
                    if (lifetime == NatHelper.PERMANENT)
                    {
                        logger.ZLogWarning($"Your router only keeps port forwards until they are removed: if this server crashes, port {port:@Port} stays open on the router until a Nitrox server starts and stops again.");
                    }
                    break;
                case NatHelper.ResultCodes.CONFLICT_IN_MAPPING_ENTRY:
                    logger.ZLogWarning($"Port forward for {port:@Port} UDP failed. It appears to already be port forwarded or it conflicts with another port forward rule.");
                    break;
                case NatHelper.ResultCodes.UNKNOWN_ERROR:
                    logger.ZLogWarning($"Failed to port forward {port:@Port} UDP through UPnP. If using a VPN or you've manually port-forwarded, please disregard this warning. To disable this feature you can go into the server settings.");
                    break;
            }
        }
    }

    private async Task RenewPortAsync(ushort port, CancellationToken cancellationToken)
    {
        // Adding the same mapping again restarts its lease
        if (await NatHelper.AddPortMappingAsync(port, Protocol.Udp, portMappingDescription, cancellationToken) != NatHelper.ResultCodes.SUCCESS && !cancellationToken.IsCancellationRequested)
        {
            logger.ZLogWarning($"Could not renew the port forward of {port:@Port} UDP on your router; it will try again in {RENEW_INTERVAL.TotalMinutes:0} minutes.");
        }
    }

    private async Task ClosePortAsync(ushort port, CancellationToken cancellationToken = default)
    {
        if (await NatHelper.DeletePortMappingAsync(port, Protocol.Udp, cancellationToken))
        {
            logger.ZLogInformation($"Removed port forward rule {port:@Port} UDP");
        }
        else
        {
            logger.ZLogInformation($"Failed to remove port forward rule {port:@Port} UDP");
        }
    }

    /// <summary>
    ///     A mapping an earlier Nitrox server left behind, by crashing say, is taken over and removed on stop like one this
    ///     server made.
    /// </summary>
    internal static bool IsLeftOfNitroxServer(string? description) => description?.StartsWith(MAPPING_DESCRIPTION_PREFIX, StringComparison.Ordinal) == true;

    private const string MAPPING_DESCRIPTION_PREFIX = "Nitrox server";

    private enum ActionKind
    {
        OPEN,
        RENEW,
        CLOSE
    }

    private record PortForwardAction(ushort Port, ActionKind Kind);
}
