using System;
using System.Collections.Generic;
using System.Linq;

namespace NitroxClient.GameLogic;

/// <summary>
///     Keeps StoryGoalManager.pendingRadioMessages in step with the server's radio queue.
/// </summary>
public static class PendingRadioMessages
{
    /// <summary>
    ///     Puts a message at the head of the queue so that StoryGoalManager.ExecutePendingRadioMessage plays it.
    /// </summary>
    /// <returns>False if the message is not in the queue.</returns>
    public static bool TryMoveToFront(List<string> pendingMessages, string messageKey)
    {
        int index = pendingMessages.FindIndex(key => string.Equals(key, messageKey, StringComparison.OrdinalIgnoreCase));
        if (index < 0)
        {
            return false;
        }
        if (index > 0)
        {
            string key = pendingMessages[index];
            pendingMessages.RemoveAt(index);
            pendingMessages.Insert(0, key);
        }
        return true;
    }

    /// <summary>
    ///     The goal StoryGoalManager.ExecutePendingRadioMessage completes when it plays a message.
    /// </summary>
    public static string GetPlayedGoal(string messageKey) => $"OnPlay{messageKey}";

    /// <summary>
    ///     Applies the server's queue at initial sync. Messages that arrived by packet during the loading screen are
    ///     already in the queue: each message is kept once, the server's order first. A message another player played
    ///     meanwhile is left out, whether the snapshot or a packet said so: the snapshot can be older than the packets.
    /// </summary>
    /// <param name="isGoalComplete">Whether a goal is complete in the snapshot or in this game.</param>
    public static void Merge(List<string> pendingMessages, IEnumerable<string> serverQueue, Func<string, bool> isGoalComplete)
    {
        List<string> merged = [];
        HashSet<string> added = new(StringComparer.OrdinalIgnoreCase);
        foreach (string key in serverQueue.Concat(pendingMessages))
        {
            if (!isGoalComplete(GetPlayedGoal(key)) && added.Add(key))
            {
                merged.Add(key);
            }
        }
        pendingMessages.Clear();
        pendingMessages.AddRange(merged);
    }
}
