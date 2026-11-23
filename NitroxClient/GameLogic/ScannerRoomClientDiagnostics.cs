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
///     Compact client-side evidence for Scanner Room requests and canonical applies.
///     It intentionally omits movement, and battery and health that stay within their band.
///     <p />
///     Line shape (see <see cref="DiagnosticLineFormat" />):
///     <c>[SRD1] n= ep= side=C sid= ev= out= room= cam= slot= rev= reason=</c>. <c>sid</c> is this client's own
///     session id, the same number the server prints for this player's requests. <c>rev</c> is the revision of the
///     preview a preview event is about. Outcomes: <c>ok</c> (applied or sent), <c>reject</c> (refused by the server or
///     by this client), <c>stale</c> (older than what is already applied), <c>pending</c> (waiting for a game object),
///     <c>diverge</c> (the game object does not match the server's state). Everything but <c>ok</c> is thinned out
///     when it repeats and then says how often it happened (<c>rep=</c>).
/// </summary>
public sealed class ScannerRoomClientDiagnostics
{
    // Guesses, not measurements: enough lines to follow a test step, and a fixed bound on what a long session keeps
    // in memory.
    internal const int HISTORY_CAPACITY = 128;
    private const int REPEAT_KEY_CAPACITY = 128;

    private readonly Queue<string> history = new(HISTORY_CAPACITY);
    private readonly DiagnosticRepeatSampler repeatSampler = new(REPEAT_KEY_CAPACITY);
    private readonly Dictionary<NitroxId, (int Energy, int Health)> componentBands = [];
    private readonly object sync = new();
    private readonly string epoch = DiagnosticLineFormat.NewEpoch();
    private readonly Func<SessionId?> localSessionId;
    private readonly Func<DateTimeOffset> utcNow;
    private bool epochAnnounced;
    private long nextSequence;
    private long ownFailures;

    internal string Epoch => epoch;

    /// <summary>
    ///     How often recording or writing a line failed. Such a failure is swallowed, because these calls sit in the
    ///     middle of the game code they observe.
    /// </summary>
    internal long OwnFailures => Interlocked.Read(ref ownFailures);

    public ScannerRoomClientDiagnostics() : this(static () => null, static () => DateTimeOffset.UtcNow)
    {
    }

    /// <summary>
    ///     Used by the client's service container, so every line can carry the session id the server knows this
    ///     player by.
    /// </summary>
    public ScannerRoomClientDiagnostics(IMultiplayerSession multiplayerSession) : this(() => multiplayerSession.Reservation?.SessionId,
        static () => DateTimeOffset.UtcNow)
    {
    }

    internal ScannerRoomClientDiagnostics(Func<SessionId?> localSessionId, Func<DateTimeOffset> utcNow)
    {
        this.localSessionId = localSessionId;
        this.utcNow = utcNow;
    }

    /// <summary>
    ///     Never throws: a failure in here must not abort the dock, control or restore step that reported the event.
    /// </summary>
    internal string Record(string eventName, string outcome, NitroxId? cameraId = null, NitroxId? roomId = null,
        long? revision = null, int? slot = null, string? reason = null)
    {
        try
        {
            return RecordCore(eventName, outcome, cameraId, roomId, revision, slot, reason);
        }
        catch (Exception)
        {
            Interlocked.Increment(ref ownFailures);
            return DiagnosticLineFormat.Header(DiagnosticLineFormat.SCANNER_ROOM_TAG, null, epoch, DiagnosticLineFormat.CLIENT_SIDE, null, eventName, "diagnostics_failed");
        }
    }

    private string RecordCore(string eventName, string outcome, NitroxId? cameraId, NitroxId? roomId, long? revision, int? slot, string? reason)
    {
        SessionId? sessionId = ReadLocalSessionId();
        string line;
        lock (sync)
        {
            AnnounceEpoch(sessionId);
            long sequence = ++nextSequence;
            string room = DiagnosticLineFormat.ShortId(roomId, false);
            string camera = DiagnosticLineFormat.ShortId(cameraId, true);
            string reasonToken = DiagnosticLineFormat.Token(reason);
            // A transition that went through is always written. Anything else can repeat for as long as its cause
            // lasts (a rejection on every packet, a camera object that never arrives), so its repeats are thinned out.
            long occurrences = 1;
            bool write = outcome == "ok" || repeatSampler.Observe($"{eventName}|{outcome}|{room}|{camera}|{DiagnosticLineFormat.Number(slot)}|{reasonToken}", out occurrences);
            line = $"{DiagnosticLineFormat.Header(DiagnosticLineFormat.SCANNER_ROOM_TAG, sequence, epoch, DiagnosticLineFormat.CLIENT_SIDE, sessionId, eventName, outcome)} room={room} cam={camera} slot={DiagnosticLineFormat.Number(slot)} rev={DiagnosticLineFormat.Number(revision)}{(occurrences > 1 ? $" rep={DiagnosticLineFormat.Number(occurrences)}" : "")} reason={reasonToken}";
            if (!write)
            {
                return line;
            }
            if (history.Count == HISTORY_CAPACITY)
            {
                history.Dequeue();
            }
            history.Enqueue(line);
            Write(line, outcome is "reject" or "diverge");
        }
        return line;
    }

    internal void RecordComponentApplied(NitroxId cameraId, float energy, float health, bool objectFound)
    {
        int energyBand = EnergyBand(energy);
        int healthBand = HealthBand(health);
        bool shouldRecord;
        lock (sync)
        {
            shouldRecord = !componentBands.TryGetValue(cameraId, out (int Energy, int Health) previous) ||
                           previous.Energy != energyBand || previous.Health != healthBand;
            componentBands[cameraId] = (energyBand, healthBand);
        }
        if (shouldRecord)
        {
            Record("component_apply", objectFound ? "ok" : "diverge", cameraId,
                reason: objectFound ? $"e{energyBand}_h{healthBand}" : "missing_object");
        }
    }

    internal IReadOnlyList<string> GetHistory()
    {
        lock (sync)
        {
            return history.ToArray();
        }
    }

    internal static int EnergyBand(float energy) => MapRoomCameraMetadata.GetComponentBand(energy, MapRoomCameraMetadata.MAX_ENERGY);

    internal static int HealthBand(float health) => MapRoomCameraMetadata.GetComponentBand(health, MapRoomCameraMetadata.MAX_HEALTH);

    /// <summary>
    ///     Writes one marker before the first event of a session. Its UTC stamp, next to the log's own local time,
    ///     lines this log up with the server log and the other client's log; its <c>sid</c> says which of the server's
    ///     players this log belongs to.
    /// </summary>
    private void AnnounceEpoch(SessionId? sessionId)
    {
        if (epochAnnounced)
        {
            return;
        }
        epochAnnounced = true;
        Write($"{DiagnosticLineFormat.Header(DiagnosticLineFormat.SCANNER_ROOM_TAG, null, epoch, DiagnosticLineFormat.CLIENT_SIDE, sessionId, "epoch", "info")} utc={ReadUtcStamp()} reason=client_diagnostics_started", false);
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
    private void Write(string line, bool warning)
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
}
