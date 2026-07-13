using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities;
using Nitrox.Server.Subnautica.Models.GameLogic;
using Nitrox.Server.Subnautica.Models.GameLogic.Entities;
using Nitrox.Server.Subnautica.Models.Packets.Core;

namespace Nitrox.Server.Subnautica.Models.Packets.Processors;

internal sealed class VehicleUndockingProcessor(EntityRegistry entityRegistry, VehicleAuthority vehicleAuthority,
    VehicleDiagnostics diagnostics, ILogger<VehicleUndockingProcessor> logger) : IAuthPacketProcessor<VehicleUndocking>
{
    public async Task Process(AuthProcessorContext context, VehicleUndocking packet)
    {
        if (packet.UndockingStart)
        {
            if (!vehicleAuthority.TryValidateUndocking(context.Sender, packet.VehicleId, packet.DockId,
                                                       out VehicleEntity vehicle, out Entity _, out string rejectionReason))
            {
                Reject(context, packet, rejectionReason);
                return;
            }

            vehicleAuthority.MarkUndockingStarted(context.Sender, packet.VehicleId, packet.DockId);
            entityRegistry.RemoveFromParent(vehicle);
        }
        else if (!vehicleAuthority.TryValidateUndockingCompletion(context.Sender, packet.VehicleId, packet.DockId, out string rejectionReason))
        {
            Reject(context, packet, rejectionReason);
            return;
        }

        diagnostics.RecordAction(true);
        if (diagnostics.TraceEnabled)
        {
            logger.ZLogInformation($"[Vehicle] accepted undocking start={packet.UndockingStart} for {packet.VehicleId} from {context.Sender.Name} #{context.Sender.SessionId}");
        }

        await context.SendToOthersAsync(new VehicleUndocking(packet.VehicleId, packet.DockId, context.Sender.SessionId, packet.UndockingStart));
    }

    private void Reject(AuthProcessorContext context, VehicleUndocking packet, string rejectionReason)
    {
        diagnostics.RecordAction(false);
        logger.ZLogWarning($"[Vehicle] rejected undocking start={packet.UndockingStart} for {packet.VehicleId} from {context.Sender.Name} #{context.Sender.SessionId}: {rejectionReason}");
    }
}
