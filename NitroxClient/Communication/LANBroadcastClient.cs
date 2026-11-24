using System;
using System.Collections.Concurrent;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using LiteNetLib;
using LiteNetLib.Utils;
using Nitrox.Model.Constants;
using Nitrox.Model.Extensions;
using Nitrox.Model.Helper;

namespace NitroxClient.Communication;

/// <summary>
///     Asks the local network for Nitrox servers, again and again for as long as it runs, so that servers started later are
///     found and every server that still runs answers each time (see <see cref="LanServerTracker" /> for dropping the others).
/// </summary>
public static class LANBroadcastClient
{
    private const int BROADCAST_INTERVAL_MS = 3000;
    private const int POLL_INTERVAL_MS = 100;

    private static readonly ConcurrentQueue<IPEndPoint> answers = [];
    private static readonly object stateLock = new();
    private static CancellationTokenSource? searchCancellation;

    /// <summary>
    ///     Starts asking, unless already asking. Answers are queued until taken with <see cref="TryTakeAnswer" />.
    /// </summary>
    public static void Start()
    {
        lock (stateLock)
        {
            if (searchCancellation != null)
            {
                return;
            }
            searchCancellation = new CancellationTokenSource();
            CancellationToken token = searchCancellation.Token;
            Task.Run(() => SearchAsync(token), token).ContinueWithHandleError(ex => Log.Warn($"LAN server search stopped: {ex.Message}"));
        }
    }

    public static void Stop()
    {
        lock (stateLock)
        {
            searchCancellation?.Cancel();
            searchCancellation?.Dispose();
            searchCancellation = null;
        }
    }

    public static bool TryTakeAnswer(out IPEndPoint endPoint) => answers.TryDequeue(out endPoint);

    private static async Task SearchAsync(CancellationToken cancellationToken)
    {
        EventBasedNetListener listener = new();
        NetManager client = new(listener)
        {
            AutoRecycle = true,
            BroadcastReceiveEnabled = true,
            UnconnectedMessagesEnabled = true
        };
        // Try start client on an available, predefined, port
        foreach (int port in LANDiscoveryConstants.BROADCAST_PORTS)
        {
            if (client.Start(port))
            {
                break;
            }
        }
        if (!client.IsRunning)
        {
            Log.Warn("Failed to start LAN discover client: none of the defined ports are available");
            return;
        }

        Log.Info("Searching for LAN servers...");
        listener.NetworkReceiveUnconnectedEvent += ReceivedResponse;
        try
        {
            DateTime nextBroadcast = DateTime.MinValue;
            while (!cancellationToken.IsCancellationRequested)
            {
                if (DateTime.UtcNow >= nextBroadcast)
                {
                    NetDataWriter writer = new();
                    writer.Put(LANDiscoveryConstants.BROADCAST_REQUEST_STRING);
                    foreach (int port in LANDiscoveryConstants.BROADCAST_PORTS)
                    {
                        client.SendBroadcast(writer, port);
                    }
                    nextBroadcast = DateTime.UtcNow.AddMilliseconds(BROADCAST_INTERVAL_MS);
                }
                client.PollEvents();
                try
                {
                    await Task.Delay(POLL_INTERVAL_MS, cancellationToken);
                }
                catch (TaskCanceledException)
                {
                    // Stopped
                }
            }
        }
        finally
        {
            listener.ClearNetworkReceiveUnconnectedEvent();
            client.Stop();
        }

        static void ReceivedResponse(IPEndPoint remoteEndPoint, NetPacketReader reader, UnconnectedMessageType messageType)
        {
            if (messageType != UnconnectedMessageType.Broadcast)
            {
                return;
            }
            string responseString = reader.GetString();
            if (responseString != LANDiscoveryConstants.BROADCAST_RESPONSE_STRING)
            {
                return;
            }
            int serverPort = reader.GetInt();
            answers.Enqueue(new IPEndPoint(remoteEndPoint.Address.TryExtractMappedIPv4(), serverPort));
        }
    }
}
