using Nitrox.Server.Subnautica.Models.Packets.Core;

namespace Nitrox.Server.Subnautica.Models.Packets.Processors;

internal sealed class VehicleOnPilotModeChangedProcessor : IAuthPacketProcessor<VehicleOnPilotModeChanged>
{
    public async Task Process(AuthProcessorContext context, VehicleOnPilotModeChanged packet)
    {
        if (packet.IsPiloting)
        {
            context.Sender.PlayerContext.DrivingVehicle = packet.VehicleId;
        }
        else if (packet.VehicleId.Equals(context.Sender.PlayerContext.DrivingVehicle))
        {
            // Leaving one vehicle does not end piloting another one entered since
            context.Sender.PlayerContext.DrivingVehicle = null;
        }
        // A player only seats or unseats itself, whatever session the packet named
        await context.SendToOthersAsync(new VehicleOnPilotModeChanged(packet.VehicleId, context.Sender.SessionId, packet.IsPiloting));
    }
}
