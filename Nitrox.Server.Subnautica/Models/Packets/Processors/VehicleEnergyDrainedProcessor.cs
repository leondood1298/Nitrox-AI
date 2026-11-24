using Nitrox.Model.Subnautica.Packets;
using Nitrox.Server.Subnautica.Models.GameLogic;
using Nitrox.Server.Subnautica.Models.Packets.Core;

namespace Nitrox.Server.Subnautica.Models.Packets.Processors;

/// <summary>
///     Hands energy drained from a vehicle in one game to the one game whose copy of the vehicle is real: the player
///     simulating it. Nobody else is told, so the energy is taken once.
/// </summary>
internal sealed class VehicleEnergyDrainedProcessor(SimulationOwnershipData simulationOwnershipData) : IAuthPacketProcessor<VehicleEnergyDrained>
{
    private readonly SimulationOwnershipData simulationOwnershipData = simulationOwnershipData;

    public async Task Process(AuthProcessorContext context, VehicleEnergyDrained packet)
    {
        if (packet.Amount <= 0f || float.IsNaN(packet.Amount))
        {
            return;
        }
        Player? simulator = simulationOwnershipData.GetPlayerForLock(packet.VehicleId);
        // Nobody simulates it: its cells change nowhere. The sender drained its own copy only if it simulated the vehicle.
        if (simulator != null && simulator != context.Sender)
        {
            await context.SendAsync(packet, simulator.SessionId);
        }
    }
}
