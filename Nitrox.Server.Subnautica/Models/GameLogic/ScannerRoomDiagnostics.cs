using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Nitrox.Model.Core;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Bases;

namespace Nitrox.Server.Subnautica.Models.GameLogic;

/// <summary>
///     Compact, bounded diagnostics for canonical Scanner Room transitions.
///     Callers must record transition boundaries only, never per-frame movement or sampling updates.
/// </summary>
internal sealed class ScannerRoomDiagnostics(ILogger<ScannerRoomDiagnostics> logger)
{
    public const string PREFIX = "[SRD1]";
    public const int HISTORY_CAPACITY = 256;
    private const int WARNING_KEY_CAPACITY = 256;
    private const int WARNING_BURST_LIMIT = 128;
    private static int nextEpoch = BitConverter.ToInt32(Guid.NewGuid().ToByteArray(), 0);

    private readonly Queue<ScannerRoomDiagnosticEntry> history = new(HISTORY_CAPACITY);
    private readonly object sync = new();
    private readonly Dictionary<string, long> warningOccurrences = new(WARNING_KEY_CAPACITY);
    private readonly Queue<string> warningKeys = new(WARNING_KEY_CAPACITY);
    private readonly string epoch = unchecked((uint)Interlocked.Increment(ref nextEpoch)).ToString("x8", CultureInfo.InvariantCulture);
    private long nextSequence;
    private long accepted;
    private long rejected;
    private long invariantFailures;
    private long checkpoints;
    private long warningObservations;
    private long suppressedWarnings;

    internal string Epoch => epoch;

    public ScannerRoomDiagnosticEntry RecordAccepted(string eventName, MapRoomEntity? room = null, NitroxId? cameraId = null,
        SessionId? sessionId = null, int? slot = null, string? reason = null) =>
        Record(ScannerRoomDiagnosticOutcome.ACCEPTED, eventName, room, cameraId, sessionId, slot, reason);

    public ScannerRoomDiagnosticEntry RecordRejected(string eventName, MapRoomEntity? room = null, NitroxId? cameraId = null,
        SessionId? sessionId = null, int? slot = null, string? reason = null) =>
        Record(ScannerRoomDiagnosticOutcome.REJECTED, eventName, room, cameraId, sessionId, slot, reason);

    public ScannerRoomDiagnosticEntry RecordInvariantFailure(string eventName, MapRoomEntity? room = null, NitroxId? cameraId = null,
        SessionId? sessionId = null, int? slot = null, string? reason = null) =>
        Record(ScannerRoomDiagnosticOutcome.INVARIANT_FAILURE, eventName, room, cameraId, sessionId, slot, reason);

