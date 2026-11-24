using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Nitrox.Model.Helper;

/// <summary>
///     Builds the command line the game is started with.
/// </summary>
/// <remarks>
///     Only what the game needs is passed on: the player's own launch arguments, the data folder (so the game writes its
///     logs and settings where the launcher does) and the arguments the caller adds. The launcher's own command line is
///     not forwarded: it used to be, joined without quotes, which split paths with spaces.
/// </remarks>
public static class GameLaunchArguments
{
    public static string Build(string? userArguments, string? dataPath, IEnumerable<string>? extraArguments = null)
    {
        List<string> parts = [];
        if (!string.IsNullOrWhiteSpace(userArguments))
        {
            parts.Add(userArguments!.Trim());
        }
        if (!string.IsNullOrWhiteSpace(dataPath))
        {
            parts.Add(NitroxDirectory.DATA_PATH_ARG);
            parts.Add(Quote(dataPath!));
        }
        foreach (string argument in extraArguments ?? [])
        {
            if (!string.IsNullOrWhiteSpace(argument))
            {
                parts.Add(Quote(argument));
            }
        }
        return string.Join(" ", parts);
    }

    /// <summary>
    ///     Quotes an argument if it holds spaces, so it reaches the program as one argument.
    /// </summary>
    public static string Quote(string argument)
    {
        if (argument.Length > 0 && !argument.Any(c => char.IsWhiteSpace(c) || c == '"'))
        {
            return argument;
        }
        // A backslash before the closing quote would escape it, so the path's own trailing backslashes are doubled.
        StringBuilder builder = new("\"");
        int backslashes = 0;
        foreach (char c in argument)
        {
            if (c == '\\')
            {
                backslashes++;
                continue;
            }
            if (c == '"')
            {
                builder.Append('\\', backslashes * 2 + 1).Append('"');
            }
            else
            {
                builder.Append('\\', backslashes).Append(c);
            }
            backslashes = 0;
        }
        builder.Append('\\', backslashes * 2).Append('"');
        return builder.ToString();
    }

    /// <summary>
    ///     Splits a command line into its arguments, keeping quoted parts together and dropping the quotes.
    /// </summary>
    public static List<string> Split(string? commandLine)
    {
        List<string> result = [];
        if (string.IsNullOrWhiteSpace(commandLine))
        {
            return result;
        }
        StringBuilder current = new();
        bool inQuotes = false;
        bool hasArgument = false;
        foreach (char c in commandLine!)
        {
            if (c == '"')
            {
                inQuotes = !inQuotes;
                hasArgument = true;
                continue;
            }
            if (char.IsWhiteSpace(c) && !inQuotes)
            {
                if (hasArgument)
                {
                    result.Add(current.ToString());
                    current.Clear();
                    hasArgument = false;
                }
                continue;
            }
            current.Append(c);
            hasArgument = true;
        }
        if (hasArgument)
        {
            result.Add(current.ToString());
        }
        return result;
    }
}
