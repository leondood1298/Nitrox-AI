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
}
