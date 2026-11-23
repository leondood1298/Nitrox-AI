using Nitrox.Server.Subnautica.Models.GameLogic;
using Nitrox.Server.Subnautica.Models.Packets.Core;

namespace Nitrox.Server.Subnautica.Models.Packets.Processors;

internal sealed class StoryGoalExecutedProcessor(IPacketSender packetSender, StoryManager storyManager, StoryScheduler storyScheduler, PdaManager pdaManager, ILogger<StoryGoalExecutedProcessor> logger)
    : IAuthPacketProcessor<StoryGoalExecuted>
{
    private readonly IPacketSender packetSender = packetSender;
    private readonly StoryManager storyManager = storyManager;
    private readonly StoryScheduler storyScheduler = storyScheduler;
    private readonly PdaManager pdaManager = pdaManager;
    private readonly ILogger<StoryGoalExecutedProcessor> logger = logger;

    public async Task Process(AuthProcessorContext context, StoryGoalExecuted packet)
    {
        logger.ZLogDebug($"Processing packet: {packet}");
        // The switch is structure is similar to StoryGoal.Execute()
        bool added = storyManager.AddCompletedStory(packet.Key);
        if (added)
        {
            storyManager.OnGoalCompleted(packet.Key);
        }
        switch (packet.Type)
        {
            case StoryGoalExecuted.EventType.RADIO:
                if (added)
                {
                    storyManager.QueueRadioStory(packet.Key);
                }
                break;
            case StoryGoalExecuted.EventType.PDA:
                if (packet.Timestamp.HasValue)
                {
                    pdaManager.AddPDALogEntry(new(packet.Key, packet.Timestamp.Value));
                }
                break;
        }
        await storyScheduler.UnscheduleStory(packet.Key);
        // The others got the goal when the server first had it complete, and a joiner gets it with the snapshot. Executed
        // again, it does nothing in their games: StoryGoal.Execute queues a radio message only when the goal is new, and
        // adding a log or encyclopedia entry that is there already does nothing.
        if (added)
        {
            await context.SendToOthersAsync(packet);
        }
    }
}
