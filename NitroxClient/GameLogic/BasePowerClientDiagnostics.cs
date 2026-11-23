using System;
using System.Collections.Generic;
using System.Threading;
using Nitrox.Model.Core;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;
using Nitrox.Model.Subnautica.Helper;
using NitroxClient.Communication.Abstract;

namespace NitroxClient.GameLogic;

/// <summary>
///     Bounded, single-line client evidence for base-power source reconciliation and relay voice transitions.
///     <p />
///     Line shape (see <see cref="DiagnosticLineFormat" />):
///     <c>[BPD1] n= ep= side=C sid= ev= out= base= source= power=current/max initial= wait= reason=</c>. <c>sid</c> is
///     this client's own session id. <c>initial</c> and <c>wait</c> say whether the initial sync had completed and
///     whether the loading screen was still up, because the source applies and voice transitions are only of
///     interest while a player joins; that trace stops at a fixed number of lines and says so. Updates this client
///     dropped as outdated (<c>out=stale</c>) and reports the server refused (<c>out=reject</c>) are recorded for the
///     whole session instead, thinned out when they repeat (<c>rep=</c> counts the repeats).
/// </summary>
public sealed class BasePowerClientDiagnostics
{
    internal const int SOURCE_HISTORY_CAPACITY = 48;
    internal const int AUDIO_HISTORY_CAPACITY = 16;
    internal const int HISTORY_CAPACITY = SOURCE_HISTORY_CAPACITY + AUDIO_HISTORY_CAPACITY + 2;
    private const int REPEAT_KEY_CAPACITY = 128;
    private const int PROBLEM_CAPACITY = 32;

    private readonly string epoch = DiagnosticLineFormat.NewEpoch();
    private readonly List<string> history = new(HISTORY_CAPACITY);
    private readonly object sync = new();
    private readonly DiagnosticRepeatSampler repeatSampler = new(REPEAT_KEY_CAPACITY);
    private readonly Queue<string> problems = new(PROBLEM_CAPACITY);
    private readonly Func<SessionId?> localSessionId;
    private readonly Func<DateTimeOffset> utcNow;
    private bool epochAnnounced;
    private int audioEntries;
    private int sourceEntries;
    private long nextSequence;
    private bool audioTruncated;
    private bool sourceTruncated;
    private long ownFailures;

    internal string Epoch => epoch;

    /// <summary>
    ///     How often recording or writing a line failed. Such a failure is swallowed, because these calls sit in the
    ///     middle of the game code they observe.
    /// </summary>
    internal long OwnFailures => Interlocked.Read(ref ownFailures);

    public BasePowerClientDiagnostics() : this(static () => null, static () => DateTimeOffset.UtcNow)
    {
    }

    /// <summary>
    ///     Used by the client's service container, so every line can carry the session id the server knows this
    ///     player by.
    /// </summary>
    public BasePowerClientDiagnostics(IMultiplayerSession multiplayerSession) : this(() => multiplayerSession.Reservation?.SessionId,
        static () => DateTimeOffset.UtcNow)
    {
    }

    internal BasePowerClientDiagnostics(Func<SessionId?> localSessionId, Func<DateTimeOffset> utcNow)
    {
        this.localSessionId = localSessionId;
        this.utcNow = utcNow;
    }

    internal string? RecordSourceApply(NitroxId? sourceId, PowerSourceMetadata metadata, bool objectFound,
        bool initialSyncCompleted, bool waitScreenWaiting, string reason)
    {
        return Record(false, "source_apply", objectFound ? "ok" : "missing", null, sourceId, metadata.Power, metadata.MaxPower,
            initialSyncCompleted, waitScreenWaiting, $"{reason}_{metadata.SourceType}_{metadata.Revision}");
    }

    public string? RecordAudioTransition(string eventName, bool suppressed, NitroxId? baseId, float power, float maxPower,
        bool initialSyncCompleted, bool waitScreenWaiting, string? reason)
    {
        return Record(true, eventName, suppressed ? "suppress" : "pass", baseId, null, power, maxPower,
            initialSyncCompleted, waitScreenWaiting, reason ?? "live");
    }

