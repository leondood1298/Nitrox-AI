using System.Collections;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities;
using NitroxClient.GameLogic.Spawning.Abstract;
using NitroxClient.GameLogic.Spawning.Metadata;
using NitroxClient.MonoBehaviours;
using UnityEngine;

namespace NitroxClient.GameLogic.Spawning;

/// <summary>
///     Gives a Lifepod power cell's PowerSource the id of its entity. The cell is part of the pod's scene object, so it
///     is found by its place among the pod's RegeneratePowerSource components.
/// </summary>
public class EscapePodPowerCellEntitySpawner(EntityMetadataManager entityMetadataManager) : SyncEntitySpawner<EscapePodPowerCellEntity>
{
    private readonly EntityMetadataManager entityMetadataManager = entityMetadataManager;
    private bool missingCellReported;

    protected override IEnumerator SpawnAsync(EscapePodPowerCellEntity entity, TaskResult<Optional<GameObject>> result)
    {
        SpawnSync(entity, result);
        yield break;
    }

    protected override bool SpawnSync(EscapePodPowerCellEntity entity, TaskResult<Optional<GameObject>> result)
    {
        result.Set(Optional.Empty);
        if (!NitroxEntity.TryGetObjectFrom(entity.ParentId, out GameObject pod))
        {
            Log.Error($"[{nameof(EscapePodPowerCellEntitySpawner)}] The escape pod {entity.ParentId} of power cell {entity.Id} is not there");
            return true;
        }

        RegeneratePowerSource[] cells = pod.GetComponentsInChildren<RegeneratePowerSource>(true);
        PowerSource powerSource = entity.CellIndex >= 0 && entity.CellIndex < cells.Length ? cells[entity.CellIndex].powerSource : null;
        if (!powerSource)
        {
            // The cell then behaves as in the game for this player alone
            if (!missingCellReported)
            {
                missingCellReported = true;
                Log.Warn($"[{nameof(EscapePodPowerCellEntitySpawner)}] The escape pod has no power cell {entity.CellIndex} (it has {cells.Length}); it is not synchronized");
            }
            return true;
        }

        NitroxEntity.SetNewId(powerSource.gameObject, entity.Id);
        // At once, not only when the whole batch has spawned: from now on the cell has an id, and the player simulating
        // it reports its level within a second (PowerSource_UpdateConnectionCallback_Patch). That must be the stored
        // level, not the prefab's. A cell without stored state keeps the prefab's level, which is then reported.
        entityMetadataManager.ApplyMetadata(powerSource.gameObject, entity.Metadata, spawnContinues: true);
        result.Set(Optional.Of(powerSource.gameObject));
        return true;
    }

    protected override bool SpawnsOwnChildren(EscapePodPowerCellEntity entity) => false;
}
