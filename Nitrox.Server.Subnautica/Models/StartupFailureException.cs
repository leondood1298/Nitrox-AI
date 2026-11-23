using Nitrox.Model.Constants;

namespace Nitrox.Server.Subnautica.Models;

/// <summary>
///     The server cannot start for a reason the user can act on. The message is meant for the user as it is; the server
///     stops with <see cref="ExitCode" /> and without a stack trace.
/// </summary>
internal class StartupFailureException(string message, int exitCode) : Exception(message)
{
    public const int WORLD_LOAD_FAILED = ServerExitCodes.WORLD_LOAD_FAILED;
    public const int PORT_UNAVAILABLE = ServerExitCodes.PORT_UNAVAILABLE;
    public const int WORLD_IN_USE = ServerExitCodes.WORLD_IN_USE;

    public int ExitCode { get; } = exitCode;
}