    /// <summary>
    ///     Records a source state from the server that this client dropped because it already holds a newer revision.
    ///     The server keeps a revision floor per source id so that revisions never start over, not even for a source
    ///     that was rebuilt. Should that fail, this is all that is left of every later update: the source stays at
    ///     the level it had. Not limited to the join phase, thinned out by repeat instead, because it lasts for as
    ///     long as the source does.
    /// </summary>
    internal string? RecordSourceStale(NitroxId sourceId, PowerSourceMetadata incoming, PowerSourceMetadata held) =>
        RecordProblem("source_apply", "stale", sourceId, incoming, $"rev_{incoming.Revision}_held_{held.Revision}", "stale", true);

    /// <summary>
    ///     Records that the server refused a level this client reported for a source it believes it simulates. A
    ///     client reports once a second until it learns who simulates the source, so repeats are thinned out and the
    ///     line is information, not a warning.
    /// </summary>
    /// <param name="canonical">The state the server told this client to restore.</param>
    internal string? RecordSourceRejected(NitroxId sourceId, PowerSourceMetadata canonical, string? reason) =>
        RecordProblem("source_update", "reject", sourceId, canonical, reason, WithoutDigits(reason), false);

    /// <summary>
    ///     The newest stale and rejected lines. Kept apart from the join trace, which stops at its capacity.
    /// </summary>
    internal IReadOnlyList<string> GetProblems()
    {
        lock (sync)
        {
            return problems.ToArray();
        }
    }

    internal IReadOnlyList<string> GetHistory()
    {
        lock (sync)
        {
            return history.ToArray();
        }
    }

    /// <summary>
    ///     Never throws: a failure in here must not abort the power event or the metadata apply that reported it.
    /// </summary>
    private string? Record(bool audio, string eventName, string outcome, NitroxId? baseId, NitroxId? sourceId,
        float power, float maxPower, bool initialSyncCompleted, bool waitScreenWaiting, string reason)
    {
        try
        {
            return RecordCore(audio, eventName, outcome, baseId, sourceId, power, maxPower, initialSyncCompleted, waitScreenWaiting, reason);
        }
        catch (Exception)
        {
            Interlocked.Increment(ref ownFailures);
            return null;
        }
    }

    private string? RecordCore(bool audio, string eventName, string outcome, NitroxId? baseId, NitroxId? sourceId,
        float power, float maxPower, bool initialSyncCompleted, bool waitScreenWaiting, string reason)
    {
        SessionId? sessionId = ReadLocalSessionId();
        string line;
        string? marker = null;
        lock (sync)
        {
            if (!epochAnnounced)
            {
                epochAnnounced = true;
                marker = $"{DiagnosticLineFormat.Header(DiagnosticLineFormat.BASE_POWER_TAG, null, epoch, DiagnosticLineFormat.CLIENT_SIDE, sessionId, "epoch", "info")} utc={ReadUtcStamp()} reason=client_diagnostics_started";
            }
            int capacity = audio ? AUDIO_HISTORY_CAPACITY : SOURCE_HISTORY_CAPACITY;
            bool atCapacity = audio ? audioEntries >= capacity : sourceEntries >= capacity;
            if (atCapacity)
            {
                bool alreadyTruncated = audio ? audioTruncated : sourceTruncated;
                if (alreadyTruncated)
                {
                    return null;
                }
                if (audio)
                {
                    audioTruncated = true;
                }
                else
                {
                    sourceTruncated = true;
                }
                line = FormatLine(++nextSequence, sessionId, audio ? "audio_trace_limit" : "source_trace_limit",
                    "truncated", null, null, 0f, 0f, initialSyncCompleted, waitScreenWaiting, $"capacity_{capacity}");
                history.Add(line);
            }
            else
            {
                if (audio)
                {
                    audioEntries++;
                }
                else
                {
                    sourceEntries++;
                }

                line = FormatLine(++nextSequence, sessionId, eventName, outcome, baseId, sourceId, power, maxPower,
                    initialSyncCompleted, waitScreenWaiting, reason);
                history.Add(line);
            }
        }
        if (marker != null)
        {
            // Its UTC stamp, next to the log's own local time, lines this log up with the server's and the other client's.
            Write(marker);
        }
        Write(line);
        return line;
    }

