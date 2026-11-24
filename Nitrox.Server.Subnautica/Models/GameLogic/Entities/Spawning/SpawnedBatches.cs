using System.Collections.Generic;
using System.Linq;
using Nitrox.Model.DataStructures;

namespace Nitrox.Server.Subnautica.Models.GameLogic.Entities.Spawning;

/// <summary>
///     The batches of the world whose natural content was made and handed to the registry. A batch is listed only once
///     its entities are registered (see <see cref="WorldEntityManager" />), so a save never lists a batch without its
///     entities.
/// </summary>
internal sealed class SpawnedBatches
{
    private readonly Lock gate = new();
    private readonly HashSet<NitroxInt3> spawned = [];

    /// <summary>
    ///     Batches that came out empty. They are not saved: made again after a restart, they come out empty again (each
    ///     batch has its own random), so listing them would only make the save larger.
    /// </summary>
    private readonly HashSet<NitroxInt3> empty = [];

    public bool Contains(NitroxInt3 batchId)
    {
        lock (gate)
        {
            return spawned.Contains(batchId);
        }
    }

    public void Add(NitroxInt3 batchId, bool hasEntities)
    {
        lock (gate)
        {
            spawned.Add(batchId);
            if (!hasEntities)
            {
                empty.Add(batchId);
            }
        }
    }

    public List<NitroxInt3> ToSave()
    {
        lock (gate)
        {
            return [.. spawned.Where(batchId => !empty.Contains(batchId))];
        }
    }

    public void Load(IEnumerable<NitroxInt3> batchIds)
    {
        lock (gate)
        {
            spawned.Clear();
            empty.Clear();
            foreach (NitroxInt3 batchId in batchIds)
            {
                spawned.Add(batchId);
            }
        }
    }
}
