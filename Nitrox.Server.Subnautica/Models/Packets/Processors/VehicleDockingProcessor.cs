using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities;
using Nitrox.Server.Subnautica.Models.GameLogic;
using Nitrox.Server.Subnautica.Models.GameLogic.Entities;
using Nitrox.Server.Subnautica.Models.Packets.Core;

namespace Nitrox.Server.Subnautica.Models.Packets.Processors;

internal sealed class VehicleDockingProcessor(EntityRegistry entityRegistry, VehicleAuthority vehicleAuthority,
    VehicleDiagnostics diagnostics, ILogger<VehicleDockingProcessor> logger) : IAuthPacketProcessor<VehicleDocking>
{
    public async Task Process(AuthProcessorContext context, VehicleDocking packet)
    {
        if (!vehicleAuthority.TryValidateDocking(context.Sender, packet.VehicleId, packet.DockId,
                                                 out VehicleEntity vehicle, out Entity dock, out string rejectionReason))
        {
            diagnostics.RecordAction(false);
            logger.ZLogWarning($"[Vehicle] rejected docking {packet.VehicleId} -> {packet.DockId} from {context.Sender.Name} #{context.Sender.SessionId}: {rejectionReason}");
            return;
        }

        entityRegistry.ReparentEntity(vehicle, dock);
        diagnostics.RecordAction(true);
        if (diagnostics.TraceEnabled)
        {
            logger.ZLogInformation($"[Vehicle] accepted docking {packet.VehicleId} -> {packet.DockId} from {context.Sender.Name} #{context.Sender.SessionId}");
        }

        await context.SendToOthersAsync(new VehicleDocking(packet.VehicleId, packet.DockId, context.Sender.SessionId));
    }
}
