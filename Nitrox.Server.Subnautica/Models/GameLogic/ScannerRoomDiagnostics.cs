using System.Collections.Generic;
using Nitrox.Model.Core;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Bases;
using Nitrox.Model.Subnautica.Helper;

namespace Nitrox.Server.Subnautica.Models.GameLogic;

/// <summary>
///     Compact, bounded diagnostics for canonical Scanner Room transitions.
///     Callers must record transition boundaries only, never per-frame movement or sampling updates.
///     <p />
///     Line shape (see <see cref="DiagnosticLineFormat" />):
///     <c>[SRD1] n= ep= side=S sid= ev= out= room= cam= slot= dockRev= cams= roomLock= camLock= fp= reason=</c>.
///     <c>sid</c> is the player the request came from. <c>roomLock</c> and <c>camLock</c> name who simulates the room
///     and the camera at the moment of the line (<c>3x</c>: session 3 exclusively, <c>1t</c>: session 1 transiently,
///     <c>none</c>: nobody), because most Scanner Room failures come down to the wrong player, or no player, holding
///     one of these two locks.
/// </summary>
internal sealed class ScannerRoomDiagnostics(ILogger<ScannerRoomDiagnostics> logger, SimulationOwnershipData? simulationOwnershipData = null,
    TimeProvider? timeProvider = null)
{
    public const string PREFIX = DiagnosticLineFormat.SCANNER_ROOM_TAG;
    public const int HISTORY_CAPACITY = 256;

    /// <summary>
    ///     Printed for a lock field when the lock exists as a concept for the line but nobody holds it.
    /// </summary>
    internal const string NO_LOCK = "none";

    /// <summary>
    ///     Printed for a lock field when this instance was not given the lock table (unit tests).
    /// </summary>
    internal const string UNKNOWN_LOCK = "?";

    /// <summary>
    ///     How many rejection lines one <see cref="REJECTION_WINDOW" /> may add to the log. A client can make up ids
    ///     and reasons, so thinning out repeats alone does not bound the log.
    /// </summary>
    internal const int REJECTION_LINES_PER_WINDOW = 128;

    private const int REJECTION_KEY_CAPACITY = 256;

    internal static readonly TimeSpan REJECTION_WINDOW = TimeSpan.FromSeconds(60);

    private readonly Queue<ScannerRoomDiagnosticEntry> history = new(HISTORY_CAPACITY);
    private readonly object sync = new();
    private readonly DiagnosticRepeatSampler rejectionSampler = new(REJECTION_KEY_CAPACITY);
    private readonly string epoch = DiagnosticLineFormat.NewEpoch();
    private readonly TimeProvider timeProvider = timeProvider ?? TimeProvider.System;
    private bool epochAnnounced;
    private long nextSequence;
    private long accepted;
    private long rejected;
    private long invariantFailures;
    private long checkpoints;
    private long suppressedWarnings;
    private bool rejectionWindowOpen;
    private long rejectionWindowStart;
    private int rejectionLinesInWindow;
    private long rejectionsMutedInWindow;

    internal string Epoch => epoch;

    public ScannerRoomDiagnosticEntry RecordAccepted(string eventName, MapRoomEntity? room = null, NitroxId? cameraId = null,
        SessionId? sessionId = null, int? slot = null, string? reason = null) =>
        Record(ScannerRoomDiagnosticOutcome.Accepted, eventName, room, cameraId, sessionId, slot, reason);

    public ScannerRoomDiagnosticEntry RecordRejected(string eventName, MapRoomEntity? room = null, NitroxId? cameraId = null,
        SessionId? sessionId = null, int? slot = null, string? reason = null) =>
        Record(ScannerRoomDiagnosticOutcome.Rejected, eventName, room, cameraId, sessionId, slot, reason);

    public ScannerRoomDiagnosticEntry RecordInvariantFailure(string eventName, MapRoomEntity? room = null, NitroxId? cameraId = null,
        SessionId? sessionId = null, int? slot = null, string? reason = null) =>
        Record(ScannerRoomDiagnosticOutcome.InvariantFailure, eventName, room, cameraId, sessionId, slot, reason);

    public ScannerRoomDiagnosticEntry RecordCheckpoint(string eventName, MapRoomEntity room, string? reason = null)
    {
        string? invariantFailure = ScannerRoomStateFingerprint.Validate(room);
        return invariantFailure == null
            ? Record(ScannerRoomDiagnosticOutcome.Checkpoint, eventName, room, reason: reason)
            : Record(ScannerRoomDiagnosticOutcome.InvariantFailure, $"{eventName}_invalid", room, reason: invariantFailure);
    }

    public ScannerRoomDiagnosticEntry RecordCapturedCheckpoint(string eventName, MapRoomEntity room,
        ScannerRoomStateSnapshot snapshot, string? invariantFailure, string? reason = null) =>
        invariantFailure == null
            ? Record(ScannerRoomDiagnosticOutcome.Checkpoint, eventName, room, reason: reason, capturedSnapshot: snapshot)
            : Record(ScannerRoomDiagnosticOutcome.InvariantFailure, $"{eventName}_invalid", room,
                reason: invariantFailure, capturedSnapshot: snapshot);

    public IReadOnlyList<ScannerRoomDiagnosticEntry> GetHistory()
    {
        lock (sync)
        {
            return history.ToArray();
        }
    }

    public ScannerRoomDiagnosticCounters GetCounters()
    {
        lock (sync)
        {
            return new ScannerRoomDiagnosticCounters(nextSequence, accepted, rejected, invariantFailures, checkpoints, suppressedWarnings);
        }
    }

    /// <summary>
    ///     Who simulates <paramref name="id" /> right now, in the form used by the <c>roomLock</c> and <c>camLock</c> fields.
    /// </summary>
    internal string DescribeLock(NitroxId? id)
    {
        if (id == null)
        {
            return DiagnosticLineFormat.NOT_APPLICABLE;
        }
        if (simulationOwnershipData == null)
        {
            return UNKNOWN_LOCK;
        }
        return simulationOwnershipData.TryGetLock(id, out SimulationOwnershipData.PlayerLock playerLock)
            ? FormatLock(playerLock.Player.SessionId, playerLock.LockType)
            : NO_LOCK;
    }

    internal static string FormatLock(SessionId holder, SimulationLockType lockType) =>
        $"{DiagnosticLineFormat.Session(holder)}{(lockType == SimulationLockType.EXCLUSIVE ? 'x' : 't')}";

    private ScannerRoomDiagnosticEntry Record(ScannerRoomDiagnosticOutcome outcome, string eventName, MapRoomEntity? room,
        NitroxId? cameraId = null, SessionId? sessionId = null, int? slot = null, string? reason = null,
        ScannerRoomStateSnapshot? capturedSnapshot = null)
    {
        // Read before taking any other lock: callers may hold the room's lock, never the lock table's.
        string roomLock = DescribeLock(room?.Id);
        string cameraLock = DescribeLock(cameraId);
        ScannerRoomStateSnapshot? snapshot = capturedSnapshot;
        long? dockingRevision = null;
        int? cameraCount = null;
        if (snapshot is { } captured)
        {
            dockingRevision = captured.DockingRevision;
            cameraCount = captured.CameraCount;
        }
        else if (room != null)
        {
            lock (room)
            {
                dockingRevision = room.DockingRevision;
                cameraCount = room.CameraRegistry.Count;
                if (outcome is ScannerRoomDiagnosticOutcome.Checkpoint or ScannerRoomDiagnosticOutcome.InvariantFailure)
                {
                    snapshot = ScannerRoomStateFingerprint.Create(room);
                }
            }
        }
        ScannerRoomDiagnosticEntry entry;
        lock (sync)
        {
            AnnounceEpoch();
            CloseRejectionWindowIfElapsed();
            entry = new ScannerRoomDiagnosticEntry(
                ++nextSequence,
                epoch,
                DiagnosticLineFormat.Token(eventName, 32),
                outcome,
                sessionId,
                DiagnosticLineFormat.ShortId(room?.Id, false),
                DiagnosticLineFormat.ShortId(cameraId, true),
                slot,
                snapshot?.DockingRevision ?? dockingRevision,
                snapshot?.CameraCount ?? cameraCount,
                snapshot?.Fingerprint,
                DiagnosticLineFormat.Token(reason, 64),
                roomLock,
                cameraLock);
            bool writeRejection = false;
            if (outcome == ScannerRoomDiagnosticOutcome.Rejected)
            {
                writeRejection = ShouldWriteRejection(entry, out long occurrences);
                entry = entry with { Repeat = occurrences };
            }

            IncrementCounter(outcome);
            if (history.Count == HISTORY_CAPACITY)
            {
                history.Dequeue();
            }
            history.Enqueue(entry);
            if (outcome == ScannerRoomDiagnosticOutcome.InvariantFailure)
            {
                // Invariants are rare correctness failures. Never let a preceding rejection burst hide one.
                string line = entry.Format();
                logger.ZLogWarning($"{line}");
            }
            else if (outcome == ScannerRoomDiagnosticOutcome.Rejected)
            {
                if (writeRejection)
                {
                    string line = entry.Format();
                    logger.ZLogWarning($"{line}");
                }
                else
                {
                    suppressedWarnings++;
                }
            }
            else
            {
                string line = entry.Format();
                logger.ZLogInformation($"{line}");
            }
        }
        return entry;
    }

    /// <summary>
    ///     Writes one marker before the first event of this server run. The UTC stamp next to the logger's own local
    ///     time is what lets this log be lined up with the client logs, which carry the same kind of marker.
    /// </summary>
    private void AnnounceEpoch()
    {
        if (epochAnnounced)
        {
            return;
        }
        epochAnnounced = true;
        string line = FormatMarker("epoch", null, "server_diagnostics_started");
        logger.ZLogInformation($"{line}");
    }

    private string FormatMarker(string eventName, SessionId? sessionId, string reason) =>
        $"{DiagnosticLineFormat.Header(PREFIX, null, epoch, DiagnosticLineFormat.SERVER_SIDE, sessionId, eventName, "info")} utc={DiagnosticLineFormat.UtcStamp(timeProvider.GetUtcNow())} reason={DiagnosticLineFormat.Token(reason, 64)}";

    /// <summary>
    ///     Decides whether a rejection gets a log line. Repeats of the same rejection are thinned out and say how
    ///     often they happened (<c>rep=</c>). Beyond that, one time window only gets a fixed number of lines; the
    ///     log says when that limit is reached and, once the window is over, how many lines it cost. The limit is
    ///     per window on purpose: a budget for the whole server run would be used up by the first busy minute and
    ///     hide every rejection after it.
    /// </summary>
    private bool ShouldWriteRejection(ScannerRoomDiagnosticEntry entry, out long occurrences)
    {
        string key = $"{entry.EventName}|{entry.SessionId}|{entry.RoomId}|{entry.CameraId}|{entry.Reason}";
        if (!rejectionSampler.Observe(key, out occurrences))
        {
            return false;
        }

        if (!rejectionWindowOpen)
        {
            rejectionWindowOpen = true;
            rejectionWindowStart = timeProvider.GetTimestamp();
            rejectionLinesInWindow = 0;
            rejectionsMutedInWindow = 0;
        }
        if (rejectionLinesInWindow < REJECTION_LINES_PER_WINDOW)
        {
            rejectionLinesInWindow++;
            return true;
        }
        if (rejectionsMutedInWindow == 0)
        {
            string line = FormatMarker("log_budget", null,
                $"rejections_muted_after_{REJECTION_LINES_PER_WINDOW}_lines_in_{(int)REJECTION_WINDOW.TotalSeconds}s");
            logger.ZLogWarning($"{line}");
        }
        rejectionsMutedInWindow++;
        return false;
    }

    private void CloseRejectionWindowIfElapsed()
    {
        if (!rejectionWindowOpen || timeProvider.GetElapsedTime(rejectionWindowStart) < REJECTION_WINDOW)
        {
            return;
        }
        rejectionWindowOpen = false;
        if (rejectionsMutedInWindow > 0)
        {
            string line = FormatMarker("log_budget", null, $"rejections_logged_again_{rejectionsMutedInWindow}_were_muted");
            logger.ZLogWarning($"{line}");
        }
    }

    private void IncrementCounter(ScannerRoomDiagnosticOutcome outcome)
    {
        switch (outcome)
        {
            case ScannerRoomDiagnosticOutcome.Accepted:
                accepted++;
                break;
            case ScannerRoomDiagnosticOutcome.Rejected:
                rejected++;
                break;
            case ScannerRoomDiagnosticOutcome.InvariantFailure:
                invariantFailures++;
                break;
            case ScannerRoomDiagnosticOutcome.Checkpoint:
                checkpoints++;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(outcome), outcome, null);
        }
    }
}

