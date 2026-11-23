using System;
using System.Collections.Generic;
using System.Linq;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;

namespace NitroxClient.GameLogic;

/// <summary>
///     Rebuilds the Sunbeam's countdown and shoot-down line of a joining player from the server's schedule. The game
///     keeps both in its own save (StoryGoalCustomEventHandler), which a joining player's game does not load.
/// </summary>
public static class SunbeamTimeline
{
    /// <summary>
    ///     The goal whose completion starts the countdown (StoryGoalCustomEventHandler.NotifyGoalComplete).
    /// </summary>
    public const string COUNTDOWN_TRIGGER = "OnPlayRadioSunbeam4";

    /// <summary>
    ///     The goal that ends the countdown and makes each game pick its shoot-down line from its player's position.
    /// </summary>
    public const string RANGE_CHECK_GOAL = "SunbeamCheckPlayerRange";

    /// <summary>
    ///     How long after the countdown starts the countdown display reaches zero (StoryGoalCustomEventHandler.endTime).
    /// </summary>
    public const float COUNTDOWN_DURATION = 2400f;

    /// <summary>
    ///     When the countdown started, from any goal of the countdown that is still scheduled. Each goal of the Sunbeam
    ///     story schedules the next one with its delay (StoryGoalCustomEventHandler.sunbeamGoals): its time minus the
    ///     delays back to the start of the countdown is the start.
    /// </summary>
    /// <param name="steps">The game's sunbeamGoals: the goal that triggers a step, the step's goal and its delay.</param>
    internal static bool TryGetCountdownStart(IEnumerable<(string Trigger, string Key, float Delay)> steps, IEnumerable<NitroxScheduledGoal> scheduled, out float countdownStart)
    {
        Dictionary<string, float> offsets = GetOffsetsFromCountdownStart(steps.ToList());
        foreach (NitroxScheduledGoal goal in scheduled)
        {
            if (offsets.TryGetValue(goal.GoalKey, out float offset))
            {
                countdownStart = goal.TimeExecute - offset;
                return true;
            }
        }
        countdownStart = 0;
        return false;
    }

    /// <summary>
    ///     When this game's own shoot-down line is due, for a player who joins after the other games picked theirs. The
    ///     range check that picks a line ran before this game joined, so it never picks one; the moment of that check is
    ///     known from any pick still scheduled.
    /// </summary>
    /// <param name="ownKey">The line this game picks from its player's position now.</param>
    /// <param name="ownDelay">How long after the range check the game plays <paramref name="ownKey" />.</param>
    /// <param name="otherKey">The line for the other position.</param>
    /// <param name="otherDelay">How long after the range check the game plays <paramref name="otherKey" />.</param>
    /// <param name="now">The game time now.</param>
    /// <returns>False when no pick is scheduled, or when this game's line would have played already.</returns>
    internal static bool TryGetOwnDestroyLineTime(string ownKey, float ownDelay, string otherKey, float otherDelay, IEnumerable<NitroxScheduledGoal> scheduled, double now, out float timeExecute)
    {
        List<NitroxScheduledGoal> picks = scheduled.ToList();
        NitroxScheduledGoal ownPick = picks.Find(goal => string.Equals(goal.GoalKey, ownKey, StringComparison.OrdinalIgnoreCase));
        NitroxScheduledGoal otherPick = picks.Find(goal => string.Equals(goal.GoalKey, otherKey, StringComparison.OrdinalIgnoreCase));
        if (ownPick != null)
        {
            timeExecute = ownPick.TimeExecute;
        }
        else if (otherPick != null)
        {
            timeExecute = otherPick.TimeExecute - otherDelay + ownDelay;
        }
        else
        {
            timeExecute = 0;
            return false;
        }
        return timeExecute > now;
    }

    /// <summary>
    ///     How long after the start of the countdown each of its goals is due, up to the range check that ends it.
    /// </summary>
    private static Dictionary<string, float> GetOffsetsFromCountdownStart(List<(string Trigger, string Key, float Delay)> steps)
    {
        Dictionary<string, float> offsets = new(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, float> reached = new(StringComparer.OrdinalIgnoreCase) { [COUNTDOWN_TRIGGER] = 0 };
        Queue<string> toFollow = new([COUNTDOWN_TRIGGER]);
        while (toFollow.Count > 0)
        {
            string trigger = toFollow.Dequeue();
            // The goals after the range check belong to the shoot-down, not to the countdown
            if (string.Equals(trigger, RANGE_CHECK_GOAL, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            foreach ((string stepTrigger, string key, float delay) in steps)
            {
                if (string.Equals(stepTrigger, trigger, StringComparison.OrdinalIgnoreCase) && !reached.ContainsKey(key))
                {
                    reached[key] = offsets[key] = reached[trigger] + delay;
                    toFollow.Enqueue(key);
                }
            }
        }
        return offsets;
    }
}
