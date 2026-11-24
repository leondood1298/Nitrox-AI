using System;
using System.Collections.Generic;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;
using Nitrox.Model.Subnautica.Packets;
using NitroxClient.Communication.Abstract;
using NitroxClient.GameLogic.FMOD;
using NitroxClient.MonoBehaviours;
using UnityEngine;

namespace NitroxClient.GameLogic;

/// <summary>
///     Lava Larvae on vehicles. A larva attaches itself and drains the vehicle in the game that simulates the larva
///     (AttachToVehicle.Perform), on that game's copy of the vehicle, which need not be the vehicle's real one.
///     <list type="bullet">
///         <item>The drain reaches the game simulating the vehicle once (<see cref="VehicleEnergyDrained" />), which
///         takes it from the vehicle's cells; that game reports their charge to the others.</item>
///         <item>Being attached, to which vehicle and at which point, is the larva's state (<see cref="LavaLarvaMetadata" />),
///         kept by the server for players who load the larva or the vehicle later, in whichever order they arrive. The
///         other games take the larva's point on their copy of the vehicle, which the vehicle's pilot feels as the larvae
///         slowing it down, and show its attacking pose. The game simulating the larva has it attached for real, so that it
///         goes on draining.</item>
///         <item>Any player knocks a larva off: a hit on a copy of the larva reaches its simulating game (creature damage),
///         where the game detaches it (AttachToVehicle.OnTakeDamage).</item>
///     </list>
/// </summary>
/// <remarks>Unity-bound.</remarks>
public sealed class LavaLarvae
{
    /// <summary>The drain of a larva is a little energy every frame; it is sent at most this often per vehicle.</summary>
    private const float DRAIN_SEND_INTERVAL = 1f;

    private const string ATTACKING_PARAMETER = "attacking";

    private readonly IPacketSender packetSender;
    private readonly SimulationOwnership simulationOwnership;
    private readonly Dictionary<NitroxId, (float Amount, float FirstDrainTime)> pendingDrains = [];

    /// <summary>Larvae loaded attached to a vehicle that is not loaded yet, by vehicle, with their point.</summary>
    private readonly Dictionary<NitroxId, Dictionary<NitroxId, int>> waitingForVehicle = [];

