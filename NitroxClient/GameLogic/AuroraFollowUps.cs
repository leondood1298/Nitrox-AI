using System;
using System.Collections.Generic;
using System.Linq;

namespace NitroxClient.GameLogic;

/// <summary>
///     The story goals that follow from the Aurora's warnings and explosion. The game names them in its data, not in its
///     code: a compound goal (CompoundGoalTracker) is triggered once all its preconditions are complete, and its own
///     completion can be a precondition of the next. Restoring the Aurora takes the warnings and the explosion back, so
///     what followed from them is taken back too, to happen again after the next explosion.
/// </summary>
public static class AuroraFollowUps
{
    /// <summary>
    ///     The goals that follow from <paramref name="auroraGoals" />, directly or through other followers. Each comes with the
    ///     goal StoryGoalManager.ExecutePendingRadioMessage completes when it is a radio message that was played.
    /// </summary>
    /// <param name="compoundGoals">The game's compound goals (CompoundGoalData.goals): a goal's key and its preconditions.</param>
    /// <returns>The followers, without the Aurora goals themselves.</returns>
    public static List<string> Find(IEnumerable<string> auroraGoals, IEnumerable<(string Key, string[] Preconditions)> compoundGoals)
    {
        List<(string Key, string[] Preconditions)> goals = compoundGoals.ToList();
        HashSet<string> taken = new(StringComparer.OrdinalIgnoreCase);
        foreach (string goal in auroraGoals)
        {
            taken.Add(goal);
            taken.Add(PendingRadioMessages.GetPlayedGoal(goal));
        }

        List<string> followers = [];
        bool found = true;
        while (found)
        {
            found = false;
            foreach ((string key, string[] preconditions) in goals)
            {
                if (!taken.Contains(key) && preconditions != null && preconditions.Any(taken.Contains))
                {
                    taken.Add(key);
                    taken.Add(PendingRadioMessages.GetPlayedGoal(key));
                    followers.Add(key);
                    followers.Add(PendingRadioMessages.GetPlayedGoal(key));
                    found = true;
                }
            }
        }
        return followers;
    }

    /// <summary>
    ///     Whether a compound goal has to wait in the tracker again after <paramref name="takenBack" /> were taken back: the
    ///     tracker drops a compound goal once it triggered, and would never trigger it again.
    /// </summary>
    public static bool MustTrackAgain(string key, string[] preconditions, ICollection<string> takenBack)
    {
        return takenBack.Contains(key) && preconditions != null && preconditions.Any(takenBack.Contains);
    }
}