    public ScannerRoomDiagnosticEntry RecordCheckpoint(string eventName, MapRoomEntity room, string? reason = null)
    {
        string? invariantFailure = ScannerRoomStateFingerprint.Validate(room);
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

    private ScannerRoomDiagnosticEntry Record(ScannerRoomDiagnosticOutcome outcome, string eventName, MapRoomEntity? room,
        NitroxId? cameraId = null, SessionId? sessionId = null, int? slot = null, string? reason = null,
        ScannerRoomStateSnapshot? capturedSnapshot = null)
    {
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
                if (outcome is ScannerRoomDiagnosticOutcome.CHECKPOINT or ScannerRoomDiagnosticOutcome.INVARIANT_FAILURE)
                {
                    snapshot = ScannerRoomStateFingerprint.Create(room);
                }
            }
        }
        ScannerRoomDiagnosticEntry entry;
        lock (sync)
        {
            entry = new ScannerRoomDiagnosticEntry(
                ++nextSequence,
                epoch,
                NormalizeToken(eventName, 32),
                outcome,
                sessionId,
                ShortId(room?.Id),
                ShortId(cameraId, distinguishSiblingCamera: true),
                slot,
                snapshot?.DockingRevision ?? dockingRevision,
                snapshot?.CameraCount ?? cameraCount,
                snapshot?.Fingerprint,
                NormalizeToken(reason, 64));

            IncrementCounter(outcome);
            if (history.Count == HISTORY_CAPACITY)
            {
                history.Dequeue();
            }
            history.Enqueue(entry);
            string line = entry.Format();
            if (outcome == ScannerRoomDiagnosticOutcome.INVARIANT_FAILURE)
            {
                // Invariants are rare correctness failures. Never let a preceding rejection burst hide one.
                logger.ZLogWarning($"{line}");
            }
            else if (outcome == ScannerRoomDiagnosticOutcome.REJECTED)
            {
                if (ShouldLogWarning(entry))
                {
                    logger.ZLogWarning($"{line}");
                }
                else
                {
                    suppressedWarnings++;
                }
            }
            else
            {
                logger.ZLogInformation($"{line}");
            }
        }
        return entry;
    }

    private bool ShouldLogWarning(ScannerRoomDiagnosticEntry entry)
    {
        warningObservations++;
        string key = $"{entry.EventName}|{entry.Outcome}|{entry.SessionId}|{entry.RoomId}|{entry.CameraId}|{entry.Reason}";
        if (!warningOccurrences.TryGetValue(key, out long occurrences))
        {
            if (warningOccurrences.Count == WARNING_KEY_CAPACITY)
            {
                string oldest = warningKeys.Dequeue();
                warningOccurrences.Remove(oldest);
            }
            warningKeys.Enqueue(key);
        }
        occurrences++;
        warningOccurrences[key] = occurrences;

        bool duplicateSample = occurrences <= 2 || (occurrences & (occurrences - 1)) == 0;
        bool globalSample = warningObservations <= WARNING_BURST_LIMIT || warningObservations % WARNING_BURST_LIMIT == 0;
        return duplicateSample && globalSample;
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
            default:
                throw new ArgumentOutOfRangeException(nameof(outcome), outcome, null);
        }
    }

    private static string NormalizeToken(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "-";
        }

        StringBuilder token = new(Math.Min(value.Length, maxLength));
        foreach (char character in value)
        {
            char normalized = char.ToLowerInvariant(character);
            if (char.IsAsciiLetterOrDigit(normalized) || normalized is '-' or '_' or '.')
            {
                token.Append(normalized);
            }
            else if (token.Length > 0 && token[^1] != '_')
            {
                token.Append('_');
            }

            if (token.Length == maxLength)
            {
                break;
            }
        }
        return token.ToString().Trim('_') is { Length: > 0 } result ? result : "-";
    }

    private static string ShortId(NitroxId? id, bool distinguishSiblingCamera = false)
    {
        string value = id?.ToString() ?? "-";
        // Stable camera IDs created for one Scanner Room share their first GUID
        // group. Include the first half of the fourth group so two sibling
        // cameras remain distinguishable in compact evidence.
        return distinguishSiblingCamera && value.Length >= 23 && value[8] == '-' && value[13] == '-' && value[18] == '-'
            ? $"{value[..8]}.{value.Substring(19, 4)}"
            : value.Length > 8 ? value[..8] : value;
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
    string Reason)
{
    private static readonly CultureInfo invariantCulture = CultureInfo.InvariantCulture;

    public string Format() => FormattableString.Invariant(
        $"{ScannerRoomDiagnostics.PREFIX} n={Sequence} ep={Epoch} side=S ev={EventName} out={OutcomeToken()} sid={Format(SessionId)} room={RoomId} cam={CameraId} slot={Format(Slot)} dRev={Format(DockingRevision)} cams={Format(CameraCount)} fp={StateFingerprint ?? "-"} reason={Reason}");

    public override string ToString() => Format();

    private string OutcomeToken() => Outcome switch
    {
        ScannerRoomDiagnosticOutcome.ACCEPTED => "ok",
        ScannerRoomDiagnosticOutcome.REJECTED => "reject",
        ScannerRoomDiagnosticOutcome.INVARIANT_FAILURE => "invariant",
        ScannerRoomDiagnosticOutcome.CHECKPOINT => "checkpoint",
        _ => throw new ArgumentOutOfRangeException(nameof(Outcome), Outcome, null)
    };

    private static string Format(SessionId? value) => value is { } sessionId ? ((ushort)sessionId).ToString(invariantCulture) : "-";
    private static string Format(int? value) => value?.ToString(invariantCulture) ?? "-";
    private static string Format(long? value) => value?.ToString(invariantCulture) ?? "-";
}
