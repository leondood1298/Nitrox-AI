using System;
using Nitrox.Model.DataStructures;

namespace Nitrox.Model.MultiplayerSession
{
    [Serializable]
    public class AuthenticationContext
    {
        public string Username { get; }
        public Optional<string> ServerPassword { get; }

        /// <summary>
        ///     The public half of the key pair the player's game keeps for this world (see <see cref="PlayerIdentityProof" />).
        ///     The server ties the name to it on the first join and refuses the name with any other key.
        /// </summary>
        public string PlayerPublicKey { get; }

        /// <summary>
        ///     The game's signature over the world identity and the challenge the server sent in its session policy, proving
        ///     it holds the private half of <see cref="PlayerPublicKey" />.
        /// </summary>
        public string PlayerSignature { get; }

        public AuthenticationContext(string username, Optional<string> serverPassword, string playerPublicKey, string playerSignature)
        {
            Username = username;
            ServerPassword = serverPassword;
            PlayerPublicKey = playerPublicKey;
            PlayerSignature = playerSignature;
        }
    }
}
