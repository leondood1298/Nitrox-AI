using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.Packets;
using NitroxClient.Communication.Abstract;
using NitroxClient.MonoBehaviours;
using UnityEngine;

namespace NitroxClient.GameLogic;

/// <summary>
///     A repulsion cannon pushes what it hits in the game of the player who shoots. What another player simulates is moved
///     in that player's game, which would put it back where it was: the shot is handed to that game (see
///     <see cref="EntityRepulsed" />).
/// </summary>
public class Repulsions
{
    private readonly IPacketSender packetSender;
    private readonly SimulationOwnership simulationOwnership;

    public Repulsions(IPacketSender packetSender, SimulationOwnership simulationOwnership)
    {
        this.packetSender = packetSender;
        this.simulationOwnership = simulationOwnership;
    }

    /// <summary>
    ///     Called after this player's repulsion cannon shot an object.
    /// </summary>
    public void Shot(Rigidbody rigidbody, Vector3 velocity)
    {
        if (!rigidbody)
        {
            return;
        }
        NitroxEntity entity = rigidbody.GetComponentInParent<NitroxEntity>();
        if (!entity || simulationOwnership.HasAnyLockType(entity.Id))
        {
            return;
        }
        packetSender.Send(new EntityRepulsed(entity.Id, velocity.ToDto()));
    }

    /// <summary>
    ///     Another player's repulsion cannon shot an object this game simulates.
    /// </summary>
    public void Repulsed(NitroxId id, Vector3 velocity)
    {
        if (!simulationOwnership.HasAnyLockType(id) || !NitroxEntity.TryGetObjectFrom(id, out GameObject gameObject) || !gameObject.TryGetComponent(out Rigidbody rigidbody))
        {
            return;
        }
        // As RepulsionCannon.ShootObject does
        UWE.Utils.SetIsKinematicAndUpdateInterpolation(rigidbody, false);
        rigidbody.velocity = velocity;
    }
}
