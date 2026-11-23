using Nitrox.Model.Core;
using Nitrox.Model.Subnautica.Helper;

namespace Nitrox.Server.Subnautica.Models.GameLogic;

internal readonly record struct ScannerRoomDiagnosticEntry(
    long Sequence,
    string Epoch,
    string EventName,
    ScannerRoomDiagnosticOutcome Outcome,
    SessionId? SessionId,
    string RoomId,
    string CameraId,
    int? Slot,
    int? CameraCount,
    string? StateFingerprint,
    string Reason,
    string RoomLock = DiagnosticLineFormat.NOT_APPLICABLE,
    string CameraLock = DiagnosticLineFormat.NOT_APPLICABLE,
    long Repeat = 1,
    DateTimeOffset Time = default)
{
    /// <summary>
    ///     <c>rep=</c> is only printed for a repeated rejection: it counts how often the same request was rejected
    ///     for the same reason, including the repeats that did not get a line of their own.
    /// </summary>
    public string Format() =>
        $"{DiagnosticLineFormat.Header(ScannerRoomDiagnostics.PREFIX, Sequence, Epoch, DiagnosticLineFormat.SERVER_SIDE, SessionId, EventName, OutcomeToken())} room={RoomId} cam={CameraId} slot={DiagnosticLineFormat.Number(Slot)} cams={DiagnosticLineFormat.Number(CameraCount)} roomLock={RoomLock} camLock={CameraLock} fp={StateFingerprint ?? DiagnosticLineFormat.NOT_APPLICABLE}{(Repeat > 1 ? $" rep={DiagnosticLineFormat.Number(Repeat)}" : "")} reason={Reason}";

    public override string ToString() => Format();

    private string OutcomeToken() => Outcome switch
    {
        ScannerRoomDiagnosticOutcome.ACCEPTED => "ok",
        ScannerRoomDiagnosticOutcome.REJECTED => "reject",
        ScannerRoomDiagnosticOutcome.INVARIANT_FAILURE => "invariant",
        ScannerRoomDiagnosticOutcome.CHECKPOINT => "checkpoint",
        _ => "unknown"
    };
}
