using System;
using System.Collections;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Nitrox.Model.MultiplayerSession;
using UnityEngine;

namespace NitroxClient.GameLogic.PlayerLogic.PlayerPreferences;

/// <summary>
///     The key pair a server ties the player's name to (see <see cref="PlayerIdentityProof" />). This game installation
///     makes one pair per world identity and keeps it with its other preferences, so the key is the same whatever address
///     the server is reached through, and no server learns anything usable on another world: only the public half and a
///     signature over its own challenge ever leave the game.
/// </summary>
public static class PlayerKeys
{
    private const string KEY_PREF_PREFIX = "NITROX_PLAYER_KEY_";

    public sealed class Proof
    {
        public string PublicKey { get; }
        public string Signature { get; }

        public Proof(string publicKey, string signature)
        {
            PublicKey = publicKey;
            Signature = signature;
        }
    }

    /// <summary>
    ///     Signs <paramref name="challenge" /> for <paramref name="worldIdentity" /> with this installation's key for that
    ///     world, making the key the first time.
    /// </summary>
    /// <remarks>
    ///     Making an RSA key with the game's Mono runtime takes from a fraction of a second to a few seconds, so the work
    ///     runs off the main thread; only the preferences, which Unity allows on the main thread alone, are read and written
    ///     here. <paramref name="done" /> gets null if the key could not be made.
    /// </remarks>
    public static IEnumerator ProveAsync(string worldIdentity, string challenge, Action<Proof> done)
    {
        string prefName = GetPrefName(worldIdentity);
        string stored = PlayerPrefs.GetString(prefName, string.Empty);
        Task<(string PrivateKeyXml, Proof Proof)> work = Task.Run(() => Prove(stored, worldIdentity, challenge));
        while (!work.IsCompleted)
        {
            yield return null;
        }
        if (work.IsFaulted || work.IsCanceled)
        {
            Log.Error(work.Exception, "Could not make or use the player key");
            done(null);
            yield break;
        }
        if (work.Result.PrivateKeyXml != stored)
        {
            if (!string.IsNullOrEmpty(stored))
            {
                Log.Warn("The stored player key for this world is unreadable, made a new one. The server will refuse the name until an admin runs forgetkey.");
            }
            PlayerPrefs.SetString(prefName, work.Result.PrivateKeyXml);
            PlayerPrefs.Save();
        }
        done(work.Result.Proof);
    }

    /// <summary>
    ///     Unity-free, so that it can be tested.
    /// </summary>
    /// <param name="storedPrivateKeyXml">The key kept for this world, or empty to make one.</param>
    /// <returns>The key to keep (a new one if none was stored or it was unreadable) and the proof.</returns>
    public static (string PrivateKeyXml, Proof Proof) Prove(string storedPrivateKeyXml, string worldIdentity, string challenge)
    {
        using RSA rsa = LoadOrCreate(storedPrivateKeyXml, out string privateKeyXml);
        byte[] signature = rsa.SignData(PlayerIdentityProof.BuildMessage(worldIdentity, challenge), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return (privateKeyXml, new Proof(PlayerIdentityProof.FormatPublicKey(rsa.ExportParameters(false)), Convert.ToBase64String(signature)));
    }

    private static RSA LoadOrCreate(string storedPrivateKeyXml, out string privateKeyXml)
    {
        if (!string.IsNullOrEmpty(storedPrivateKeyXml))
        {
            RSA stored = CreateRsa();
            try
            {
                stored.FromXmlString(storedPrivateKeyXml);
                if (stored.KeySize == PlayerIdentityProof.KEY_SIZE_BITS)
                {
                    privateKeyXml = storedPrivateKeyXml;
                    return stored;
                }
            }
            catch (Exception)
            {
                // Unreadable (the runtimes throw different exception types for bad text): made anew below
            }
            stored.Dispose();
        }

        RSA created = CreateRsa();
        privateKeyXml = created.ToXmlString(true);
        return created;
    }

    private static RSA CreateRsa()
    {
#if NET
        return RSA.Create(PlayerIdentityProof.KEY_SIZE_BITS);
#else
        // Mono's RSA.Create() has no size; this constructor makes the key on first use and keeps it in no key store
        return new RSACryptoServiceProvider(PlayerIdentityProof.KEY_SIZE_BITS);
#endif
    }

    /// <summary>
    ///     A preference name for the world: the identity is the server's text, so only a hash of it goes into the name.
    /// </summary>
    private static string GetPrefName(string worldIdentity)
    {
        using SHA256 sha = SHA256.Create();
        byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(worldIdentity ?? string.Empty));
        StringBuilder name = new(KEY_PREF_PREFIX);
        for (int i = 0; i < 12; i++)
        {
            name.Append(hash[i].ToString("X2"));
        }
        return name.ToString();
    }
}
