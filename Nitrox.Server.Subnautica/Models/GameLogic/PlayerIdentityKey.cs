using System.Security.Cryptography;
using System.Text;

namespace Nitrox.Server.Subnautica.Models.GameLogic;

/// <summary>
///     A player's name is tied to the key their game sent the first time the name joined this server. Only a hash of
///     the key is kept, so that a copy of the save does not let anyone join as that player.
/// </summary>
internal static class PlayerIdentityKey
{
    /// <returns>The hash of <paramref name="key" />, or null when no key was sent.</returns>
    public static string? Hash(string? key)
    {
        if (string.IsNullOrEmpty(key))
        {
            return null;
        }
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)));
    }

    /// <param name="storedHash">The hash kept with the player, null if no key is tied to the name yet.</param>
    /// <param name="presentedHash">The hash of the key the joining game sent, null if it sent none.</param>
    /// <returns>True if the joining game may use the name.</returns>
    public static bool Matches(string? storedHash, string? presentedHash)
    {
        if (storedHash == null)
        {
            return true;
        }
        if (presentedHash == null)
        {
            return false;
        }
        return CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(storedHash), Encoding.ASCII.GetBytes(presentedHash));
    }
}
