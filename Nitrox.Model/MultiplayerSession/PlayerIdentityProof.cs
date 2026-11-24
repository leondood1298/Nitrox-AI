using System;
using System.Security.Cryptography;
using System.Text;

namespace Nitrox.Model.MultiplayerSession;

/// <summary>
///     How a joining game proves it holds the key its player name is tied to, without sending the key itself: the game
///     keeps one RSA key pair per world, the server keeps the public half with the player, and every join the game signs
///     a fresh challenge from the server together with the world's identity. A signature cannot be used again (the
///     challenge is new each time), and a server announcing another world's identity only gets signatures for its own
///     challenges, which the real server never issued.
/// </summary>
/// <remarks>
///     RSA 2048 with SHA-256 and PKCS#1 v1.5 padding: the game's Mono runtime signs with it
///     (RSACryptoServiceProvider.SignHash maps SHA256 and Pkcs1), the server verifies with .NET.
/// </remarks>
public static class PlayerIdentityProof
{
    public const int KEY_SIZE_BITS = 2048;

    /// <summary>
    ///     Length of the server's challenge, in bytes.
    /// </summary>
    public const int CHALLENGE_LENGTH = 32;

    private const string MESSAGE_PREFIX = "Nitrox player identity v1";
    private const char PUBLIC_KEY_SEPARATOR = ':';

    /// <summary>
    ///     The bytes a game signs to join a world.
    /// </summary>
    public static byte[] BuildMessage(string worldIdentity, string challenge)
    {
        return Encoding.UTF8.GetBytes($"{MESSAGE_PREFIX}\n{worldIdentity ?? ""}\n{challenge ?? ""}");
    }

    /// <summary>
    ///     The public half of a key in the form it is sent and stored: modulus and exponent, each in base64.
    /// </summary>
    public static string FormatPublicKey(RSAParameters parameters)
    {
        return $"{Convert.ToBase64String(parameters.Modulus)}{PUBLIC_KEY_SEPARATOR}{Convert.ToBase64String(parameters.Exponent)}";
    }

    /// <returns>False if the text is no public key of the expected size.</returns>
    public static bool TryParsePublicKey(string publicKey, out RSAParameters parameters)
    {
        parameters = default;
        if (string.IsNullOrEmpty(publicKey))
        {
            return false;
        }
        string[] parts = publicKey.Split(PUBLIC_KEY_SEPARATOR);
        if (parts.Length != 2)
        {
            return false;
        }
        try
        {
            byte[] modulus = Convert.FromBase64String(parts[0]);
            byte[] exponent = Convert.FromBase64String(parts[1]);
            // A smaller key could be broken; a larger one is not what any game makes.
            if (modulus.Length != KEY_SIZE_BITS / 8 || exponent.Length is < 1 or > 4)
            {
                return false;
            }
            parameters = new RSAParameters { Modulus = modulus, Exponent = exponent };
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    /// <returns>True if <paramref name="signature" /> (base64) is the holder of <paramref name="publicKey" /> signing this world and challenge.</returns>
    public static bool Verify(string publicKey, string signature, string worldIdentity, string challenge)
    {
        if (string.IsNullOrEmpty(signature) || string.IsNullOrEmpty(challenge) || !TryParsePublicKey(publicKey, out RSAParameters parameters))
        {
            return false;
        }
        try
        {
            byte[] signatureBytes = Convert.FromBase64String(signature);
            using RSA rsa = RSA.Create();
            rsa.ImportParameters(parameters);
            return rsa.VerifyData(BuildMessage(worldIdentity, challenge), signatureBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        }
        catch (Exception ex) when (ex is FormatException or CryptographicException)
        {
            return false;
        }
    }
}
