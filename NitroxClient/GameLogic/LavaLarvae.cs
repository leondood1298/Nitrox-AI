using System;
using System.Collections.Generic;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.Packets;
using NitroxClient.Communication.Abstract;
using NitroxClient.MonoBehaviours;
using UnityEngine;

namespace NitroxClient.GameLogic;

/// <summary>
///     Lava Larvae on vehicles. A larva attaches itself and drains the vehicle in the game that simulates the larva
///     (AttachToVehicle.Perform), on that game's copy of the vehicle, which need not be the vehicle's real one.
///     <list type="bullet">
///         <item>The drain reaches the game simulating the vehicle once (<see cref="VehicleEnergyDrained" />), which takes
///         it from the vehicle's cells; that game reports their charge to the others.</item>
///         <item>The other games show the larva attached (<see cref="LavaLarvaAttachChanged" />): its point is taken on
///         their copy of the vehicle, which the vehicle's pilot feels as the larvae slowing it down.</item>
///         <item>Any player knocks a larva off: a hit on a copy of the larva reaches its simulating game (creature damage),
///         where the game detaches it (AttachToVehicle.OnTakeDamage).</item>
///     </list>
/// </summary>
/// <remarks>Unity-bound.</remarks>
public sealed class LavaLarvae
{
    /// <summary>The drain of a larva is a little energy every frame; it is sent at most this often per vehicle.</summary>
    private const float DRAIN_SEND_INTERVAL = 1f;

    private readonly IPacketSender packetSender;
    private readonly SimulationOwnership simulationOwnership;
    private readonly Dictionary<NitroxId, (float Amount, float FirstDrainTime)> pendingDrains = [];

    public LavaLarvae(IPacketSender packetSender, SimulationOwnership simulationOwnership)
    {
        this.packetSender = packetSender;
        this.simulationOwnership = simulationOwnership;
    }

    /// <summary>
    ///     Called before LavaLarvaTarget.ConsumeEnergy, i.e. a larva of this game drains a vehicle.
    /// </summary>
    /// <returns>True if the vehicle is drained here; false if its drain was passed to the game simulating it.</returns>
    public bool DrainsHere(LavaLarvaTarget target, float amount)
    {
        if (!TryGetVehicleId(target, out NitroxId vehicleId) || simulationOwnership.HasAnyLockType(vehicleId))
        {
            return true;
        }

        float now = Time.time;
        if (!pendingDrains.TryGetValue(vehicleId, out (float Amount, float FirstDrainTime) pending))
        {
            pending = (0f, now);
        }
        pending.Amount += amount;
        if (now - pending.FirstDrainTime >= DRAIN_SEND_INTERVAL)
        {
            packetSender.Send(new VehicleEnergyDrained(vehicleId, pending.Amount));
            pendingDrains.Remove(vehicleId);
        }
        else
        {
            pendingDrains[vehicleId] = pending;
        }
        return false;
    }

    /// <summary>
    ///     A drain passed on by the game simulating a larva, to be taken here, where the vehicle is simulated.
    /// </summary>
    public void ApplyDrain(VehicleEnergyDrained drain)
    {
        if (!NitroxEntity.TryGetObjectFrom(drain.VehicleId, out GameObject vehicleObject) || !simulationOwnership.HasAnyLockType(drain.VehicleId))
        {
            return;
        }
        LavaLarvaTarget target = vehicleObject.GetComponentInChildren<LavaLarvaTarget>(true);
        if (target)
        {
            // The game's own way, which knows a Cyclops from a vehicle; not passed on again since the vehicle is simulated here
            target.ConsumeEnergy(drain.Amount);
        }
    }

    /// <summary>
    ///     Called after AttachToVehicle.SetAttached: in the game simulating the larva, it took its point.
    /// </summary>
    public void Attached(AttachToVehicle attachToVehicle)
    {
        BroadcastAttachment(attachToVehicle, true);
    }

