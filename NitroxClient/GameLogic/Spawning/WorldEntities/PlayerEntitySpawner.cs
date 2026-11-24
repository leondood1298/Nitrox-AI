using System.Collections;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities;
using NitroxClient.GameLogic.PlayerLogic;
using NitroxClient.GameLogic.PlayerLogic.PlayerModel.Abstract;
using NitroxClient.GameLogic.Spawning.Abstract;
using NitroxClient.MonoBehaviours;
using UnityEngine;

namespace NitroxClient.GameLogic.Spawning.WorldEntities;

public class PlayerEntitySpawner(PlayerManager playerManager, ILocalNitroxPlayer localPlayer, Entities entities) : EntitySpawner<PlayerEntity>
{
    private readonly PlayerManager playerManager = playerManager;
    private readonly ILocalNitroxPlayer localPlayer = localPlayer;
    private readonly Entities entities = entities;

    protected override IEnumerator SpawnAsync(PlayerEntity entity, TaskResult<Optional<GameObject>> result)
    {
        if (Player.main.TryGetNitroxId(out NitroxId localPlayerId) && localPlayerId == entity.Id)
        {
            yield return entities.SpawnBatchAsync(entity.ChildEntities, true, true);
            result.Set(Player.main.gameObject);
            yield break;
        }

        Optional<RemotePlayer> remotePlayer = playerManager.Find(entity.Id);
        Optional<GameObject> parent = entity.ParentId != null ? NitroxEntity.GetObjectFrom(entity.ParentId) : Optional.Empty;

        // The server may send us a player entity but they are not guarenteed to be actively connected at the moment - don't spawn them.  In the
        // future, we could make this configurable to be able to spawn disconnected players in the world.
        if (!remotePlayer.HasValue || remotePlayer.Value.Body)
        {
            result.Set(Optional.Empty);
            yield break;
        }

        GameObject remotePlayerBody = CloneLocalPlayerBodyPrototype();
        remotePlayer.Value.InitializeGameObject(remotePlayerBody, localPlayer.BodyPrototype);
        playerManager.BodyCreated(remotePlayer.Value);
        PlaceBody(remotePlayerBody.transform, entity, parent);

        if (parent.HasValue)
        {
            AttachToParent(remotePlayer.Value, parent.Value);
        }

        yield return entities.SpawnBatchAsync(entity.ChildEntities, true, true);

        result.Set(Optional.Of(remotePlayerBody));
    }

    protected override bool SpawnsOwnChildren(PlayerEntity entity) => true;

    private GameObject CloneLocalPlayerBodyPrototype()
    {
        GameObject clone = Object.Instantiate(localPlayer.BodyPrototype, null, false);
        clone.SetActive(true);
        return clone;
    }

    private static void PlaceBody(Transform body, PlayerEntity entity, Optional<GameObject> parent)
    {
        if (entity.Transform == null)
        {
            return;
        }

        bool hasParent = parent.HasValue && parent.Value;
        Transform parentTransform = hasParent ? parent.Value.transform : null;
        bool parentIsBase = hasParent && parentTransform.TryGetComponent(out SubRoot subRoot) && subRoot.isBase;
        (Vector3 position, Quaternion? rotation) = RemoteBodyPlacement.Place(entity.Transform.LocalPosition.ToUnity(), entity.Transform.LocalRotation.ToUnity(), hasParent,
                                                                             hasParent ? parentTransform.position : Vector3.zero,
                                                                             hasParent ? parentTransform.rotation : Quaternion.identity, parentIsBase);
        body.position = position;
        if (rotation.HasValue)
        {
            body.rotation = rotation.Value;
        }
    }

    private void AttachToParent(RemotePlayer remotePlayer, GameObject parent)
    {
        if (parent.TryGetComponent(out SubRoot subRoot))
        {
            Log.Debug($"Found sub root for {remotePlayer.PlayerName}. Will add him and update animation.");
            remotePlayer.SetSubRoot(subRoot);
        }
        else if (parent.TryGetComponent(out EscapePod escapePod))
        {
            Log.Debug($"Found EscapePod for {remotePlayer.PlayerName}.");
            remotePlayer.SetEscapePod(escapePod);
        }
        else
        {
            Log.Error($"Found neither SubRoot component nor EscapePod on {parent.name} for {remotePlayer.PlayerName}.");
        }
    }
}
