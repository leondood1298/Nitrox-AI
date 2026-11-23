using System;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace NitroxClient.GameLogic.PlayerLogic.PlayerPreferences;

/// <summary>
///     The key a server ties the player's name to. It is derived from one secret this game installation makes once and
///     keeps with its other preferences, and from the server's address, so that a server never learns the key the
///     player uses on another server.
/// </summary>
public static class PlayerKeys
{
    private const string SECRET_PREF_KEY_NAME = "NITROX_PLAYER_SECRET";
    private const int SECRET_LENGTH = 32;

    /// <param name="serverAddress">The address the player typed or picked for the server, as preferences are kept per server.</param>
    public static string GetKeyFor(string serverAddress)
    {
        return Derive(GetOrCreateSecret(), serverAddress);
    }

    /// <summary>
    ///     The key for one server. Unity-free, so that it can be tested.
    /// </summary>
    public static string Derive(byte[] secret, string serverAddress)
    {
        using HMACSHA256 hmac = new(secret);
        byte[] key = hmac.ComputeHash(Encoding.UTF8.GetBytes((serverAddress ?? string.Empty).Trim().ToLowerInvariant()));
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
