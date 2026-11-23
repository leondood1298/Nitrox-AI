namespace Nitrox.Server.Subnautica.Models.GameLogic;

/// <summary>
///     How many events <see cref="ScannerRoomDiagnostics" /> has counted since the server started, by outcome. The
///     history is bounded and the log thinned out, so only these say how often something happened.
/// </summary>
/// <param name="SuppressedWarnings">Repeated rejections that were counted and got neither a log line nor a place in the history.</param>
internal readonly record struct ScannerRoomDiagnosticCounters(
    long Recorded,
    long Accepted,
    long Rejected,
    long InvariantFailures,
    long Checkpoints,
    long SuppressedWarnings = 0);
