using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Nitrox.Model.Configuration;
using Nitrox.Model.Core;
using Nitrox.Model.DataStructures;
using Nitrox.Model.DataStructures.Unity;
using Nitrox.Model.MultiplayerSession;
using Nitrox.Model.Packets;
using Nitrox.Model.Subnautica.MultiplayerSession;
using Nitrox.Model.Subnautica.Packets;
using Nitrox.Server.Subnautica.Models.AppEvents;
using Nitrox.Server.Subnautica.Models.Communication;
using Nitrox.Server.Subnautica.Models.GameLogic;
using Nitrox.Server.Subnautica.Models.Packets.Core;
using Nitrox.Server.Subnautica.Models.Serialization;
using Nitrox.Server.Subnautica.Services;
using NSubstitute;
using ServerPlayer = Nitrox.Server.Subnautica.Models.Player;

namespace Nitrox.Test.Server.Helper;

/// <summary>
///     A real <see cref="PlayerManager" /> with players connected the way the server connects them, for processors that
///     decide who receives a packet.
/// </summary>
internal sealed class ConnectedPlayers
{
    private readonly SessionManager sessionManager = new(new ISessionCleaner.Trigger(() => []), NullLogger<SessionManager>.Instance);
    private int nextPort = 20000;

    public PlayerManager PlayerManager { get; }

    /// <param name="worldIdentity">For tests of names tied to keys across a change of the world identity.</param>
    public ConnectedPlayers(WorldIdentity? worldIdentity = null)
    {
        // The ban check a connecting player goes through waits until the ban list has been loaded, which happens when
        // the service starts. No save has this name, so there is no ban file to read.
        ServerStartOptions startOptions = new() { SaveName = $"test-{Guid.NewGuid():N}" };
        BanService banService = new(new ServerJsonSerializer(NullLogger<ServerJsonSerializer>.Instance), Options.Create(startOptions), NullLogger<BanService>.Instance);
        banService.StartAsync(CancellationToken.None).GetAwaiter().GetResult();
        banService.StopAsync(CancellationToken.None).GetAwaiter().GetResult();
        PlayerManager = new PlayerManager(sessionManager, banService, Options.Create(new SubnauticaServerOptions()), NullLogger<PlayerManager>.Instance, worldIdentity);
    }

    public SessionManager SessionManager => sessionManager;

    public ServerPlayer Connect(string name, string key = "key")
    {
        SessionId sessionId = Reserve(name, key, out MultiplayerSessionReservation reservation);
        reservation.ReservationState.Should().Be(MultiplayerSessionReservationState.RESERVED);
        return PlayerManager.CreatePlayerData(sessionId, out _);
    }

    /// <summary>
    ///     Connects a session and asks for a reservation, the way a joining game does before it waits in the queue.
    /// </summary>
    public SessionId Reserve(string name, string key, out MultiplayerSessionReservation reservation)
    {
        IPEndPoint endPoint = new(IPAddress.Loopback, nextPort++);
        SessionId sessionId = sessionManager.GetOrCreateSession(endPoint).Id;
        reservation = PlayerManager.ReservePlayerContext(sessionId, endPoint, new PlayerSettings(new NitroxColor(1f, 1f, 1f)), new AuthenticationContext(name, Optional.Empty, key));
        return sessionId;
    }

    /// <summary>
    ///     Ends a session the way a lost connection does, as far as the player manager is concerned.
    /// </summary>
    public void Disconnect(SessionId sessionId)
    {
        PlayerManager.OnEventAsync(new ISessionCleaner.Args(new SessionManager.Session(sessionId, new IPEndPoint(IPAddress.Loopback, 1)), 0)).GetAwaiter().GetResult();
    }

    /// <summary>
    ///     The packets of type <typeparamref name="T" /> a substituted sender was asked to send to one player.
    /// </summary>
    public static List<T> SentTo<T>(IPacketSender packetSender, ServerPlayer player) where T : Packet
    {
        return packetSender.ReceivedCalls()
                           .Where(call => call.GetMethodInfo().Name == nameof(IPacketSender.SendPacketAsync) && Equals(call.GetArguments()[1], player.SessionId))
                           .Select(call => call.GetArguments()[0])
                           .OfType<T>()
                           .ToList();
    }

    /// <summary>
    ///     The packets of type <typeparamref name="T" /> a substituted sender was asked to send to every player.
    /// </summary>
    public static List<T> SentToAll<T>(IPacketSender packetSender) where T : Packet
    {
        return packetSender.ReceivedCalls()
                           .Where(call => call.GetMethodInfo().Name == nameof(IPacketSender.SendPacketToAllAsync))
                           .Select(call => call.GetArguments()[0])
                           .OfType<T>()
                           .ToList();
    }
}
