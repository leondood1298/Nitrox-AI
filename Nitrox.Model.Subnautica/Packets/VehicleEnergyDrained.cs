using System;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Packets;

namespace Nitrox.Model.Subnautica.Packets;

/// <summary>
///     Energy something in the sender's game took from a vehicle or Cyclops that another player's game simulates: Lava
///     Larvae attached to it, which drain it in the game simulating the larvae. The server hands it to the player simulating
///     the vehicle, whose game takes it from the vehicle's own cells.
/// </summary>
[Serializable]
public sealed class VehicleEnergyDrained : Packet
{
    public NitroxId VehicleId { get; }
    public float Amount { get; }

    public VehicleEnergyDrained(NitroxId vehicleId, float amount)
    {
        VehicleId = vehicleId;
        Amount = amount;
    }

    public override string ToString()
    {
        return $"[{nameof(VehicleEnergyDrained)} VehicleId: {VehicleId}, Amount: {Amount}]";
    }
}
