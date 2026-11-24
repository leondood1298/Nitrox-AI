using Nitrox.Model.Subnautica.Packets;
using NitroxClient.Communication.Packets.Processors.Core;
using NitroxClient.GameLogic;

namespace NitroxClient.Communication.Packets.Processors;

internal sealed class VehicleEnergyDrainedProcessor(LavaLarvae lavaLarvae) : IClientPacketProcessor<VehicleEnergyDrained>
{
    private readonly LavaLarvae lavaLarvae = lavaLarvae;

    public Task Process(ClientProcessorContext context, VehicleEnergyDrained packet)
    {
        lavaLarvae.ApplyDrain(packet);
        return Task.CompletedTask;
    }
}
