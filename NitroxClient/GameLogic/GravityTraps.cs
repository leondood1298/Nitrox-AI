using Nitrox.Model.DataStructures;
using NitroxClient.MonoBehaviours;
using UnityEngine;

namespace NitroxClient.GameLogic;

/// <summary>
///     A deployed gravity trap pulls fish and resources toward itself. Every game that has it loaded runs one, and each
///     pulled on its own, so the things it held were in different places for each player. Now only the game that
///     simulates the trap pulls; it takes over what it pulls, so the positions it gives them are sent to everybody.
/// </summary>
public class GravityTraps
{
    private readonly SimulationOwnership simulationOwnership;

    public GravityTraps(SimulationOwnership simulationOwnership)
    {
        this.simulationOwnership = simulationOwnership;
    }

    /// <summary>
    ///     Called before a gravity trap starts to pull what entered its range.
    /// </summary>
    /// <returns>True if this game pulls it.</returns>
    public bool MayAttract(Gravsphere gravsphere, Collider collider)
    {
        if (!Simulates(gravsphere))
        {
            return false;
        }
        Rigidbody rigidbody = collider ? UWE.Utils.GetComponentInHierarchy<Rigidbody>(collider.gameObject) : null;
        if (!rigidbody)
        {
            return true;
        }
        NitroxEntity entity = rigidbody.GetComponentInParent<NitroxEntity>();
        if (!entity || simulationOwnership.HasAnyLockType(entity.Id))
        {
            return true;
        }
        // Held by another player's propulsion cannon or a Stalker they simulate
        if (simulationOwnership.IsHeldExclusivelyByAnotherPlayer(entity.Id))
        {
            return false;
        }
        simulationOwnership.RequestSimulationLock(entity.Id, SimulationLockType.TRANSIENT);
        return true;
    }

    /// <summary>
    ///     Called before a gravity trap pulls. A game that stopped simulating the trap lets go of what it held: the game
    ///     that simulates it now pulls.
    /// </summary>
    public void Pulling(Gravsphere gravsphere)
    {
        if (gravsphere.attractableList.Count > 0 && !Simulates(gravsphere))
        {
            gravsphere.ClearAll();
        }
    }

    private bool Simulates(Gravsphere gravsphere)
    {
        // A trap this game has not heard of from the server yet is this game's own, just thrown
        return !gravsphere.TryGetNitroxId(out NitroxId id) || simulationOwnership.HasAnyLockType(id);
    }
}
