using Nitrox.Model.Subnautica.Packets;
using NitroxClient.Communication.Packets.Processors.Core;
using NitroxClient.GameLogic;

namespace NitroxClient.Communication.Packets.Processors;

internal sealed class TimeCapsuleCollectedProcessor(TimeCapsules timeCapsules) : IClientPacketProcessor<TimeCapsuleCollected>
{
    private readonly TimeCapsules timeCapsules = timeCapsules;

    public Task Process(ClientProcessorContext context, TimeCapsuleCollected packet)
    {
        // Announced like the game announces it to the player who collected it
        timeCapsules.AddCollected(packet.Capsule, true);
        return Task.CompletedTask;
    }
}
