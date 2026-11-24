using System;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace NitroxClient.GameLogic.PlayerLogic.PlayerPreferences;

/// <summary>
///     The key a server ties the player's name to. It is derived from one secret this game installation makes once and
///     keeps with its other preferences, and from the identity of the world the server runs (sent in the session
///     policy). So the key is the same whatever address the server is reached through, and no server learns the key the
///     player uses on another world.
/// </summary>
public static class PlayerKeys
{
    private const string SECRET_PREF_KEY_NAME = "NITROX_PLAYER_SECRET";
    private const int SECRET_LENGTH = 32;

    /// <param name="worldIdentity">The identity the server sent in its session policy.</param>
    public static string GetKeyFor(string worldIdentity)
    {
        return Derive(GetOrCreateSecret(), worldIdentity);
    }

    /// <summary>
    ///     The key for one world. Unity-free, so that it can be tested.
    /// </summary>
    public static string Derive(byte[] secret, string worldIdentity)
    {
        using HMACSHA256 hmac = new(secret);
        byte[] key = hmac.ComputeHash(Encoding.UTF8.GetBytes(worldIdentity ?? string.Empty));
        return Convert.ToBase64String(key);
    }

    private static byte[] GetOrCreateSecret()
    {
        string stored = PlayerPrefs.GetString(SECRET_PREF_KEY_NAME, string.Empty);
        if (!string.IsNullOrEmpty(stored))
        {
            try
            {
                byte[] secret = Convert.FromBase64String(stored);
                if (secret.Length == SECRET_LENGTH)
                {
                    return secret;
                }
            }
            catch (FormatException)
            {
                // Made anew below
            }
            Log.Warn("The stored player secret is unreadable, making a new one. Servers this name joined before will refuse it until an admin runs forgetkey.");
        }

        byte[] newSecret = new byte[SECRET_LENGTH];
        using (RandomNumberGenerator random = RandomNumberGenerator.Create())
        {
            random.GetBytes(newSecret);
        }
        PlayerPrefs.SetString(SECRET_PREF_KEY_NAME, Convert.ToBase64String(newSecret));
        PlayerPrefs.Save();
        return newSecret;
    }
}
