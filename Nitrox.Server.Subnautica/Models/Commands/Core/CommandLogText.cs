using System.Linq;

namespace Nitrox.Server.Subnautica.Models.Commands.Core;

/// <summary>
///     The text of a command as it may be written to the log: the arguments of commands that take a password are hidden,
///     since logs are kept and often shared to report problems.
/// </summary>
internal static class CommandLogText
{
    /// <summary>
    ///     Commands (and aliases) whose arguments are secrets.
    /// </summary>
    private static readonly string[] SECRET_COMMANDS = ["login", "changeserverpassword", "changeadminpassword"];

    private const string HIDDEN = "***";

    public static string Redact(string commandText)
    {
        string trimmed = commandText.Trim();
        string withoutSlash = trimmed.TrimStart('/');
        int end = withoutSlash.IndexOf(' ');
        string name = end == -1 ? withoutSlash : withoutSlash[..end];
        if (end == -1 || !SECRET_COMMANDS.Contains(name, StringComparer.OrdinalIgnoreCase))
        {
            return trimmed;
        }
        return $"{trimmed[..(trimmed.Length - withoutSlash.Length)]}{name} {HIDDEN}";
    }
}
