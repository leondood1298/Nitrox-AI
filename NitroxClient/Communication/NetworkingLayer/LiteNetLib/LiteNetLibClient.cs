using System;
using System.Buffers;
using System.Reflection;
using System.Threading.Tasks;
using LiteNetLib;
using LiteNetLib.Layers;
using LiteNetLib.Utils;
using Nitrox.Model.Core;
using NitroxClient.Communication.Abstract;
using NitroxClient.Debuggers;
using NitroxClient.MonoBehaviours;
using NitroxClient.MonoBehaviours.Gui.Modals;
using Nitrox.Model.Networking;
using Nitrox.Model.Packets;
using NitroxClient.Communication.Exceptions;

namespace NitroxClient.Communication.NetworkingLayer.LiteNetLib;

public class LiteNetLibClient : IClient
{
    private const int CONNECTION_TIMEOUT_MS = 10_000;

    private readonly NetManager client;
    private readonly object connectionLock = new();

    private readonly NetDataWriter dataWriter = new();
    private readonly INetworkDebugger networkDebugger;
    private readonly PacketReceiver packetReceiver;
    private readonly FieldInfo manualModeFieldInfo = typeof(NetManager).GetField("_manualMode", BindingFlags.Instance | BindingFlags.NonPublic);
    private bool isStopping;
    private NetPeer currentPeer;
    private TaskCompletionSource<MultiplayerProtocolMismatchException> connectionCompletion;

    public bool IsConnected { get; private set; }
    public int PingInterval
    {
        get => client.PingInterval;
        set => client.PingInterval = value;
    }
    public Action<long> LatencyUpdateCallback;

    public LiteNetLibClient(PacketReceiver packetReceiver, INetworkDebugger networkDebugger = null)
    {
        this.packetReceiver = packetReceiver;
        this.networkDebugger = networkDebugger;
        EventBasedNetListener listener = new();
        listener.PeerConnectedEvent += Connected;
        listener.PeerDisconnectedEvent += Disconnected;
        listener.NetworkReceiveEvent += ReceivedNetworkData;
        listener.NetworkLatencyUpdateEvent += (peer, _) =>
        {
            LatencyUpdateCallback?.Invoke(peer.RemoteTimeDelta);
        };


        client = new NetManager(listener, NitroxEnvironment.IsReleaseMode ? new Crc32cLayer() : null)
        {
            UpdateTime = 15,
            ChannelsCount = (byte)typeof(Packet.UdpChannelId).GetEnumValues().Length,
            IPv6Enabled = true,
#if DEBUG
            DisconnectTimeout = 300_000, //Disables Timeout (for 5 min) for debug purpose (like if you jump though the server code)
#else
            DisconnectTimeout = 30_000, // 30 seconds; prevents false disconnects when post-sync game-loading stalls LiteNetLib briefly
#endif
        };
    }

    public async Task StartAsync(string ipAddress, int serverPort)
    {
        Log.Info("Initializing LiteNetLibClient...");
        TaskCompletionSource<MultiplayerProtocolMismatchException> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);

        // ConfigureAwait(false) is needed because Unity uses a custom "UnitySynchronizationContext". Which makes async/await work like Unity coroutines.
        // Because this Task.Run is async-over-sync this would otherwise blocks the main thread as it wants to, without ConfigureAwait(false), continue on the same thread (i.e. main thread).
        NetPeer attemptPeer = await Task.Run(() =>
        {
            lock (connectionLock)
            {
                if (isStopping || currentPeer != null || connectionCompletion != null)
                {
                    throw new InvalidOperationException("A connection attempt is already active.");
                }

                client.Start();
                NetPeer peer = client.Connect(ipAddress, serverPort, NitroxNetworkProtocol.ConnectionKey);
                if (peer == null)
                {
                    throw new ClientConnectionFailedException("LiteNetLib did not create a peer for the connection attempt.");
                }

                connectionCompletion = completion;
                currentPeer = peer;
                return peer;
            }
        }).ConfigureAwait(false);

