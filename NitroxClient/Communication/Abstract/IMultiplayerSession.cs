using System.Threading.Tasks;
using Nitrox.Model.MultiplayerSession;
using Nitrox.Model.Packets;
using Nitrox.Model.Subnautica.MultiplayerSession;
using Nitrox.Model.Subnautica.Packets;

namespace NitroxClient.Communication.Abstract
{
    public delegate void MultiplayerSessionConnectionStateChangedEventHandler(IMultiplayerSessionConnectionState newState);

    public interface IMultiplayerSession : IPacketSender, IMultiplayerSessionState
    {
        IMultiplayerSessionConnectionState CurrentState { get; }

        /// <summary>
        ///     Why the session ended the last connection itself (for example an incompatible version), for the player; null
        ///     when it did not.
        /// </summary>
        string DisconnectReason { get; }
        event MultiplayerSessionConnectionStateChangedEventHandler ConnectionStateChanged;

        Task ConnectAsync(string ipAddress, int port);
        void ProcessSessionPolicy(MultiplayerSessionPolicy policy);
        void RequestSessionReservation(PlayerSettings playerSettings, AuthenticationContext authenticationContext);
        void ProcessReservationResponsePacket(MultiplayerSessionReservation reservation);
        void JoinSession();
        void Disconnect();
    }
}
