using System;
using Nitrox.Model.Core;
using Nitrox.Model.DataStructures;
using Nitrox.Model.MultiplayerSession;
using Nitrox.Model.Packets;

namespace Nitrox.Model.Subnautica.Packets;

[Serializable]
public sealed class MultiplayerSessionPolicy : Packet
{
    public SessionId SessionId { get; }
    public bool RequiresServerPassword { get; }
    public MultiplayerSessionAuthenticationAuthority AuthenticationAuthority { get; }
    public bool DisableConsole { get; }
    public int MaxConnections { get; }
    public NitroxVersion NitroxVersionAllowed { get; }

    /// <summary>
    ///     An identifier the world was given once and keeps in its save. Not a secret: the joining game derives the key
    ///     it ties its player name to from its own secret and this, so the key does not depend on the address used to
    ///     reach the server, and no server learns the key used on another world.
    /// </summary>
    public string WorldIdentity { get; }

    /// <summary>
    ///     A fresh random value for this connection only: the joining game signs it with the world identity to prove it
    ///     holds the key its name is tied to (see <see cref="Nitrox.Model.MultiplayerSession.PlayerIdentityProof" />).
    /// </summary>
    public string JoinChallenge { get; }

    public MultiplayerSessionPolicy(SessionId sessionId, bool disableConsole, int maxConnections, bool requiresServerPassword, string worldIdentity, string joinChallenge)
    {
        SessionId = sessionId;
        RequiresServerPassword = requiresServerPassword;
        AuthenticationAuthority = MultiplayerSessionAuthenticationAuthority.SERVER;
        DisableConsole = disableConsole;
        MaxConnections = maxConnections;
        WorldIdentity = worldIdentity;
        JoinChallenge = joinChallenge;

        Version ver = NitroxEnvironment.Version;
        // only the major and minor version number is required
        NitroxVersionAllowed = new(ver.Major, ver.Minor);
    }

    /// <remarks>Used for deserialization</remarks>
    public MultiplayerSessionPolicy(SessionId sessionId, bool requiresServerPassword, MultiplayerSessionAuthenticationAuthority authenticationAuthority,
                                    bool disableConsole, int maxConnections, NitroxVersion nitroxVersionAllowed, string worldIdentity, string joinChallenge)
    {
        SessionId = sessionId;
        RequiresServerPassword = requiresServerPassword;
        AuthenticationAuthority = authenticationAuthority;
        DisableConsole = disableConsole;
        MaxConnections = maxConnections;
        NitroxVersionAllowed = nitroxVersionAllowed;
        WorldIdentity = worldIdentity;
        JoinChallenge = joinChallenge;
    }
}