        Task completedTask = await Task.WhenAny(completion.Task, Task.Delay(CONNECTION_TIMEOUT_MS)).ConfigureAwait(false);
        if (completedTask != completion.Task)
        {
            bool timedOut = false;
            lock (connectionLock)
            {
                if (ReferenceEquals(currentPeer, attemptPeer) && !completion.Task.IsCompleted)
                {
                    isStopping = true;
                    currentPeer = null;
                    connectionCompletion = null;
                    IsConnected = false;
                    timedOut = true;
                }
            }

            if (timedOut)
            {
                try
                {
                    client.Stop();
                }
                finally
                {
                    lock (connectionLock)
                    {
                        isStopping = false;
                    }
                }
                return;
            }

        }

        MultiplayerProtocolMismatchException connectionFailure = await completion.Task.ConfigureAwait(false);
        if (connectionFailure != null)
        {
            throw connectionFailure;
        }
    }

    public void Send(Packet packet)
    {
        byte[] packetData = packet.Serialize();
        dataWriter.Reset();
        dataWriter.Put(packetData.Length);
        dataWriter.Put(packetData);

        networkDebugger?.PacketSent(packet, dataWriter.Length);
        client.SendToAll(dataWriter, (byte)packet.UdpChannel, NitroxDeliveryMethod.ToLiteNetLib(packet.DeliveryMethod));
    }

    public void Stop()
    {
        TaskCompletionSource<MultiplayerProtocolMismatchException> completion;
        lock (connectionLock)
        {
            if (isStopping)
            {
                return;
            }

            isStopping = true;
            IsConnected = false;
            currentPeer = null;
            completion = connectionCompletion;
            connectionCompletion = null;
        }

        try
        {
            client.Stop();
        }
        finally
        {
            lock (connectionLock)
            {
                isStopping = false;
            }
            completion?.TrySetResult(null);
        }
    }

    /// <summary>
    ///     This should be called <b>once</b> each game tick
    /// </summary>
    public void PollEvents() => client.PollEvents();

    private void ReceivedNetworkData(NetPeer peer, NetDataReader reader, byte channel, DeliveryMethod deliveryMethod)
    {
        int packetDataLength = reader.GetInt();
        byte[] packetData = ArrayPool<byte>.Shared.Rent(packetDataLength);
        try
        {
            reader.GetBytes(packetData, packetDataLength);
            Packet packet = Packet.Deserialize(packetData);
            packetReceiver.Add(packet);
            networkDebugger?.PacketReceived(packet, packetDataLength);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(packetData, true);
        }
    }

    private void Connected(NetPeer peer)
    {
        lock (connectionLock)
        {
            if (!ReferenceEquals(currentPeer, peer))
            {
                Log.Info("Ignored a connection event from a stale peer");
                return;
            }

            // IsConnected must happen before completing the task so the unblocked connection flow observes the new state.
            IsConnected = true;
            connectionCompletion?.TrySetResult(null);
        }
        Log.Info("Connected to server");
    }

    private void Disconnected(NetPeer peer, DisconnectInfo disconnectInfo)
    {
        bool showLostConnection;
        lock (connectionLock)
        {
            if (!ReferenceEquals(currentPeer, peer))
            {
                Log.Info("Ignored a disconnection event from a stale peer");
                return;
            }

            // Check must happen before IsConnected is set to false, so that it doesn't send an exception when we aren't even ingame.
            showLostConnection = Multiplayer.Active;
            IsConnected = false;

            MultiplayerProtocolMismatchException connectionFailure = null;
            if (disconnectInfo.Reason == DisconnectReason.ConnectionRejected &&
                disconnectInfo.AdditionalData.TryGetString(out string serverConnectionKey) &&
                !NitroxNetworkProtocol.IsCompatible(serverConnectionKey))
            {
                connectionFailure = new MultiplayerProtocolMismatchException(serverConnectionKey);
            }

            connectionCompletion?.TrySetResult(connectionFailure);
            connectionCompletion = null;
            currentPeer = null;
        }

        if (showLostConnection)
        {
            Modal.Get<LostConnectionModal>()?.Show();
        }
        Log.Info("Disconnected from server");
    }

    internal void ForceUpdate()
    {
        int pingInterval = PingInterval;
        // Set PingInterval to 0 so another ping is sent immediately
        PingInterval = 0;
        // ManualUpdate requires the client to have _manualMode set to true so we temporarily do so
        manualModeFieldInfo.SetValue(client, true);
        client.ManualUpdate(0);
        manualModeFieldInfo.SetValue(client, false);
        // We set it back to its high value so another ping isn't sent while we're waiting for the previous one
        PingInterval = pingInterval;
    }
}
