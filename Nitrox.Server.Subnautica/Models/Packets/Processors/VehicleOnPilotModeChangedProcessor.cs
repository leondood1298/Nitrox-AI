using Nitrox.Server.Subnautica.Models.GameLogic;
using Nitrox.Server.Subnautica.Models.GameLogic.Entities;
using Nitrox.Server.Subnautica.Models.Packets.Core;

namespace Nitrox.Server.Subnautica.Models.Packets.Processors;

internal sealed class VehicleOnPilotModeChangedProcessor(VehicleAuthority vehicleAuthority, VehicleDiagnostics diagnostics,
    ILogger<VehicleOnPilotModeChangedProcessor> logger) : IAuthPacketProcessor<VehicleOnPilotModeChanged>
{
    public async Task Process(AuthProcessorContext context, VehicleOnPilotModeChanged packet)
    {
        if (context.Sender.PlayerContext == null)
        {
            diagnostics.RecordAction(false);
            logger.ZLogWarning($"[Vehicle] rejected pilot transition for {packet.VehicleId} from {context.Sender.Name} #{context.Sender.SessionId}: player context is unavailable");
            return;
        }

        if (!vehicleAuthority.TryValidatePilotChange(context.Sender, packet.VehicleId, packet.IsPiloting, out _, out string rejectionReason))
        {
            diagnostics.RecordAction(false);
            logger.ZLogWarning($"[Vehicle] rejected pilot transition for {packet.VehicleId} from {context.Sender.Name} #{context.Sender.SessionId}: {rejectionReason}");
            return;
        }

        context.Sender.PlayerContext.DrivingVehicle = packet.IsPiloting ? packet.VehicleId : null;
        diagnostics.RecordAction(true);
        if (diagnostics.TraceEnabled)
        {
            logger.ZLogInformation($"[Vehicle] accepted pilot={packet.IsPiloting} for {packet.VehicleId} from {context.Sender.Name} #{context.Sender.SessionId}");
        }

        await context.SendToOthersAsync(new VehicleOnPilotModeChanged(packet.VehicleId, context.Sender.SessionId, packet.IsPiloting));
    }
}
