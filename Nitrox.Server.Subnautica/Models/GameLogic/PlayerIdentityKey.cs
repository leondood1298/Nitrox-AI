using System.Security.Cryptography;
using System.Text;

namespace Nitrox.Server.Subnautica.Models.GameLogic;

/// <summary>
///     A player's name is tied to the public key their game proved it holds the first time the name joined this server
///     (see <see cref="Nitrox.Model.MultiplayerSession.PlayerIdentityProof" />). Only the public half is kept, so a copy of
///     the save does not let anyone join as that player.
/// </summary>
internal static class PlayerIdentityKey
{
    /// <param name="storedPublicKey">The key kept with the player, null if no key is tied to the name yet.</param>
    /// <param name="presentedPublicKey">The key the joining game proved it holds.</param>
    /// <returns>True if the joining game may use the name.</returns>
    public static bool Matches(string? storedPublicKey, string? presentedPublicKey)
    {
        if (storedPublicKey == null)
        {
            return true;
        }
        if (presentedPublicKey == null)
        {
            return false;
        }
        return CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(storedPublicKey), Encoding.ASCII.GetBytes(presentedPublicKey));
    }
}
