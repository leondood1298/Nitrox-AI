namespace Nitrox.Server.Subnautica.Models.GameLogic;

/// <summary>
///     What a line of <see cref="ScannerRoomDiagnostics" /> says became of a request, or that it is no request at all.
/// </summary>
internal enum ScannerRoomDiagnosticOutcome
{
    ACCEPTED,
    REJECTED,

    /// <summary>
    ///     What the server holds for a room breaks one of the rules of <see cref="ScannerRoomStateFingerprint.Validate" />.
    /// </summary>
    INVARIANT_FAILURE,

    /// <summary>
    ///     No request: a room's state at a named moment (load, save, a tester's mark), to compare two moments by.
    /// </summary>
    CHECKPOINT
}
