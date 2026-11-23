using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Server.Subnautica.Models.GameLogic;
using Nitrox.Server.Subnautica.Models.Packets.Core;

namespace Nitrox.Server.Subnautica.Models.Packets.Processors;

internal sealed class ScheduleProcessor(StoryScheduler storyScheduler) : IAuthPacketProcessor<Schedule>
{
    private readonly StoryScheduler storyScheduler = storyScheduler;

    public async Task Process(AuthProcessorContext context, Schedule packet)
    {
        // A goal the server did not take (already scheduled, already done or in the past) is not passed on either
        if (storyScheduler.ScheduleStory(NitroxScheduledGoal.From(packet.TimeExecute, packet.Key, packet.Type)))
        {
            await context.SendToOthersAsync(packet);
        }
    }
}
