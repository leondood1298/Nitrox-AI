namespace NitroxClient.GameLogic.ChatUI;

/// <summary>
///     What the chat log shows of a line and when a line joins the entry above it.
/// </summary>
public static class ChatLogRules
{
    public const int LINE_CHAR_LIMIT = 255;

    /// <summary>
    ///     One player's lines in a row share an entry, but only up to here: a player who keeps talking must not grow one text
    ///     past what the UI can draw.
    /// </summary>
    public const int MAX_LINES_PER_ENTRY = 20;

    public static string Sanitize(string message)
    {
        message = (message ?? string.Empty).Trim().TrimEnd('\n').Trim();
        return message.Length <= LINE_CHAR_LIMIT ? message : message.Substring(0, LINE_CHAR_LIMIT);
    }

    /// <summary>
    ///     A line joins the entry above when it has the same sender and there is room. The server and a player are never
    ///     the same sender, whatever name the player chose.
    /// </summary>
    public static bool JoinsEntry(ChatLogEntry last, string playerName, bool isServer)
    {
        return last != null &&
               last.IsServer == isServer &&
               last.PlayerName == playerName &&
               last.LineCount < MAX_LINES_PER_ENTRY;
    }
}
