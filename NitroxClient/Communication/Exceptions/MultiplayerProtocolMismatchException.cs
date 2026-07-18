using Nitrox.Model.Networking;

namespace NitroxClient.Communication.Exceptions;

public sealed class MultiplayerProtocolMismatchException : ClientConnectionFailedException
{
    public MultiplayerProtocolMismatchException(string serverConnectionKey) :
        base($"The server uses multiplayer protocol {serverConnectionKey}, but this client uses {NitroxNetworkProtocol.ConnectionKey}.")
    {
        ServerConnectionKey = serverConnectionKey;
    }

    public string ClientConnectionKey => NitroxNetworkProtocol.ConnectionKey;
    public string ServerConnectionKey { get; }
}