    /// <summary>True while this game applies a larva's stored state: nothing is sent and the attach sound is not played.</summary>
    private bool applyingStoredState;

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
        if (applyingStoredState)
        {
            return;
        }
        LavaLarvaTarget target = attachToVehicle.currTarget;
        LavaLarvaAttachPoint point = attachToVehicle.currentAttachPoint;
        if (!target || !point || !attachToVehicle.TryGetNitroxId(out NitroxId larvaId) || !simulationOwnership.HasAnyLockType(larvaId) ||
            !TryGetVehicleId(target, out NitroxId vehicleId))
        {
            return;
        }
        int pointIndex = Array.IndexOf(target.attachPoints, point);
        if (pointIndex < 0)
        {
            return;
        }
        // A point this game had shown the larva at (its stored state) before it attached for real somewhere else
        ReleaseOtherPoints(target, point, attachToVehicle.gameObject);
        Send(larvaId, new LavaLarvaMetadata(true, vehicleId, pointIndex, CreatureMetadata.UNDECIDED));
    }

    /// <summary>
    ///     Called before AttachToVehicle.SetDetached, which forgets the vehicle. Told by whichever game had the larva
    ///     attached, also one whose simulation of the larva just went to another player (see <see cref="ReleaseIfSimulatedElsewhere" />).
    /// </summary>
    public void Detaching(AttachToVehicle attachToVehicle)
    {
        LavaLarvaAttachPoint point = attachToVehicle.currentAttachPoint;
        if (!applyingStoredState && point && point.attached && attachToVehicle.TryGetNitroxId(out NitroxId larvaId))
        {
            Send(larvaId, new LavaLarvaMetadata(false, null, -1, CreatureMetadata.UNDECIDED));
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
    ///     A larva's stored state, as it spawns or as another game changed it.
    /// </summary>
    public void ApplyStoredState(GameObject larvaObject, LavaLarvaMetadata state)
    {
        if (!larvaObject.TryGetNitroxId(out NitroxId larvaId))
        {
            return;
        }
        ForgetWaiting(larvaId);
        ReleaseShownPoints(larvaObject);

        if (!state.Attached || state.VehicleId == null)
        {
            ShowPose(larvaObject, false);
            return;
        }
        if (!NitroxEntity.TryGetObjectFrom(state.VehicleId, out GameObject vehicleObject))
        {
            // The vehicle may come later; a larva of a vehicle that is gone for good just waits
            if (!waitingForVehicle.TryGetValue(state.VehicleId, out Dictionary<NitroxId, int> larvae))
            {
                waitingForVehicle[state.VehicleId] = larvae = [];
            }
            larvae[larvaId] = state.AttachPoint;
            return;
        }
        Attach(larvaObject, larvaId, vehicleObject, state.AttachPoint);
    }

    /// <summary>
    ///     Called once a vehicle or Cyclops spawned here: larvae loaded before it go on it now.
    /// </summary>
    public void VehicleSpawned(NitroxId vehicleId, GameObject vehicleObject)
    {
        if (!waitingForVehicle.TryGetValue(vehicleId, out Dictionary<NitroxId, int> larvae))
        {
            return;
        }
        waitingForVehicle.Remove(vehicleId);
        foreach (KeyValuePair<NitroxId, int> larva in larvae)
        {
            if (NitroxEntity.TryGetObjectFrom(larva.Key, out GameObject larvaObject))
            {
                Attach(larvaObject, larva.Key, vehicleObject, larva.Value);
            }
        }
    }

    private void Attach(GameObject larvaObject, NitroxId larvaId, GameObject vehicleObject, int pointIndex)
    {
        LavaLarvaTarget target = vehicleObject.GetComponentInChildren<LavaLarvaTarget>(true);
        if (!target || target.attachPoints == null || pointIndex < 0 || pointIndex >= target.attachPoints.Length)
        {
            return;
        }
        LavaLarvaAttachPoint point = target.attachPoints[pointIndex];
        point.occupied = true;
        point.attached = true;
        point.lavaLarva = larvaObject;

        AttachToVehicle attachToVehicle = larvaObject.GetComponentInChildren<AttachToVehicle>(true);
        if (attachToVehicle && simulationOwnership.HasAnyLockType(larvaId))
        {
            AttachForReal(attachToVehicle, target, point);
            return;
        }

        ShowPose(larvaObject, true);
        // As AttachToVehicle.SetAttached tells a Cyclops (its alarm)
        if (target.IsCyclops() && target.subControl)
        {
            target.subControl.BroadcastMessage("AttachedLavaLarva", larvaObject, SendMessageOptions.DontRequireReceiver);
        }
    }

    /// <summary>
    ///     The game simulating the larva has it attached as the game would have it after AttachToVehicle.Perform reached the
    ///     point, without the attach sound, and as its current action, so that it goes on draining instead of starting over.
    /// </summary>
    private void AttachForReal(AttachToVehicle attachToVehicle, LavaLarvaTarget target, LavaLarvaAttachPoint point)
    {
        Transform larva = attachToVehicle.transform;
        attachToVehicle.currTarget = target;
        attachToVehicle.currentAttachPoint = point;
        attachToVehicle.startPos = larva.position;
        attachToVehicle.startRot = larva.rotation;
        // The next Update puts it at the point at once
        attachToVehicle.timeTargetSet = Time.time - attachToVehicle.transitionTime;
        attachToVehicle.state = AttachToVehicle.State.Transition;

        FMOD_StudioEventEmitter attachSound = attachToVehicle.attachSound;
        attachToVehicle.attachSound = null;
        applyingStoredState = true;
        try
        {
            using (FMODSystem.SuppressSendingSounds())
            {
                attachToVehicle.SetAttached();
            }
        }
        finally
        {
            applyingStoredState = false;
            attachToVehicle.attachSound = attachSound;
        }
        if (attachToVehicle.creature)
        {
            attachToVehicle.creature.prevBestAction = attachToVehicle;
        }
    }

    private static void ShowPose(GameObject larvaObject, bool attached)
    {
        if (larvaObject.TryGetComponent(out Creature creature))
        {
            SafeAnimator.SetBool(creature.GetAnimator(), ATTACKING_PARAMETER, attached);
        }
    }

    /// <summary>
    ///     Frees the points this game showed the larva at; a larva attached for real here comes off through the game.
    /// </summary>
    private void ReleaseShownPoints(GameObject larvaObject)
    {
        AttachToVehicle attachToVehicle = larvaObject.GetComponentInChildren<AttachToVehicle>(true);
        if (attachToVehicle && attachToVehicle.IsAttached())
        {
            applyingStoredState = true;
            try
            {
                attachToVehicle.SetDetached();
            }
            finally
            {
                applyingStoredState = false;
            }
        }
        foreach (LavaLarvaAttachPoint point in UnityEngine.Object.FindObjectsOfType<LavaLarvaAttachPoint>())
        {
            if (point.lavaLarva == larvaObject)
            {
                LavaLarvaTarget target = point.GetComponentInParent<LavaLarvaTarget>();
                point.Clear();
                if (target && target.IsCyclops() && target.subControl)
                {
                    target.subControl.BroadcastMessage("DetachedLavaLarva", larvaObject, SendMessageOptions.DontRequireReceiver);
                }
            }
        }
    }

    private static void ReleaseOtherPoints(LavaLarvaTarget target, LavaLarvaAttachPoint keptPoint, GameObject larvaObject)
    {
        foreach (LavaLarvaAttachPoint point in target.attachPoints)
        {
            if (point != keptPoint && point.lavaLarva == larvaObject)
            {
                point.Clear();
            }
        }
    }

    private void ForgetWaiting(NitroxId larvaId)
    {
        foreach (Dictionary<NitroxId, int> larvae in waitingForVehicle.Values)
        {
            larvae.Remove(larvaId);
        }
    }

    private void Send(NitroxId larvaId, LavaLarvaMetadata state)
    {
        // The infection, leash and size are not this update's: the server keeps the stored ones (CreatureMetadataAuthority)
        packetSender.Send(new EntityMetadataUpdate(larvaId, state));
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
