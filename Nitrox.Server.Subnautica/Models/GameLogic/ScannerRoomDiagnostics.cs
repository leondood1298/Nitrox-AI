using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading;
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
    public const string Prefix = "[SRD1]";
    public const int HistoryCapacity = 256;
    private const int WARNING_KEY_CAPACITY = 256;
    private const int WARNING_BURST_LIMIT = 128;
    private static int nextEpoch = BitConverter.ToInt32(Guid.NewGuid().ToByteArray(), 0);

    private readonly Queue<ScannerRoomDiagnosticEntry> history = new(HistoryCapacity);
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
        NitroxId? cameraId = null, SessionId? sessionId = null, int? slot = null, string? reason = null)
    {
        ScannerRoomStateSnapshot? snapshot = null;
        long? dockingRevision = null;
        int? cameraCount = null;
        if (room != null)
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
            entry = new ScannerRoomDiagnosticEntry(
                ++nextSequence,
                epoch,
                NormalizeToken(eventName, 32),
                outcome,
                sessionId,
                ShortId(room?.Id),
                ShortId(cameraId),
                slot,
                snapshot?.DockingRevision ?? dockingRevision,
                snapshot?.CameraCount ?? cameraCount,
                snapshot?.Fingerprint,
                NormalizeToken(reason, 64));

            IncrementCounter(outcome);
            if (history.Count == HistoryCapacity)
            {
                history.Dequeue();
            }
            history.Enqueue(entry);
            string line = entry.Format();
            if (outcome == ScannerRoomDiagnosticOutcome.InvariantFailure)
            {
                // Invariants are rare correctness failures. Never let a preceding rejection burst hide one.
                logger.ZLogWarning($"{line}");
            }
            else if (outcome == ScannerRoomDiagnosticOutcome.Rejected)
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

    private static string ShortId(NitroxId? id)
    {
        string value = id?.ToString() ?? "-";
        return value.Length > 8 ? value[..8] : value;
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
    string Reason)
{
    private static readonly CultureInfo invariantCulture = CultureInfo.InvariantCulture;

    public string Format() => FormattableString.Invariant(
        $"{ScannerRoomDiagnostics.Prefix} n={Sequence} ep={Epoch} side=S ev={EventName} out={OutcomeToken()} sid={Format(SessionId)} room={RoomId} cam={CameraId} slot={Format(Slot)} dRev={Format(DockingRevision)} cams={Format(CameraCount)} fp={StateFingerprint ?? "-"} reason={Reason}");

    public override string ToString() => Format();

    private string OutcomeToken() => Outcome switch
    {
        ScannerRoomDiagnosticOutcome.Accepted => "ok",
        ScannerRoomDiagnosticOutcome.Rejected => "reject",
        ScannerRoomDiagnosticOutcome.InvariantFailure => "invariant",
        ScannerRoomDiagnosticOutcome.Checkpoint => "checkpoint",
        _ => throw new ArgumentOutOfRangeException(nameof(Outcome), Outcome, null)
    };

    private static string Format(SessionId? value) => value is { } sessionId ? ((ushort)sessionId).ToString(invariantCulture) : "-";
    private static string Format(int? value) => value?.ToString(invariantCulture) ?? "-";
    private static string Format(long? value) => value?.ToString(invariantCulture) ?? "-";
}
