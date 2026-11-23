using Nitrox.Server.Subnautica.Models.Packets.Core;

namespace Nitrox.Server.Subnautica.Models.Packets.Processors;

internal sealed class GoalCompletedProcessor : IAuthPacketProcessor<GoalCompleted>
{
    public Task Process(AuthProcessorContext context, GoalCompleted packet)
    {
        // A client can report a goal again (e.g. after the restore at initial sync), the first completion time is kept
        context.Sender.PersonalCompletedGoalsWithTimestamp.TryAdd(packet.CompletedGoal, packet.CompletionTime);
        return Task.CompletedTask;
    }
}
