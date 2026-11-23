using Nitrox.Server.Subnautica.Models.GameLogic;
using Nitrox.Server.Subnautica.Models.Packets.Core;

namespace Nitrox.Server.Subnautica.Models.Packets.Processors;

/// <summary>
///     Every connected game takes back what followed from the Aurora's explosion when the Aurora is restored, and reports
///     it. The server takes the report of the player who restored it.
/// </summary>
internal sealed class AuroraFollowUpGoalsClearedProcessor(StoryManager storyManager, StoryScheduler storyScheduler, ILogger<AuroraFollowUpGoalsClearedProcessor> logger)
    : IAuthPacketProcessor<AuroraFollowUpGoalsCleared>
{
    private readonly StoryManager storyManager = storyManager;
    private readonly StoryScheduler storyScheduler = storyScheduler;
    private readonly ILogger<AuroraFollowUpGoalsClearedProcessor> logger = logger;

    public async Task Process(AuthProcessorContext context, AuroraFollowUpGoalsCleared packet)
    {
        if (await storyManager.ClearAuroraFollowUpsAsync(context.Sender.SessionId, packet.GoalKeys ?? [], storyScheduler))
        {
            logger.ZLogInformation($"Took back {packet.GoalKeys?.Count ?? 0} goals that followed from the Aurora's explosion");
        }
    }
}
