using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Server.Subnautica.Models.GameLogic;
using Nitrox.Server.Subnautica.Models.GameLogic.Entities;
using Nitrox.Server.Subnautica.Models.Packets.Core;

namespace Nitrox.Server.Subnautica.Models.Packets.Processors;

/// <summary>
///     A vehicle leaves a docking bay in the game of the player who took it (that player asked for its lock first). See
///     <see cref="VehicleDockingAuthority" />.
/// </summary>
internal sealed class VehicleUndockingProcessor(EntityRegistry entityRegistry, SimulationOwnershipData simulationOwnershipData, ILogger<VehicleUndockingProcessor> logger)
    : IAuthPacketProcessor<VehicleUndocking>
{
    private readonly EntityRegistry entityRegistry = entityRegistry;
    private readonly SimulationOwnershipData simulationOwnershipData = simulationOwnershipData;
    private readonly ILogger<VehicleUndockingProcessor> logger = logger;

    public async Task Process(AuthProcessorContext context, VehicleUndocking packet)
    {
        if (!entityRegistry.TryGetEntityById(packet.VehicleId, out Entity vehicleEntity))
        {
            logger.ZLogError($"Unable to find vehicle to undock {packet.VehicleId}");
            return;
        }

        bool senderSimulates = simulationOwnershipData.GetPlayerForLock(packet.VehicleId) == context.Sender;
        if (packet.UndockingStart)
        {
            entityRegistry.TryGetEntityById(packet.DockId, out Entity dockEntity);
            if (!VehicleDockingAuthority.MayUndock(vehicleEntity, dockEntity, senderSimulates))
            {
                logger.ZLogWarningOnce($"Player {context.Sender.Name} undocked vehicle {packet.VehicleId} from {packet.DockId} without simulating it or while it is not docked there");
                return;
            }
            entityRegistry.RemoveFromParent(vehicleEntity);
        }
        else if (!senderSimulates)
        {
            return;
        }

        // The session is the sender's, whatever the packet said
        await context.SendToOthersAsync(new VehicleUndocking(packet.VehicleId, packet.DockId, context.Sender.SessionId, packet.UndockingStart));
    }
}
