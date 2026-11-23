using Nitrox.Server.Subnautica.Models.GameLogic;
using Nitrox.Server.Subnautica.Models.Packets.Core;

namespace Nitrox.Server.Subnautica.Models.Packets.Processors;

internal sealed class TimeCapsuleCollectedProcessor(CollectedTimeCapsules collectedTimeCapsules) : IAuthPacketProcessor<TimeCapsuleCollected>
{
    private readonly CollectedTimeCapsules collectedTimeCapsules = collectedTimeCapsules;

    public async Task Process(AuthProcessorContext context, TimeCapsuleCollected packet)
    {
        // Two capsules in the world can hold the same content, which has one encyclopedia entry
        if (collectedTimeCapsules.Add(packet.Capsule))
        {
            await context.SendToOthersAsync(packet);
        }
    }
}
