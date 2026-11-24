using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Server.Subnautica.Models.GameLogic;
using Nitrox.Server.Subnautica.Models.GameLogic.Entities;
using Nitrox.Server.Subnautica.Models.Packets.Core;

namespace Nitrox.Server.Subnautica.Models.Packets.Processors;

/// <summary>
///     A vehicle docked in its simulating player's game. See <see cref="VehicleDockingAuthority" />.
/// </summary>
internal sealed class VehicleDockingProcessor(EntityRegistry entityRegistry, SimulationOwnershipData simulationOwnershipData, ILogger<VehicleDockingProcessor> logger)
    : IAuthPacketProcessor<VehicleDocking>
{
    private readonly EntityRegistry entityRegistry = entityRegistry;
    private readonly SimulationOwnershipData simulationOwnershipData = simulationOwnershipData;
    private readonly ILogger<VehicleDockingProcessor> logger = logger;

    public async Task Process(AuthProcessorContext context, VehicleDocking packet)
    {
        if (!entityRegistry.TryGetEntityById(packet.VehicleId, out Entity vehicleEntity))
        {
            logger.ZLogError($"Unable to find vehicle to dock {packet.VehicleId}");
            return;
        }

        if (!entityRegistry.TryGetEntityById(packet.DockId, out Entity dockEntity))
        {
            logger.ZLogError($"Unable to find dock {packet.DockId} for docking vehicle {packet.VehicleId}");
            return;
        }

        bool senderSimulates = simulationOwnershipData.GetPlayerForLock(packet.VehicleId) == context.Sender;
        switch (VehicleDockingAuthority.MayDock(vehicleEntity, dockEntity, senderSimulates))
        {
            case VehicleDockingAuthority.Decision.IGNORE:
                logger.ZLogWarningOnce($"Player {context.Sender.Name} docked vehicle {packet.VehicleId} which it does not simulate");
                return;
            case VehicleDockingAuthority.Decision.SEND_OUT:
                logger.ZLogInformation($"Vehicle {packet.VehicleId} of {context.Sender.Name} docked into bay {packet.DockId} which holds another vehicle; sending it out");
                await context.ReplyAsync(new VehicleUndocking(packet.VehicleId, packet.DockId, context.Sender.SessionId, false));
                return;
        }

        entityRegistry.ReparentEntity(vehicleEntity, dockEntity);
        // The session is the sender's, whatever the packet said
        await context.SendToOthersAsync(new VehicleDocking(packet.VehicleId, packet.DockId, context.Sender.SessionId));
    }
}
