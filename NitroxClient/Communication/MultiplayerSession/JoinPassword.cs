using Nitrox.Model.DataStructures;

namespace NitroxClient.Communication.MultiplayerSession;

public static class JoinPassword
{
    /// <summary>
    ///     The password a reservation request carries: only one typed for this server, and only when it asks for one. A
    ///     password typed for another server must never reach this one.
    /// </summary>
    public static Optional<string> ToSend(bool serverRequiresPassword, Optional<string> typedForThisAttempt)
    {
        return serverRequiresPassword ? typedForThisAttempt : Optional.Empty;
    }
}
