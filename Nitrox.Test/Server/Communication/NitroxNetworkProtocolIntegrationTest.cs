using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using LiteNetLib;
using LiteNetLib.Layers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nitrox.Model.Configuration;
using Nitrox.Model.Core;
using Nitrox.Model.Networking;
using Nitrox.Server.Subnautica.Models.AppEvents;
using Nitrox.Server.Subnautica.Models.Communication;
using Nitrox.Server.Subnautica.Models.Logging.ZLogger;
using NitroxClient.Communication.Exceptions;
using NitroxClient.Communication.NetworkingLayer.LiteNetLib;
using ZLogger.Providers;

namespace Nitrox.Test.Server.Communication;

[TestClass]
[DoNotParallelize]
public sealed class NitroxNetworkProtocolIntegrationTest
{
    [TestMethod]
    [Timeout(15_000)]
    public async Task ProductionServerRejectsLegacyKeyAndAcceptsProductionClient()
    {
        ushort port = GetAvailableUdpPort();
        using LoggerFactory loggerFactory = new([new ZLoggerAtomicConsoleLoggerProvider(new ZLoggerConsoleOptions())]);
        SessionManager sessionManager = new(new ISessionCleaner.Trigger(() => []), loggerFactory.CreateLogger<SessionManager>());
        LiteNetLibServer server = new(null!, sessionManager, null!, null!,
                                            Options.Create(new SubnauticaServerOptions { ServerPort = port, MaxConnections = 4 }),
                                            loggerFactory.CreateLogger<LiteNetLibServer>());

        await server.StartAsync(CancellationToken.None);
        try
        {
            RawConnection legacy = await ConnectRawAsync(port, "nitrox");
            try
            {
                legacy.Connected.Should().BeFalse();
                legacy.DisconnectReason.Should().Be(DisconnectReason.ConnectionRejected);
                legacy.ServerConnectionKey.Should().Be(NitroxNetworkProtocol.ConnectionKey);
                sessionManager.GetSessionCount().Should().Be(0);
            }
            finally
            {
                legacy.Client.Stop();
            }

            LiteNetLibClient currentClient = new(null!);
            try
            {
                await ConnectProductionClientAsync(currentClient, port);
                currentClient.IsConnected.Should().BeTrue();
                await WaitUntilAsync(() => sessionManager.GetSessionCount() == 1);
            }
            finally
            {
                currentClient.Stop();
            }

            await WaitUntilAsync(() => sessionManager.GetSessionCount() == 0);
        }
        finally
        {
            await server.StopAsync(CancellationToken.None);
        }
    }

    [TestMethod]
    [Timeout(15_000)]
    public async Task ProductionClientReportsRejectedProtocolEpoch()
    {
        ushort port = GetAvailableUdpPort();
        EventBasedNetListener listener = new();
        NetManager server = new(listener, NitroxEnvironment.IsReleaseMode ? new Crc32cLayer() : null)
        {
            UnsyncedEvents = true
        };
        listener.ConnectionRequestEvent += request =>
        {
            Thread.Sleep(2_500);
            LiteNetLib.Utils.NetDataWriter rejectionData = new();
            rejectionData.Put("nitrox-ai/3");
            request.Reject(rejectionData);
        };
        server.Start(port);

        LiteNetLibClient client = new(null!);
        try
        {
            Func<Task> connect = async () => await ConnectProductionClientAsync(client, port);
            MultiplayerProtocolMismatchException exception = (await connect.Should().ThrowAsync<MultiplayerProtocolMismatchException>()).Which;
            exception.ServerConnectionKey.Should().Be("nitrox-ai/3");
            exception.ClientConnectionKey.Should().Be(NitroxNetworkProtocol.ConnectionKey);
        }
        finally
        {
            client.Stop();
            server.Stop();
        }
    }

    private static async Task ConnectProductionClientAsync(LiteNetLibClient client, ushort port)
    {
        Task connection = client.StartAsync(IPAddress.Loopback.ToString(), port);
        Stopwatch timeout = Stopwatch.StartNew();
        while (!connection.IsCompleted && timeout.Elapsed < TimeSpan.FromSeconds(5))
        {
            client.PollEvents();
            await Task.Delay(10);
        }
        await connection;
    }

    private static async Task<RawConnection> ConnectRawAsync(ushort port, string connectionKey)
    {
        TaskCompletionSource<RawConnectionResult> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        EventBasedNetListener listener = new();
        NetManager client = new(listener, NitroxEnvironment.IsReleaseMode ? new Crc32cLayer() : null)
        {
            IPv6Enabled = true,
            UnsyncedEvents = true
        };
        listener.PeerConnectedEvent += _ => completion.TrySetResult(new RawConnectionResult(true, null, null));
        listener.PeerDisconnectedEvent += (_, disconnectInfo) =>
        {
            disconnectInfo.AdditionalData.TryGetString(out string serverConnectionKey);
            completion.TrySetResult(new RawConnectionResult(false, disconnectInfo.Reason, serverConnectionKey));
        };
        client.Start();
        client.Connect(IPAddress.Loopback.ToString(), port, connectionKey);

        RawConnectionResult result = await completion.Task.WaitAsync(TimeSpan.FromSeconds(5));
        return new RawConnection(client, result.Connected, result.DisconnectReason, result.ServerConnectionKey);
    }

    private static ushort GetAvailableUdpPort()
    {
        using UdpClient portReservation = new(0);
        return (ushort)((IPEndPoint)portReservation.Client.LocalEndPoint!).Port;
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        Stopwatch timeout = Stopwatch.StartNew();
        while (!condition() && timeout.Elapsed < TimeSpan.FromSeconds(5))
        {
            await Task.Delay(10);
        }
        condition().Should().BeTrue();
    }

    private sealed record RawConnection(NetManager Client, bool Connected, DisconnectReason? DisconnectReason, string? ServerConnectionKey);
    private sealed record RawConnectionResult(bool Connected, DisconnectReason? DisconnectReason, string? ServerConnectionKey);
}
