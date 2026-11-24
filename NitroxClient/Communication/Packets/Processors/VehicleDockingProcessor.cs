using System.Collections;
using Nitrox.Model.Core;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.Packets;
using NitroxClient.Communication.Packets.Processors.Core;
using NitroxClient.GameLogic;
using NitroxClient.MonoBehaviours;
using NitroxClient.MonoBehaviours.Vehicles;
using NitroxClient.Unity.Helper;
using UnityEngine;

namespace NitroxClient.Communication.Packets.Processors;

/// <summary>
///     Another player's vehicle docked. Also replayed after loading, when the snapshot may already show the vehicle docked
///     (nothing to do) or the vehicle may have been undocked meanwhile (the undocking cancels the docking still played).
/// </summary>
internal sealed class VehicleDockingProcessor(Vehicles vehicles) : IClientPacketProcessor<VehicleDocking>
{
    private readonly Vehicles vehicles = vehicles;

    public Task Process(ClientProcessorContext context, VehicleDocking packet)
    {
        if (!NitroxEntity.TryGetComponentFrom(packet.VehicleId, out Vehicle vehicle))
        {
            Log.Error($"[{nameof(VehicleDockingProcessor)}] could not find Vehicle component on {packet.VehicleId}");
            return Task.CompletedTask;
        }

        if (!NitroxEntity.TryGetComponentFrom(packet.DockId, out VehicleDockingBay dockingBay))
        {
            Log.Error($"[{nameof(VehicleDockingProcessor)}] could not find VehicleDockingBay component on {packet.DockId}");
            return Task.CompletedTask;
        }

        if (dockingBay.GetDockedVehicle() == vehicle)
        {
            // Already docked there, e.g. loaded so from the snapshot this packet was held for
            vehicles.SetOnPilotMode(packet.VehicleId, packet.SessionId, false);
            return Task.CompletedTask;
        }

        int dockingNumber = vehicles.BeginRemoteDocking(vehicle, dockingBay);
        VehicleMovementReplicator vehicleMovementReplicator = vehicle.GetComponent<VehicleMovementReplicator>();
        vehicle.StartCoroutine(DelayAnimationAndDisablePiloting(vehicle, vehicleMovementReplicator, dockingBay, packet.VehicleId, packet.SessionId, dockingNumber));
        return Task.CompletedTask;
    }

    private IEnumerator DelayAnimationAndDisablePiloting(Vehicle vehicle, VehicleMovementReplicator vehicleMovementReplicator, VehicleDockingBay vehicleDockingBay, NitroxId vehicleId,
                                                         SessionId sessionId, int dockingNumber)
    {
        // Consider the vehicle movement latency (we don't teleport the vehicle to the docking position)
        if (vehicleMovementReplicator)
        {
            // NB: We don't have a lifetime ahead of us
            float waitTime = Mathf.Clamp(vehicleMovementReplicator.MaxAllowedLatency, 0f, 2f);
            yield return new WaitForSeconds(waitTime);
        }
        else
        {
            yield return Yielders.WaitFor1Second;
        }

        // Undocked meanwhile (e.g. both were held during loading), or the bay was destroyed
        if (!vehicles.IsRemoteDockingCurrent(vehicle, dockingNumber) || !vehicleDockingBay)
        {
            yield break;
        }
        vehicles.EndRemoteDocking(vehicle);

        // DockVehicle sets the rigid body kinematic of the vehicle to true, we don't want that behaviour
        // Therefore disable kinematic (again) to remove the bouncing behavior
        Vehicles.DockWithoutPlayer(vehicleDockingBay, vehicle);
        vehicle.useRigidbody.isKinematic = false;

        yield return Yielders.WaitFor2Seconds;
        vehicles.SetOnPilotMode(vehicleId, sessionId, false);
    }
}
