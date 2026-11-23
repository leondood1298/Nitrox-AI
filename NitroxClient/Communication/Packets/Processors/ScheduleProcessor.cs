using System.Linq;
using Nitrox.Model.Subnautica.Packets;
using NitroxClient.Communication.Packets.Processors.Core;
using NitroxClient.GameLogic;
using Story;

namespace NitroxClient.Communication.Packets.Processors;

internal sealed class ScheduleProcessor : IClientPacketProcessor<Schedule>
{
    public Task Process(ClientProcessorContext context, Schedule schedulePacket)
    {
        if (!StoryManager.IsStoryLoaded())
        {
            return Task.CompletedTask;
        }

        ScheduledGoal goal = new()
        {
            goalKey = schedulePacket.Key,
            goalType = (Story.GoalType)schedulePacket.Type,
            timeExecute = schedulePacket.TimeExecute
        };
        if (ShouldSchedule(goal))
        {
            StoryGoalScheduler.main.schedule.Add(goal);
        }
        Log.Debug($"Processed a Schedule packet [Key: {goal.goalKey}, Type: {goal.goalType}, TimeExecute: {goal.timeExecute}]");
        return Task.CompletedTask;
    }

    private bool ShouldSchedule(ScheduledGoal goal)
    {
        // The sender's game picked this Sunbeam line from its player's position, this game schedules its own pick
        return goal.timeExecute >= DayNightCycle.main.timePassedAsDouble && !IsAlreadyKnown(goal.goalKey) && !StoryManager.IsSunbeamDestroyEventPickedByPosition(goal.goalKey);
    }

    private bool IsAlreadyKnown(string goalKey)
    {
        return StoryGoalScheduler.main.schedule.Any(g => g.goalKey == goalKey) || //  Scheduled
               StoryGoalManager.main.completedGoals.Contains(goalKey); // Completed
    }
}
