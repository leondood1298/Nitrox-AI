using System.Security.Cryptography;
using System.Text;

namespace Nitrox.Server.Subnautica.Models.Administration;

/// <summary>
///     Compares a typed password with the stored one in a time that does not depend on where they first differ (nor on
///     their lengths, as both are hashed first), so timing replies does not reveal the password letter by letter.
/// </summary>
internal static class SecretComparison
{
    public static bool AreEqual(string? typed, string? stored)
    {
        byte[] typedHash = SHA256.HashData(Encoding.UTF8.GetBytes(typed ?? ""));
        byte[] storedHash = SHA256.HashData(Encoding.UTF8.GetBytes(stored ?? ""));
        return CryptographicOperations.FixedTimeEquals(typedHash, storedHash);
    }
}
