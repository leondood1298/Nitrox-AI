namespace Nitrox.Server.Subnautica.Models;

/// <summary>
///     The server cannot start for a reason the user can act on. The message is meant for the user as it is; the server
///     stops with <see cref="ExitCode" /> and without a stack trace.
/// </summary>
internal class StartupFailureException(string message, int exitCode) : Exception(message)
{
    public const int WORLD_LOAD_FAILED = 3;
    public const int PORT_UNAVAILABLE = 4;
    public const int WORLD_IN_USE = 5;

    public int ExitCode { get; } = exitCode;
}
