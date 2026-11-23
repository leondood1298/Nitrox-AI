using System.Collections.Generic;
using Nitrox.Model.Core;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Bases;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;
using Nitrox.Model.Subnautica.Helper;

namespace Nitrox.Server.Subnautica.Models.GameLogic;

/// <summary>
///     Compact, bounded diagnostics for canonical Scanner Room transitions.
///     Callers must record transition boundaries only, never per-frame movement or sampling updates.
///     The history holds the same events as the log: a repeated rejection that got no line is counted only.
///     <p />
///     Line shape (see <see cref="DiagnosticLineFormat" />):
///     <c>[SRD1] n= ep= side=S sid= ev= out= room= cam= slot= dockRev= cams= roomLock= camLock= fp= reason=</c>.
///     <c>sid</c> is the player the request came from. <c>roomLock</c> and <c>camLock</c> name who simulates the room
///     and the camera at the moment of the line (<c>3x</c>: session 3 exclusively, <c>1t</c>: session 1 transiently,
///     <c>none</c>: nobody), because most Scanner Room failures come down to the wrong player, or no player, holding
///     one of these two locks. <c>n</c> counts every event, so a gap in it is a repeated rejection that got no line;
///     the next line for the same rejection carries <c>rep=</c> with how often it has happened by then.
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
    private const int STATE_REVISION_CAPACITY = 256;

    internal static readonly TimeSpan REJECTION_WINDOW = TimeSpan.FromSeconds(60);

    private readonly Queue<ScannerRoomDiagnosticEntry> history = new(HISTORY_CAPACITY);
    private readonly object sync = new();
    private readonly DiagnosticRepeatSampler rejectionSampler = new(REJECTION_KEY_CAPACITY);
    private readonly Dictionary<string, long> lastStateRevisions = new(StringComparer.Ordinal);
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
    private long ownFailures;
    private long unchangedRepeats;

    internal string Epoch => epoch;

    /// <summary>
    ///     How many requests were not recorded because they changed nothing and are repeated as a matter of course:
    ///     a refresh that left the state at the revision already recorded, or scan progress the server already had.
    /// </summary>
    internal long UnchangedRepeats => Interlocked.Read(ref unchangedRepeats);

    /// <summary>
    ///     How often recording or writing a line failed. Such a failure is swallowed, because these calls sit in the
    ///     middle of the packet processors they observe.
    /// </summary>
    internal long OwnFailures => Interlocked.Read(ref ownFailures);

    /// <param name="stateRevision">
    ///     The revision of the state the request was accepted into, for requests a client repeats on a timer (the
    ///     available scan types are republished every few seconds). When it is the revision already recorded for
    ///     this event and room, nothing changed and nothing is recorded: one such request per room would otherwise
    ///     add a line every few seconds and push every real transition out of the history.
    /// </param>
    public ScannerRoomDiagnosticEntry RecordAccepted(string eventName, MapRoomEntity? room = null, NitroxId? cameraId = null,
        SessionId? sessionId = null, int? slot = null, string? reason = null, long? stateRevision = null) =>
        Record(ScannerRoomDiagnosticOutcome.ACCEPTED, eventName, room, cameraId, sessionId, slot, reason, stateRevision: stateRevision);

    public ScannerRoomDiagnosticEntry RecordRejected(string eventName, MapRoomEntity? room = null, NitroxId? cameraId = null,
        SessionId? sessionId = null, int? slot = null, string? reason = null) =>
        Record(ScannerRoomDiagnosticOutcome.REJECTED, eventName, room, cameraId, sessionId, slot, reason);

    /// <summary>
    ///     Records a scan target or scan progress request that was dropped because it was based on an older state
    ///     of the room than the server holds. Only a dropped change of target gets a line: it is a choice a player
    ///     made and lost. Repeated progress from the simulating player is dropped all the time and means nothing.
    /// </summary>
    public void RecordStaleScanRequest(MapRoomEntity room, SessionId sessionId, MapRoomMetadata requested)
    {
        try
        {
            MapRoomMetadata? current = room.Metadata as MapRoomMetadata;
            if (current != null && Equals(current.TypeToScan, requested.TypeToScan))
            {
                Interlocked.Increment(ref unchangedRepeats);
                return;
            }
            Record(ScannerRoomDiagnosticOutcome.REJECTED, "scan_target", room, sessionId: sessionId,
                reason: $"stale_request_gen_{requested.Generation}_room_gen_{current?.Generation ?? 0}");
        }
        catch (Exception exception)
        {
            ReportOwnFailure(exception, ScannerRoomDiagnosticOutcome.REJECTED, "scan_target", sessionId);
        }
    }

    public ScannerRoomDiagnosticEntry RecordInvariantFailure(string eventName, MapRoomEntity? room = null, NitroxId? cameraId = null,
        SessionId? sessionId = null, int? slot = null, string? reason = null) =>
        Record(ScannerRoomDiagnosticOutcome.INVARIANT_FAILURE, eventName, room, cameraId, sessionId, slot, reason);

    public ScannerRoomDiagnosticEntry RecordCheckpoint(string eventName, MapRoomEntity room, string? reason = null)
    {
        string? invariantFailure;
        try
        {
            invariantFailure = ScannerRoomStateFingerprint.Validate(room);
        }
        catch (Exception exception)
        {
            return ReportOwnFailure(exception, ScannerRoomDiagnosticOutcome.CHECKPOINT, eventName, null);
        }
        return invariantFailure == null
            ? Record(ScannerRoomDiagnosticOutcome.CHECKPOINT, eventName, room, reason: reason)
            : Record(ScannerRoomDiagnosticOutcome.INVARIANT_FAILURE, $"{eventName}_invalid", room, reason: invariantFailure);
    }

    public ScannerRoomDiagnosticEntry RecordCapturedCheckpoint(string eventName, MapRoomEntity room,
        ScannerRoomStateSnapshot snapshot, string? invariantFailure, string? reason = null) =>
        invariantFailure == null
            ? Record(ScannerRoomDiagnosticOutcome.CHECKPOINT, eventName, room, reason: reason, capturedSnapshot: snapshot)
            : Record(ScannerRoomDiagnosticOutcome.INVARIANT_FAILURE, $"{eventName}_invalid", room,
                reason: invariantFailure, capturedSnapshot: snapshot);

    /// <summary>
    ///     Writes a line a tester asked for, to bracket a test step in the log. It is not an event: it has no number
    ///     and does not enter the history, so it also works when there is no Scanner Room to checkpoint.
    /// </summary>
    public void RecordMark(string label)
    {
        try
        {
            lock (sync)
            {
                AnnounceEpoch();
                Write(LogLevel.Information, FormatMarker("mark", null, label));
            }
        }
        catch (Exception)
        {
            Interlocked.Increment(ref ownFailures);
        }
    }

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
        ScannerRoomStateSnapshot? capturedSnapshot = null, long? stateRevision = null)
    {
        try
        {
            if (stateRevision is { } revision && IsUnchangedState(eventName, room, revision))
            {
                Interlocked.Increment(ref unchangedRepeats);
                return new ScannerRoomDiagnosticEntry(0, epoch, DiagnosticLineFormat.Token(eventName, 32), outcome, sessionId,
                    DiagnosticLineFormat.ShortId(room?.Id, false), DiagnosticLineFormat.NOT_APPLICABLE, slot, null, null, null, "unchanged");
            }
            return RecordCore(outcome, eventName, room, cameraId, sessionId, slot, reason, capturedSnapshot);
        }
        catch (Exception exception)
        {
            return ReportOwnFailure(exception, outcome, eventName, sessionId);
        }
    }

    private bool IsUnchangedState(string eventName, MapRoomEntity? room, long stateRevision)
    {
        string key = $"{eventName}|{room?.Id}";
        lock (sync)
        {
            if (lastStateRevisions.TryGetValue(key, out long recorded) && recorded == stateRevision)
            {
                return true;
            }
            if (lastStateRevisions.Count >= STATE_REVISION_CAPACITY)
            {
                // Rooms that were deconstructed never leave on their own. Forgetting everything costs one extra line per room.
                lastStateRevisions.Clear();
            }
            lastStateRevisions[key] = stateRevision;
            return false;
        }
    }

    /// <summary>
    ///     A diagnostics call must never break the feature it observes: a processor that has already changed state
    ///     still has to answer the client and release its locks. So a failure in here ends in a placeholder entry.
    /// </summary>
    private ScannerRoomDiagnosticEntry ReportOwnFailure(Exception exception, ScannerRoomDiagnosticOutcome outcome, string eventName,
        SessionId? sessionId)
    {
        long failures = Interlocked.Increment(ref ownFailures);
        if (DiagnosticRepeatSampler.IsSampledOccurrence(failures))
        {
            try
            {
                logger.ZLogError(exception, $"{PREFIX} could not record {eventName} (failure {failures} of the diagnostics themselves)");
            }
            catch (Exception)
            {
                // The log sink is what failed. There is nowhere left to report it.
            }
        }
        return new ScannerRoomDiagnosticEntry(0, epoch, DiagnosticLineFormat.Token(eventName, 32), outcome, sessionId,
            DiagnosticLineFormat.NOT_APPLICABLE, DiagnosticLineFormat.NOT_APPLICABLE, null, null, null, null, "diagnostics_failed");
    }

    private ScannerRoomDiagnosticEntry RecordCore(ScannerRoomDiagnosticOutcome outcome, string eventName, MapRoomEntity? room,
        NitroxId? cameraId, SessionId? sessionId, int? slot, string? reason, ScannerRoomStateSnapshot? capturedSnapshot)
    {
        // The room is a copy, or the live room handed over by the Scanner Room manager from inside its gate, so it
        // does not change while it is read here.
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
            dockingRevision = room.DockingRevision;
            cameraCount = room.CameraRegistry?.Count ?? 0;
            if (outcome is ScannerRoomDiagnosticOutcome.CHECKPOINT or ScannerRoomDiagnosticOutcome.INVARIANT_FAILURE)
            {
                snapshot = ScannerRoomStateFingerprint.Create(room);
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
                cameraLock,
                Time: timeProvider.GetUtcNow());
            bool writeRejection = false;
            if (outcome == ScannerRoomDiagnosticOutcome.REJECTED)
            {
                writeRejection = ShouldWriteRejection(entry, out long occurrences);
                entry = entry with { Repeat = occurrences };
            }

            IncrementCounter(outcome);
            if (outcome == ScannerRoomDiagnosticOutcome.REJECTED && !writeRejection)
            {
                // Counted, but kept out of the history as well as out of the log: a request rejected on every
                // packet would otherwise leave nothing else in the history within seconds.
                suppressedWarnings++;
                return entry;
            }

            if (history.Count == HISTORY_CAPACITY)
            {
                history.Dequeue();
            }
            history.Enqueue(entry);
            // Invariants are rare correctness failures and are never thinned out, whatever came before them.
            Write(outcome is ScannerRoomDiagnosticOutcome.REJECTED or ScannerRoomDiagnosticOutcome.INVARIANT_FAILURE ? LogLevel.Warning : LogLevel.Information,
                entry.Format());
        }
        return entry;
    }

    /// <summary>
    ///     Writes a line without letting a failing log sink undo the bookkeeping already done for the event.
    /// </summary>
    private void Write(LogLevel level, string line)
    {
        try
        {
            if (level == LogLevel.Warning)
            {
                logger.ZLogWarning($"{line}");
            }
            else
            {
                logger.ZLogInformation($"{line}");
            }
        }
        catch (Exception)
        {
            Interlocked.Increment(ref ownFailures);
        }
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
        Write(LogLevel.Information, FormatMarker("epoch", null, "server_diagnostics_started"));
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
            Write(LogLevel.Warning, FormatMarker("log_budget", null,
                $"rejections_muted_after_{REJECTION_LINES_PER_WINDOW}_lines_in_{(int)REJECTION_WINDOW.TotalSeconds}s"));
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
            Write(LogLevel.Warning, FormatMarker("log_budget", null, $"rejections_logged_again_{rejectionsMutedInWindow}_were_muted"));
        }
    }

    private void IncrementCounter(ScannerRoomDiagnosticOutcome outcome)
    {
        switch (outcome)
        {
            case ScannerRoomDiagnosticOutcome.ACCEPTED:
                accepted++;
                break;
            case ScannerRoomDiagnosticOutcome.REJECTED:
                rejected++;
                break;
            case ScannerRoomDiagnosticOutcome.INVARIANT_FAILURE:
                invariantFailures++;
                break;
            case ScannerRoomDiagnosticOutcome.CHECKPOINT:
                checkpoints++;
                break;
        }
    }
}

internal enum ScannerRoomDiagnosticOutcome
{
    ACCEPTED,
    REJECTED,
    INVARIANT_FAILURE,
    CHECKPOINT
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
    long Repeat = 1,
    DateTimeOffset Time = default)
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
        ScannerRoomDiagnosticOutcome.ACCEPTED => "ok",
        ScannerRoomDiagnosticOutcome.REJECTED => "reject",
        ScannerRoomDiagnosticOutcome.INVARIANT_FAILURE => "invariant",
        ScannerRoomDiagnosticOutcome.CHECKPOINT => "checkpoint",
        _ => "unknown"
    };
}
