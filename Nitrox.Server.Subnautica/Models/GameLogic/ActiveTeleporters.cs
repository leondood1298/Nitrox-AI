using System.Collections.Generic;
using System.Linq;

namespace Nitrox.Server.Subnautica.Models.GameLogic;

/// <summary>
///     The identifiers of the precursor teleporters that were activated. In the game they live in TeleporterManager, which
///     a teleporter reads when it spawns to know whether it is open; only the teleporters loaded for the activating player
///     hear of an activation.
/// </summary>
internal sealed class ActiveTeleporters
{
    private readonly HashSet<string> identifiers = [];
    private readonly Lock locker = new();

    /// <returns>True if the identifier was not active yet.</returns>
    public bool Add(string identifier)
    {
        if (string.IsNullOrWhiteSpace(identifier))
        {
            return false;
        }

        lock (locker)
        {
            return identifiers.Add(identifier);
        }
    }

    public List<string> GetAll()
    {
        lock (locker)
        {
            return identifiers.Order(StringComparer.Ordinal).ToList();
        }
    }

    public void Load(IEnumerable<string>? saved)
    {
        lock (locker)
        {
            identifiers.Clear();
            foreach (string identifier in saved ?? [])
            {
                if (!string.IsNullOrWhiteSpace(identifier))
                {
                    identifiers.Add(identifier);
                }
            }
        }
    }
}
