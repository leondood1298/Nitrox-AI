namespace Nitrox.Model.Constants;

/// <summary>
///     Exit codes by which the server tells the launcher why it stopped before it could run.
/// </summary>
public static class ServerExitCodes
{
    public const int WORLD_LOAD_FAILED = 3;
    public const int PORT_UNAVAILABLE = 4;
    public const int WORLD_IN_USE = 5;

    /// <summary>
    ///     What the launcher shows below the server's output once it has stopped.
    /// </summary>
    public static string Describe(int? exitCode) => exitCode switch
    {
        null or 0 => "Server stopped.",
        WORLD_LOAD_FAILED => "Server stopped: its world cannot be opened. The lines above say why.",
        PORT_UNAVAILABLE => "Server stopped: its port is in use by another program, often another Nitrox server.",
        WORLD_IN_USE => "Server stopped: this world is already open in another Nitrox server.",
        _ => $"Server stopped unexpectedly (exit code {exitCode}). The server log in the Nitrox logs folder has the details."
    };
}
