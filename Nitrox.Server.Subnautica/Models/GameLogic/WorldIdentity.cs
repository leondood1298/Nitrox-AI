using System.IO;
using System.Security.Cryptography;
using Nitrox.Server.Subnautica.Models.Serialization;

namespace Nitrox.Server.Subnautica.Models.GameLogic;

/// <summary>
///     An identifier this world is given once and keeps in its save folder (so a backup, which copies the folder, keeps
///     it too). Joining games derive the key their player name is tied to from it (see <see cref="PlayerIdentityKey" />).
///     It is sent to every connecting game and is no secret.
/// </summary>
/// <remarks>
///     Kept in a file of its own, like the ban list, instead of in the world data, so that no other part of saving
///     changes. A copied save folder is the same world as far as names are concerned.
/// </remarks>
internal sealed class WorldIdentity
{
    internal const string FILE_NAME = "WorldIdentity.txt";

    private readonly Func<string> saveDirectoryProvider;
    private readonly ILogger<WorldIdentity> logger;
    private readonly Lock valueLock = new();
    private string? value;

    public WorldIdentity(IOptions<ServerStartOptions> startOptions, ILogger<WorldIdentity> logger) : this(() => startOptions.Value.GetServerSavePath(), logger)
    {
    }

    internal WorldIdentity(Func<string> saveDirectoryProvider, ILogger<WorldIdentity> logger)
    {
        this.saveDirectoryProvider = saveDirectoryProvider;
        this.logger = logger;
    }

    /// <summary>
    ///     True if no identifier was stored and this run made one. The keys names were tied to before were derived from
    ///     another identifier (or none), so they can no longer match.
    /// </summary>
    public bool CreatedThisRun
    {
        get
        {
            _ = Value;
            return createdThisRun;
        }
    }

    private bool createdThisRun;

    /// <summary>
    ///     The identifier, read from the save folder or made and written there the first time.
    /// </summary>
    public string Value
    {
        get
        {
            lock (valueLock)
            {
                return value ??= LoadOrCreate();
            }
        }
    }

    private string LoadOrCreate()
    {
        string directory = saveDirectoryProvider();
        string path = Path.Combine(directory, FILE_NAME);
        try
        {
            if (File.Exists(path))
            {
                string stored = File.ReadAllText(path).Trim();
                if (stored.Length > 0)
                {
                    return stored;
                }
                logger.ZLogWarning($"{FILE_NAME} is empty, making a new world identity: player names are tied again on their next join");
            }
        }
        catch (Exception ex)
        {
            logger.ZLogError(ex, $"Could not read {path}, making a new world identity: player names are tied again on their next join");
        }

        createdThisRun = true;
        string created = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
        try
        {
            Directory.CreateDirectory(directory);
            string temporary = $"{path}.tmp";
            File.WriteAllText(temporary, created);
            File.Move(temporary, path, true);
        }
        catch (Exception ex)
        {
            logger.ZLogError(ex, $"Could not write {path}: the world identity lasts only until the server stops");
        }
        return created;
    }
}
