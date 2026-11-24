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
        ///     A secret the player's game keeps for this server. The server ties the name to it on the first join and
        ///     refuses the name with any other key, so that nobody can join as an offline player.
        /// </summary>
        public string PlayerKey { get; }

        public AuthenticationContext(string username, Optional<string> serverPassword, string playerKey)
        {
            Username = username;
            ServerPassword = serverPassword;
            PlayerKey = playerKey;
        }
    }
}
