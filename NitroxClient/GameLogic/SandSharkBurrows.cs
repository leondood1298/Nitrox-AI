using Nitrox.Model.DataStructures;
using Nitrox.Model.DataStructures.Unity;
using Nitrox.Model.Subnautica.Packets;
using NitroxClient.Communication.Abstract;
using NitroxClient.MonoBehaviours;
using UnityEngine;
using static Nitrox.Model.Subnautica.Packets.SandSharkBurrowChanged;

namespace NitroxClient.GameLogic;

/// <summary>
///     Shows every player a Sand Shark going under the sand and bursting out of it. Only the simulating player's game runs
///     BurryInSand.Perform, which hides the shark: it holds the shark still (kinematic) where it lies, covers it with sand
///     and plays the burrow sound. The other games' copies follow its movement but stayed a shark lying on the sand, and
///     their own BurryInSand.Update kept them in the swimming pose. They now do what the simulating game did, each with
///     its own sound, so that everybody hears it once.
/// </summary>
/// <remarks>
///     A player who loads the shark while it lies buried sees it lying on the sand until it next goes under.
/// </remarks>
public sealed class SandSharkBurrows(IPacketSender packetSender, SimulationOwnership simulationOwnership)
{
    private readonly IPacketSender packetSender = packetSender;
    private readonly SimulationOwnership simulationOwnership = simulationOwnership;

    public void CoverStarted(BurryInSand burryInSand) => Broadcast(burryInSand, BurrowState.COVERING);

    public void Buried(BurryInSand burryInSand) => Broadcast(burryInSand, BurrowState.BURIED);

    public void DugUp(BurryInSand burryInSand) => Broadcast(burryInSand, BurrowState.DUG_UP);

    public void Apply(SandSharkBurrowChanged burrow)
    {
        if (!ShouldApply(burrow.CreatureId) || !NitroxEntity.TryGetComponentFrom(burrow.CreatureId, out BurryInSand burryInSand))
        {
            return;
        }
        // Sent before the simulating player learned that the shark died
        LiveMixin liveMixin = burryInSand.GetComponent<LiveMixin>();
        if (liveMixin && !liveMixin.IsAlive())
        {
            return;
        }

        Rigidbody rigidbody = burryInSand.GetComponent<Rigidbody>();
        switch (burrow.State)
        {
            case BurrowState.COVERING:
                burryInSand.burryPosition = burrow.Position.ToUnity();
                burryInSand.floorAlign = burrow.Rotation.ToUnity();
                // Like BurryInSand.Perform on its last stretch: without it, BurryInSand.Update would dig the shark up at once
                if (rigidbody)
                {
                    rigidbody.isKinematic = true;
                }
                if (!burryInSand.coverInSandEffectStarted)
                {
                    burryInSand.TriggerSandCoverEffect();
                }
                break;
            case BurrowState.BURIED:
                if (rigidbody)
                {
                    rigidbody.isKinematic = true;
                }
                burryInSand.burryPosition = burrow.Position.ToUnity();
                burryInSand.floorAlign = burrow.Rotation.ToUnity();
                burryInSand.transform.rotation = burryInSand.floorAlign;
                burryInSand.Burry();
                break;
            case BurrowState.DUG_UP:
                // BurryInSand.Update digs a shark up once it moves freely again; this copy then follows the shark's movement
                burryInSand.burried = false;
                burryInSand.coverInSandEffectStarted = false;
                if (rigidbody)
                {
                    rigidbody.isKinematic = false;
                }
                break;
        }
    }

    /// <summary>
    ///     Only the simulating player's game decides when the shark burrows.
    /// </summary>
    internal bool ShouldApply(NitroxId creatureId)
    {
        return !simulationOwnership.HasAnyLockType(creatureId);
    }

    internal void Broadcast(NitroxId creatureId, BurrowState state, NitroxVector3 position, NitroxQuaternion rotation)
    {
        // The other games replay what they receive through the same methods
        if (simulationOwnership.HasAnyLockType(creatureId))
        {
            packetSender.Send(new SandSharkBurrowChanged(creatureId, state, position, rotation));
        }
    }

    private void Broadcast(BurryInSand burryInSand, BurrowState state)
    {
        if (burryInSand.TryGetNitroxId(out NitroxId creatureId))
        {
            Broadcast(creatureId, state, burryInSand.burryPosition.ToDto(), burryInSand.floorAlign.ToDto());
        }
    }
}