    /// <summary>
    ///     Called before AttachToVehicle.SetDetached, which forgets the vehicle.
    /// </summary>
    public void Detaching(AttachToVehicle attachToVehicle)
    {
        LavaLarvaAttachPoint point = attachToVehicle.currentAttachPoint;
        if (point && point.attached)
        {
            BroadcastAttachment(attachToVehicle, false);
        }
    }

    /// <summary>
    ///     Called before AttachToVehicle.Update. A larva attached in this game whose simulation went to another player (who
    ///     grabbed it with a propulsion cannon, for example) lets go here: that player's game moves it now.
    /// </summary>
    public void ReleaseIfSimulatedElsewhere(AttachToVehicle attachToVehicle)
    {
        if (attachToVehicle.IsAttached() && attachToVehicle.TryGetNitroxId(out NitroxId larvaId) && !simulationOwnership.HasAnyLockType(larvaId))
        {
            attachToVehicle.SetDetached();
        }
    }

    /// <summary>
    ///     Another game's larva attached to or came off a vehicle: this game's copy is shown so, and the point counted.
    /// </summary>
    public void ApplyAttachment(LavaLarvaAttachChanged change)
    {
        if (!NitroxEntity.TryGetObjectFrom(change.LarvaId, out GameObject larvaObject) || !NitroxEntity.TryGetObjectFrom(change.VehicleId, out GameObject vehicleObject))
        {
            return;
        }
        LavaLarvaTarget target = vehicleObject.GetComponentInChildren<LavaLarvaTarget>(true);
        if (!target || target.attachPoints == null || change.AttachPoint < 0 || change.AttachPoint >= target.attachPoints.Length)
        {
            return;
        }

        LavaLarvaAttachPoint point = target.attachPoints[change.AttachPoint];
        if (change.Attached)
        {
            point.occupied = true;
            point.attached = true;
            point.lavaLarva = larvaObject;
        }
        else if (point.lavaLarva == larvaObject || !point.lavaLarva)
        {
            point.Clear();
        }

        if (larvaObject.TryGetComponent(out Creature creature))
        {
            SafeAnimator.SetBool(creature.GetAnimator(), "attacking", change.Attached);
        }
        // As AttachToVehicle.SetAttached and SetDetached tell a Cyclops (its alarm)
        if (target.IsCyclops() && target.subControl)
        {
            target.subControl.BroadcastMessage(change.Attached ? "AttachedLavaLarva" : "DetachedLavaLarva", larvaObject, SendMessageOptions.DontRequireReceiver);
        }
    }

    private void BroadcastAttachment(AttachToVehicle attachToVehicle, bool attached)
    {
        LavaLarvaTarget target = attachToVehicle.currTarget;
        LavaLarvaAttachPoint point = attachToVehicle.currentAttachPoint;
        // Only a game simulating the larva attaches it. Coming off is told by whichever game had it attached, also one whose
        // simulation of the larva just went to another player (see ReleaseIfSimulatedElsewhere)
        if (!target || !point || !attachToVehicle.TryGetNitroxId(out NitroxId larvaId) || (attached && !simulationOwnership.HasAnyLockType(larvaId)) ||
            !TryGetVehicleId(target, out NitroxId vehicleId))
        {
            return;
        }
        int pointIndex = Array.IndexOf(target.attachPoints, point);
        if (pointIndex >= 0)
        {
            packetSender.Send(new LavaLarvaAttachChanged(larvaId, vehicleId, pointIndex, attached));
        }
    }

    private static bool TryGetVehicleId(LavaLarvaTarget target, out NitroxId vehicleId)
    {
        if (target.vehicle)
        {
            return target.vehicle.TryGetNitroxId(out vehicleId);
        }
        if (target.TryGetComponentInParent(out SubRoot subRoot, true))
        {
            return subRoot.TryGetNitroxId(out vehicleId);
        }
        vehicleId = null;
        return false;
    }
}