    private string? RecordProblem(string eventName, string outcome, NitroxId sourceId, PowerSourceMetadata shown, string? reason, string repeatKey,
        bool warning)
    {
        try
        {
            SessionId? sessionId = ReadLocalSessionId();
            string line;
            string? marker = null;
            lock (sync)
            {
                if (!repeatSampler.Observe($"{eventName}|{sourceId}|{repeatKey}", out long occurrences))
                {
                    return null;
                }
                if (!epochAnnounced)
                {
                    epochAnnounced = true;
                    marker = $"{DiagnosticLineFormat.Header(DiagnosticLineFormat.BASE_POWER_TAG, null, epoch, DiagnosticLineFormat.CLIENT_SIDE, sessionId, "epoch", "info")} utc={ReadUtcStamp()} reason=client_diagnostics_started";
                }
                line = FormattableString.Invariant(
                    $"{DiagnosticLineFormat.Header(DiagnosticLineFormat.BASE_POWER_TAG, ++nextSequence, epoch, DiagnosticLineFormat.CLIENT_SIDE, sessionId, eventName, outcome)} base={DiagnosticLineFormat.NOT_APPLICABLE} source={DiagnosticLineFormat.ShortId(sourceId, true)} power={shown.Power:F2}/{shown.MaxPower:F2} initial={DiagnosticLineFormat.NOT_APPLICABLE} wait={DiagnosticLineFormat.NOT_APPLICABLE}{(occurrences > 1 ? $" rep={DiagnosticLineFormat.Number(occurrences)}" : "")} reason={DiagnosticLineFormat.Token($"{shown.SourceType}_{reason}", 64)}");
                if (problems.Count == PROBLEM_CAPACITY)
                {
                    problems.Dequeue();
                }
                problems.Enqueue(line);
            }
            if (marker != null)
            {
                Write(marker);
            }
            Write(line, warning);
            return line;
        }
        catch (Exception)
        {
            Interlocked.Increment(ref ownFailures);
            return null;
        }
    }

    /// <summary>
    ///     Rejection reasons quote the offending value. Repeats are recognised by the reason without its numbers.
    /// </summary>
    private static string WithoutDigits(string? reason)
    {
        if (string.IsNullOrEmpty(reason))
        {
            return "";
        }
        char[] kept = new char[reason.Length];
        int length = 0;
        foreach (char character in reason)
        {
            if (!char.IsDigit(character))
            {
                kept[length++] = character;
            }
        }
        return new string(kept, 0, length);
    }

    private SessionId? ReadLocalSessionId()
    {
        try
        {
            return localSessionId();
        }
        catch (Exception)
        {
            // The session is torn down while late packets are still being applied. The event is still worth a line.
            return null;
        }
    }

    private string ReadUtcStamp()
    {
        try
        {
            return DiagnosticLineFormat.UtcStamp(utcNow());
        }
        catch (Exception)
        {
            return DiagnosticLineFormat.NOT_APPLICABLE;
        }
    }

    /// <summary>
    ///     Writes a line without letting a failing log sink undo the bookkeeping already done for the event.
    /// </summary>
    private void Write(string line, bool warning = false)
    {
        try
        {
            if (warning)
            {
                Log.Warn(line);
            }
            else
            {
                Log.Info(line);
            }
        }
        catch (Exception)
        {
            Interlocked.Increment(ref ownFailures);
        }
    }

    private string FormatLine(long sequence, SessionId? sessionId, string eventName, string outcome, NitroxId? baseId, NitroxId? sourceId,
        float power, float maxPower, bool initialSyncCompleted, bool waitScreenWaiting, string reason) =>
        FormattableString.Invariant(
            $"{DiagnosticLineFormat.Header(DiagnosticLineFormat.BASE_POWER_TAG, sequence, epoch, DiagnosticLineFormat.CLIENT_SIDE, sessionId, eventName, outcome)} base={DiagnosticLineFormat.ShortId(baseId, true)} source={DiagnosticLineFormat.ShortId(sourceId, true)} power={power:F2}/{maxPower:F2} initial={(initialSyncCompleted ? 1 : 0)} wait={(waitScreenWaiting ? 1 : 0)} reason={DiagnosticLineFormat.Token(reason)}");
}
