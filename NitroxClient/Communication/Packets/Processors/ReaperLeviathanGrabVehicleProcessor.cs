using Nitrox.Model.Subnautica.Packets;
using NitroxClient.Communication.Packets.Processors.Core;
using NitroxClient.GameLogic;
using NitroxClient.MonoBehaviours;

namespace NitroxClient.Communication.Packets.Processors;

internal sealed class ReaperLeviathanGrabVehicleProcessor(TimeManager timeManager) : IClientPacketProcessor<ReaperLeviathanGrabVehicle>
{
    private readonly TimeManager timeManager = timeManager;

    public Task Process(ClientProcessorContext context, ReaperLeviathanGrabVehicle packet)
    {
        if (!NitroxEntity.TryGetComponentFrom(packet.ReaperLeviathanId, out ReaperLeviathan reaperLeviathan) ||
            !NitroxEntity.TryGetComponentFrom(packet.VehicleId, out Vehicle vehicle))
        {
            return Task.CompletedTask;
        }

        double releaseDelay = packet.RealReleaseTime - timeManager.RealTimeElapsed;
        // if the release is supposed to have already occurred, don't start the grab animation
        if (releaseDelay <= 0d)
        {
            return Task.CompletedTask;
        }

        using (PacketSuppressor<ReaperLeviathanGrabVehicle>.Suppress())
        {
            reaperLeviathan.GrabVehicle(vehicle, packet.VehicleType);
            reaperLeviathan.CancelInvoke(nameof(ReaperLeviathan.DamageVehicle));
            reaperLeviathan.CancelInvoke(nameof(ReaperLeviathan.ReleaseVehicle));
            reaperLeviathan.Invoke(nameof(ReaperLeviathan.ReleaseVehicle), (float)releaseDelay);
        }
        return Task.CompletedTask;
    }
}
