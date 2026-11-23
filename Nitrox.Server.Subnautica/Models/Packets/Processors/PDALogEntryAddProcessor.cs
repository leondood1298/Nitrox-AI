using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Server.Subnautica.Models.GameLogic;
using Nitrox.Server.Subnautica.Models.Packets.Core;

namespace Nitrox.Server.Subnautica.Models.Packets.Processors;

internal sealed class PDALogEntryAddProcessor(PdaManager pdaManager, StoryScheduler storyScheduler, PdaNotificationManager pdaNotificationManager) : IAuthPacketProcessor<PDALogEntryAdd>
{
    private readonly PdaManager pdaManager = pdaManager;
    private readonly StoryScheduler storyScheduler = storyScheduler;
    private readonly PdaNotificationManager pdaNotificationManager = pdaNotificationManager;

    public async Task Process(AuthProcessorContext context, PDALogEntryAdd packet)
    {
        pdaManager.AddPDALogEntry(new PDALogEntry(packet.Key, packet.Timestamp));
        pdaNotificationManager.LogEntryAdded(packet.Key);
        if (storyScheduler.ContainsScheduledStory(packet.Key))
        {
            await storyScheduler.UnscheduleStory(packet.Key);
        }
        await context.SendToOthersAsync(packet);
    }
}
