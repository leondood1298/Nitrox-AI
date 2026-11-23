using System.Collections;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities;
using NitroxClient.GameLogic.Spawning.Abstract;
using UnityEngine;

namespace NitroxClient.GameLogic.Spawning;

/// <summary>
///     Gives the game's time capsule the id of a rocket's <see cref="PlayerTimeCapsuleEntity" />, so that the items put
///     into it are synced like a locker's and its text arrives as the entity's metadata.
/// </summary>
public class PlayerTimeCapsuleEntitySpawner : SyncEntitySpawner<PlayerTimeCapsuleEntity>
{
    protected override IEnumerator SpawnAsync(PlayerTimeCapsuleEntity entity, TaskResult<Optional<GameObject>> result)
    {
        SpawnSync(entity, result);
        yield break;
    }

    protected override bool SpawnSync(PlayerTimeCapsuleEntity entity, TaskResult<Optional<GameObject>> result)
    {
        result.Set(Optional.OfNullable(TimeCapsules.TakeCapsule(entity.Id)));
        return true;
    }

    protected override bool SpawnsOwnChildren(PlayerTimeCapsuleEntity entity) => false;
}
