using System.Collections.Generic;
using System.Linq;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;

namespace Nitrox.Server.Subnautica.Models.GameLogic;

/// <summary>
///     The time capsules found in the world and collected, with the content shown in their encyclopedia entries. In the
///     game that content lives in TimeCapsuleContentProvider, which only the collecting player's game fills from the
///     online service.
/// </summary>
internal sealed class CollectedTimeCapsules
{
    private readonly Dictionary<string, CollectedTimeCapsule> capsulesById = [];
    private readonly Lock locker = new();

    /// <returns>True if no capsule with this id was collected before.</returns>
    public bool Add(CollectedTimeCapsule? capsule)
    {
        if (capsule == null || string.IsNullOrWhiteSpace(capsule.Id))
        {
            return false;
        }

        lock (locker)
        {
            return capsulesById.TryAdd(capsule.Id, capsule);
        }
    }

    public List<CollectedTimeCapsule> GetAll()
    {
        lock (locker)
        {
            return capsulesById.Values.OrderBy(capsule => capsule.Id, StringComparer.Ordinal).ToList();
        }
    }

    public void Load(IEnumerable<CollectedTimeCapsule>? saved)
    {
        lock (locker)
        {
            capsulesById.Clear();
            foreach (CollectedTimeCapsule capsule in saved ?? [])
            {
                if (capsule != null && !string.IsNullOrWhiteSpace(capsule.Id))
                {
                    capsulesById.TryAdd(capsule.Id, capsule);
                }
            }
        }
    }
}
