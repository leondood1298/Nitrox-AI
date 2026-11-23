using System;
using System.Collections.Generic;
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
///     whether the loading screen was still up, because both events are only of interest while a player joins.
/// </summary>
public sealed class BasePowerClientDiagnostics
{
    internal const int SOURCE_HISTORY_CAPACITY = 48;
    internal const int AUDIO_HISTORY_CAPACITY = 16;
    internal const int HISTORY_CAPACITY = SOURCE_HISTORY_CAPACITY + AUDIO_HISTORY_CAPACITY + 2;

    private readonly string epoch = DiagnosticLineFormat.NewEpoch();
    private readonly List<string> history = new(HISTORY_CAPACITY);
    private readonly object sync = new();
    private readonly Func<SessionId?> localSessionId;
    private readonly Func<DateTimeOffset> utcNow;
    private bool epochAnnounced;
    private int audioEntries;
    private int sourceEntries;
    private bool audioTruncated;
    private bool sourceTruncated;

    internal string Epoch => epoch;

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

    internal IReadOnlyList<string> GetHistory()
    {
        lock (sync)
        {
            return history.ToArray();
        }
    }

    private string? Record(bool audio, string eventName, string outcome, NitroxId? baseId, NitroxId? sourceId,
        float power, float maxPower, bool initialSyncCompleted, bool waitScreenWaiting, string reason)
    {
        SessionId? sessionId = localSessionId();
        string line;
        string? marker = null;
        lock (sync)
        {
            if (!epochAnnounced)
            {
                epochAnnounced = true;
                marker = $"{DiagnosticLineFormat.Header(DiagnosticLineFormat.BASE_POWER_TAG, null, epoch, DiagnosticLineFormat.CLIENT_SIDE, sessionId, "epoch", "info")} utc={DiagnosticLineFormat.UtcStamp(utcNow())} reason=client_diagnostics_started";
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
                line = FormatLine(history.Count + 1, sessionId, audio ? "audio_trace_limit" : "source_trace_limit",
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

                line = FormatLine(history.Count + 1, sessionId, eventName, outcome, baseId, sourceId, power, maxPower,
                    initialSyncCompleted, waitScreenWaiting, reason);
                history.Add(line);
            }
        }
        if (marker != null)
        {
            // Its UTC stamp, next to the log's own local time, lines this log up with the server's and the other client's.
            Log.Info(marker);
        }
        Log.Info(line);
        return line;
    }

    private string FormatLine(int sequence, SessionId? sessionId, string eventName, string outcome, NitroxId? baseId, NitroxId? sourceId,
        float power, float maxPower, bool initialSyncCompleted, bool waitScreenWaiting, string reason) =>
        FormattableString.Invariant(
            $"{DiagnosticLineFormat.Header(DiagnosticLineFormat.BASE_POWER_TAG, sequence, epoch, DiagnosticLineFormat.CLIENT_SIDE, sessionId, eventName, outcome)} base={DiagnosticLineFormat.ShortId(baseId, true)} source={DiagnosticLineFormat.ShortId(sourceId, true)} power={power:F2}/{maxPower:F2} initial={(initialSyncCompleted ? 1 : 0)} wait={(waitScreenWaiting ? 1 : 0)} reason={DiagnosticLineFormat.Token(reason)}");
}
