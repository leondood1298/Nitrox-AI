using System;
using System.Collections.Generic;

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
    ///     Applies the server's queue at initial sync. Messages that arrived by packet during the loading screen are
    ///     already in the queue: each message is kept once, the server's order first.
    /// </summary>
    public static void Merge(List<string> pendingMessages, IEnumerable<string> serverQueue)
    {
        List<string> merged = [];
        HashSet<string> added = new(StringComparer.OrdinalIgnoreCase);
        foreach (string key in serverQueue)
        {
            if (added.Add(key))
            {
                merged.Add(key);
            }
        }
        foreach (string key in pendingMessages)
        {
            if (added.Add(key))
            {
                merged.Add(key);
            }
        }
        pendingMessages.Clear();
        pendingMessages.AddRange(merged);
    }
}
