using Nitrox.Server.Subnautica.Models.GameLogic;
using Nitrox.Server.Subnautica.Models.Packets.Core;

namespace Nitrox.Server.Subnautica.Models.Packets.Processors;

internal sealed class TimeCapsuleCollectedProcessor(CollectedTimeCapsules collectedTimeCapsules, PdaNotificationManager pdaNotificationManager) : IAuthPacketProcessor<TimeCapsuleCollected>
{
    private readonly CollectedTimeCapsules collectedTimeCapsules = collectedTimeCapsules;
    private readonly PdaNotificationManager pdaNotificationManager = pdaNotificationManager;

    public async Task Process(AuthProcessorContext context, TimeCapsuleCollected packet)
    {
        // Two capsules in the world can hold the same content, which has one encyclopedia entry
        if (collectedTimeCapsules.Add(packet.Capsule))
        {
            // Every game that has the entry adds it verbosely (TimeCapsule.Collect, TimeCapsules.AddCollected)
            pdaNotificationManager.EncyclopediaEntryAdded(packet.Capsule.Id, true);
            await context.SendToOthersAsync(packet);
        }
    }
}
