using NitroxClient.GameLogic.PlayerLogic;
using UnityEngine;

namespace NitroxClient.GameLogic;

/// <summary>
///     The predators in the Primary Containment Facility's aquarium swim at the player who stands at the glass
///     (PrisonPredatorSwimToPlayer). The game only looks at its own player, so on the game of the player simulating them they
///     ignored every other player. They now go for the nearest player who can be attacked, the way the game picks the
///     player; the target is kept in the creature's <see cref="LastTarget" />, which is sent to the others with the rest of
///     its aim. Taken from upstream pull request 2255. Unity-bound.
/// </summary>
public sealed class PrisonPredators
{
    private static readonly EcoRegion.TargetFilter isTargetValidFilter = IsTargetValid;

    /// <summary>
    ///     Stands in for PrisonPredatorSwimToPlayer.Evaluate.
    /// </summary>
    public float Evaluate(PrisonPredatorSwimToPlayer action, Creature creature, float time)
    {
        if (time < action.timeStopSwim + action.pauseInterval || time < action.timeStartSwim + action.maxDuration || EcoRegionManager.main == null)
        {
            return 0f;
        }

        IEcoTarget ecoTarget = EcoRegionManager.main.FindNearestTarget(RemotePlayer.PLAYER_ECO_TARGET_TYPE, action.transform.position, isTargetValidFilter, 2);
        GameObject target = ecoTarget != null ? ecoTarget.GetGameObject() : null;
        if (!target)
        {
            return 0f;
        }

        float distance = Vector3.Distance(target.transform.position, action.transform.position);
        if (distance > action.maxDistance || distance < action.minDistance || !creature.GetCanSeeObject(target))
        {
            return 0f;
        }

        if (creature.TryGetComponent(out LastTarget lastTarget))
        {
            lastTarget.SetLockedTarget(target);
        }
        return action.GetEvaluatePriority();
    }

    /// <summary>
    ///     Stands in for PrisonPredatorSwimToPlayer.Perform: swims at the player Evaluate chose.
    /// </summary>
    public void Perform(PrisonPredatorSwimToPlayer action, Creature creature, float time)
    {
        if (!creature.TryGetComponent(out LastTarget lastTarget) || !lastTarget.target)
        {
            return;
        }
        if (time > action.timeNextSwim)
        {
            action.timeNextSwim = time + action.swimInterval;
            Transform targetTransform = lastTarget.target.transform;
            // The game faces it away from the camera of its own player; the direction towards the target does the same for anyone
            Vector3 direction = (targetTransform.position - action.transform.position).normalized;
            action.swimBehaviour.SwimTo(targetTransform.position, direction, action.swimVelocity);
        }
        lastTarget.UnlockTarget();
    }

    private static bool IsTargetValid(IEcoTarget ecoTarget)
    {
        GameObject target = ecoTarget.GetGameObject();
        if (!target)
        {
            return false;
        }
        if (Player.main && target == Player.main.gameObject)
        {
            return Player.main.CanBeAttacked();
        }
        return !target.TryGetComponent(out RemotePlayerIdentifier remotePlayerIdentifier) || remotePlayerIdentifier.RemotePlayer.CanBeAttacked();
    }
}