internal enum ScannerRoomDiagnosticOutcome
{
    Accepted,
    Rejected,
    InvariantFailure,
    Checkpoint
}

internal readonly record struct ScannerRoomDiagnosticCounters(
    long Recorded,
    long Accepted,
    long Rejected,
    long InvariantFailures,
    long Checkpoints,
    long SuppressedWarnings = 0);

internal readonly record struct ScannerRoomDiagnosticEntry(
    long Sequence,
    string Epoch,
    string EventName,
    ScannerRoomDiagnosticOutcome Outcome,
    SessionId? SessionId,
    string RoomId,
    string CameraId,
    int? Slot,
    long? DockingRevision,
    int? CameraCount,
    string? StateFingerprint,
    string Reason,
    string RoomLock = DiagnosticLineFormat.NOT_APPLICABLE,
    string CameraLock = DiagnosticLineFormat.NOT_APPLICABLE,
    long Repeat = 1)
{
    /// <summary>
    ///     <c>rep=</c> is only printed for a repeated rejection: it counts how often the same request was rejected
    ///     for the same reason, including the repeats that did not get a line of their own.
    /// </summary>
    public string Format() =>
        $"{DiagnosticLineFormat.Header(ScannerRoomDiagnostics.PREFIX, Sequence, Epoch, DiagnosticLineFormat.SERVER_SIDE, SessionId, EventName, OutcomeToken())} room={RoomId} cam={CameraId} slot={DiagnosticLineFormat.Number(Slot)} dockRev={DiagnosticLineFormat.Number(DockingRevision)} cams={DiagnosticLineFormat.Number(CameraCount)} roomLock={RoomLock} camLock={CameraLock} fp={StateFingerprint ?? DiagnosticLineFormat.NOT_APPLICABLE}{(Repeat > 1 ? $" rep={DiagnosticLineFormat.Number(Repeat)}" : "")} reason={Reason}";

    public override string ToString() => Format();

    private string OutcomeToken() => Outcome switch
    {
        ScannerRoomDiagnosticOutcome.Accepted => "ok",
        ScannerRoomDiagnosticOutcome.Rejected => "reject",
        ScannerRoomDiagnosticOutcome.InvariantFailure => "invariant",
        ScannerRoomDiagnosticOutcome.Checkpoint => "checkpoint",
        _ => throw new ArgumentOutOfRangeException(nameof(Outcome), Outcome, null)
    };
}
