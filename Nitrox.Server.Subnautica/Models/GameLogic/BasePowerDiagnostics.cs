using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging.Abstractions;
using Nitrox.Model.Core;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;
using Nitrox.Model.Subnautica.Helper;
using Nitrox.Server.Subnautica.Models.GameLogic.Entities;

namespace Nitrox.Server.Subnautica.Models.GameLogic;

/// <summary>
///     Server-side evidence for base power. A power source's level is whatever the player simulating it reports, and
///     what other players consume from it is routed to that player, so nearly every base power failure is a question
///     of who reported what for which source, and when. This keeps that per source for the <c>basepower</c> command
///     and writes the few events worth a log line.
///     <p />
///     Line shape (see <see cref="DiagnosticLineFormat" />):
///     <c>[BPD1] n= ep= side=S sid= ev= out= source= type= power=current/max rev= owner= amount= total= reason=</c>.
///     <c>sid</c> is the player the packet came from, <c>owner</c> who simulates the source at that moment
///     (<c>none</c>: nobody). <c>n</c> counts every update and consumption packet, also those without a line.
/// </summary>
internal sealed class BasePowerDiagnostics(ILogger<BasePowerDiagnostics>? logger = null, SimulationOwnershipData? simulationOwnershipData = null,
    TimeProvider? timeProvider = null)
{
    public const string PREFIX = DiagnosticLineFormat.BASE_POWER_TAG;

    /// <summary>
    ///     Sources are identified by ids a client sends, so the per-source bookkeeping must not grow without limit.
    /// </summary>
    internal const int SOURCE_CAPACITY = 512;

    /// <summary>
    ///     Session ids are handed out again after a disconnect, so a source only ever sees a few consumers.
    /// </summary>
    internal const int CONSUMER_CAPACITY = 16;

    internal const string NO_OWNER = "none";
    internal const string UNKNOWN_OWNER = "?";
    private const int REPEAT_KEY_CAPACITY = 256;
    private const int REASON_LENGTH = 64;

    private readonly ILogger logger = logger ?? NullLogger<BasePowerDiagnostics>.Instance;
    private readonly TimeProvider timeProvider = timeProvider ?? TimeProvider.System;
    private readonly object sync = new();
    private readonly string epoch = DiagnosticLineFormat.NewEpoch();
    private readonly Dictionary<NitroxId, SourceState> sources = [];
    private readonly DiagnosticRepeatSampler repeatSampler = new(REPEAT_KEY_CAPACITY);
    private volatile bool traceEnabled;
    private bool epochAnnounced;
    private long nextSequence;
    private long acceptedUpdates;
    private long rejectedUpdates;
    private long ownFailures;
    private long untrackedSourceEvents;

    public bool TraceEnabled
    {
        get => traceEnabled;
        set => traceEnabled = value;
    }

    public long AcceptedUpdates => Interlocked.Read(ref acceptedUpdates);
    public long RejectedUpdates => Interlocked.Read(ref rejectedUpdates);

    internal string Epoch => epoch;

    /// <summary>
    ///     How often recording or writing a line failed. Such a failure is swallowed, because these calls sit in the
    ///     middle of the packet processors they observe.
    /// </summary>
    internal long OwnFailures => Interlocked.Read(ref ownFailures);

    public void RecordAccepted() => Interlocked.Increment(ref acceptedUpdates);

    public void RecordRejected() => Interlocked.Increment(ref rejectedUpdates);

    /// <summary>
    ///     Records an update the source's owner got accepted. Only the first update of an owner and a revision that
    ///     did not advance get a line: an owner reports about once a second per source.
    /// </summary>
    public void RecordAccepted(NitroxId sourceId, SessionId ownerSessionId, PowerSourceMetadata accepted)
    {
        Interlocked.Increment(ref acceptedUpdates);
        try
        {
            lock (sync)
            {
                long sequence = ++nextSequence;
                SourceState? state = GetSource(sourceId, true);
                if (state == null)
                {
                    return;
                }

                bool hadUpdate = state.HasAcceptedUpdate;
                long previousRevision = state.Revision;
                SessionId? previousOwner = state.LastOwner;
                state.Apply(accepted);
                state.HasAcceptedUpdate = true;
                state.AcceptedUpdates++;
                state.LastOwner = ownerSessionId;
                state.LastAcceptedTimestamp = timeProvider.GetTimestamp();

                if (!hadUpdate || previousOwner != ownerSessionId)
                {
                    string reason = previousOwner is { } previous && previous != ownerSessionId
                        ? $"owner_changed_from_{DiagnosticLineFormat.Session(previous)}"
                        : "first_update";
                    Write(LogLevel.Information, FormatLine(sequence, ownerSessionId, "source_update", "ok", state, null, null, 1, reason));
                }
                if (hadUpdate && accepted.Revision <= previousRevision)
                {
                    // Clients drop anything older than the revision they hold, so from here on they ignore this source.
                    // The authority keeps a revision floor per id to rule this out; this line means the floor failed.
                    state.RevisionRegressions++;
                    if (repeatSampler.Observe($"regress|{sourceId}", out long occurrences))
                    {
                        Write(LogLevel.Warning, FormatLine(sequence, ownerSessionId, "source_update", "invariant", state, null, null, occurrences,
                            $"revision_{accepted.Revision}_after_{previousRevision}"));
                    }
                }
            }
        }
        catch (Exception)
        {
            Interlocked.Increment(ref ownFailures);
        }
    }

    /// <summary>
    ///     Records an update that was refused. A client that believes it simulates a source sends one every second
    ///     until it is told otherwise, so repeats are thinned out, and since this is how the server answers such a
    ///     client by design, the line is information, not a warning.
    /// </summary>
    /// <param name="canonical">What the server holds for the source, which is also what the sender is told to restore.</param>
    public void RecordRejected(NitroxId sourceId, SessionId senderSessionId, PowerSourceMetadata canonical, string reason)
    {
        Interlocked.Increment(ref rejectedUpdates);
        try
        {
            lock (sync)
            {
                long sequence = ++nextSequence;
                SourceState? state = GetSource(sourceId, canonical.SourceType != BasePowerSourceType.UNKNOWN);
                if (state != null)
                {
                    if (!state.HasAcceptedUpdate)
                    {
                        state.Apply(canonical);
                    }
                    state.RejectedUpdates++;
                    state.LastRejection = DiagnosticLineFormat.Token(reason, REASON_LENGTH);
                }
                if (repeatSampler.Observe($"update|{senderSessionId}|{sourceId}|{WithoutDigits(reason)}", out long occurrences))
                {
                    Write(LogLevel.Information, FormatLine(sequence, senderSessionId, "source_update", "reject", state, sourceId, null, occurrences, reason,
                        canonical));
                }
            }
        }
        catch (Exception)
        {
            Interlocked.Increment(ref ownFailures);
        }
    }

    /// <summary>
    ///     Records energy a player reported as consumed from a source it does not simulate, and what became of the
    ///     report. A consumer reports several times a second, so a line is written for the first two reports of a
    ///     kind and then for every power of two; <c>total=</c> carries what the reports in between added up to.
    /// </summary>
    /// <param name="stored">What the server holds for the source after routing the report.</param>
    public void RecordConsumption(NitroxId sourceId, SessionId consumerSessionId, SessionId? ownerSessionId, float amount, ConsumptionRoute route,
        PowerSourceMetadata? stored, string? rejectionReason)
    {
        if (route == ConsumptionRoute.REJECTED)
        {
            Interlocked.Increment(ref rejectedUpdates);
        }
        try
        {
            lock (sync)
            {
                long sequence = ++nextSequence;
                SourceState? state = GetSource(sourceId, stored != null && stored.SourceType != BasePowerSourceType.UNKNOWN);
                ConsumerState? consumer = state?.GetOrAddConsumer(consumerSessionId);
                float total = 0f;
                if (state != null && stored != null && (route == ConsumptionRoute.APPLIED_BY_SERVER || !state.HasAcceptedUpdate))
                {
                    state.Apply(stored);
                }
                if (consumer != null)
                {
                    total = consumer.Add(route, amount);
                }

                string outcome = route switch
                {
                    ConsumptionRoute.FORWARD_TO_OWNER => "forward",
                    ConsumptionRoute.APPLIED_BY_SERVER => "server",
                    _ => "reject"
                };
                string reason = route switch
                {
                    ConsumptionRoute.FORWARD_TO_OWNER => "sent_to_owner_to_apply",
                    ConsumptionRoute.APPLIED_BY_SERVER => "no_owner_applied_to_stored_power",
                    _ => rejectionReason ?? ""
                };
                string reasonKey = route == ConsumptionRoute.REJECTED ? WithoutDigits(reason) : "";
                if (repeatSampler.Observe($"consume|{consumerSessionId}|{sourceId}|{outcome}|{ownerSessionId}|{reasonKey}", out long occurrences))
                {
                    Write(LogLevel.Information, FormatLine(sequence, consumerSessionId, "consume", outcome, state, sourceId, (amount, total), occurrences, reason, stored));
                }
            }
        }
        catch (Exception)
        {
            Interlocked.Increment(ref ownFailures);
        }
    }

    /// <summary>
    ///     Records that a player gave the simulation of a power source back because it cannot simulate it (the source
    ///     does not exist on that client), and whether another player took it over. Entities that are not power
    ///     sources are ignored: the hand-back request is the same for every kind of entity.
    /// </summary>
    /// <param name="released">False when the request was refused: the sender did not hold a plain simulation lock.</param>
    /// <param name="takenOver">True when another player simulates the source now. The line's <c>owner</c> names them.</param>
    public void RecordHandBack(Entity entity, SessionId senderSessionId, bool released, bool takenOver)
    {
        try
        {
            if (entity.Metadata is not PowerSourceMetadata stored)
            {
                return;
            }
            lock (sync)
            {
                long sequence = ++nextSequence;
                SourceState? state = GetSource(entity.Id, true);
                if (state != null)
                {
                    if (!state.HasAcceptedUpdate)
                    {
                        state.Apply(stored);
                    }
                    if (released)
                    {
                        state.HandBacks++;
                    }
                }
                string outcome = released ? "ok" : "reject";
                string reason = !released ? "sender_holds_no_plain_simulation_lock" : takenOver ? "taken_over_by_owner" : "nobody_else_can_simulate";
                if (repeatSampler.Observe($"handback|{senderSessionId}|{entity.Id}|{reason}", out long occurrences))
                {
                    Write(LogLevel.Information, FormatLine(sequence, senderSessionId, "source_handback", outcome, state, entity.Id, null, occurrences, reason, stored));
                }
            }
        }
        catch (Exception)
        {
            Interlocked.Increment(ref ownFailures);
        }
    }

    /// <summary>
    ///     Writes a line a tester asked for, to bracket a test step in the log.
    /// </summary>
    public void RecordMark(string label)
    {
        try
        {
            lock (sync)
            {
                Write(LogLevel.Information, FormatMarker("mark", label));
            }
        }
        catch (Exception)
        {
            Interlocked.Increment(ref ownFailures);
        }
    }

    /// <summary>
    ///     What was recorded per source, for the <c>basepower</c> command.
    /// </summary>
    public IReadOnlyDictionary<NitroxId, BasePowerSourceDiagnostics> GetSources()
    {
        lock (sync)
        {
            return sources.ToDictionary(pair => pair.Key, pair => pair.Value.ToSnapshot(timeProvider));
        }
    }

    public BasePowerDiagnosticTotals GetTotals()
    {
        lock (sync)
        {
            long forwarded = 0;
            long serverApplied = 0;
            long rejectedConsumption = 0;
            float forwardedAmount = 0f;
            float serverAppliedAmount = 0f;
            long regressions = 0;
            foreach (SourceState state in sources.Values)
            {
                regressions += state.RevisionRegressions;
                foreach (ConsumerState consumer in state.Consumers.Values)
                {
                    forwarded += consumer.Forwarded;
                    forwardedAmount += consumer.ForwardedAmount;
                    serverApplied += consumer.ServerApplied;
                    serverAppliedAmount += consumer.ServerAppliedAmount;
                    rejectedConsumption += consumer.Rejected;
                }
            }
            return new BasePowerDiagnosticTotals(nextSequence, AcceptedUpdates, RejectedUpdates, forwarded, forwardedAmount, serverApplied,
                serverAppliedAmount, rejectedConsumption, regressions, Interlocked.Read(ref untrackedSourceEvents));
        }
    }

    /// <summary>
    ///     Who simulates <paramref name="sourceId" /> right now, in the form used by the <c>owner</c> field.
    /// </summary>
    internal string DescribeOwner(NitroxId sourceId)
    {
        if (simulationOwnershipData == null)
        {
            return UNKNOWN_OWNER;
        }
        return simulationOwnershipData.TryGetLock(sourceId, out SimulationOwnershipData.PlayerLock playerLock)
            ? DiagnosticLineFormat.Session(playerLock.Player.SessionId)
            : NO_OWNER;
    }

    /// <param name="knownToServer">
    ///     False for an id the server has no power source for. Such ids come straight from a client and must not use
    ///     up the bookkeeping.
    /// </param>
    private SourceState? GetSource(NitroxId sourceId, bool knownToServer)
    {
        if (sources.TryGetValue(sourceId, out SourceState? state))
        {
            return state;
        }
        if (!knownToServer)
        {
            return null;
        }
        if (sources.Count >= SOURCE_CAPACITY)
        {
            Interlocked.Increment(ref untrackedSourceEvents);
            return null;
        }
        state = new SourceState(sourceId);
        sources.Add(sourceId, state);
        return state;
    }

    private string FormatLine(long sequence, SessionId sessionId, string eventName, string outcome, SourceState? state, NitroxId? sourceId,
        (float Amount, float Total)? consumption, long occurrences, string reason, PowerSourceMetadata? shown = null)
    {
        NitroxId? id = state?.Id ?? sourceId;
        BasePowerSourceType type = shown?.SourceType ?? state?.Type ?? BasePowerSourceType.UNKNOWN;
        float power = shown?.Power ?? state?.Power ?? 0f;
        float maxPower = shown?.MaxPower ?? state?.MaxPower ?? 0f;
        long revision = shown?.Revision ?? state?.Revision ?? 0;
        string amount = consumption is { } consumed
            ? FormattableString.Invariant($"{consumed.Amount:F3} total={consumed.Total:F3}")
            : $"{DiagnosticLineFormat.NOT_APPLICABLE} total={DiagnosticLineFormat.NOT_APPLICABLE}";
        return FormattableString.Invariant(
            $"{DiagnosticLineFormat.Header(PREFIX, sequence, epoch, DiagnosticLineFormat.SERVER_SIDE, sessionId, eventName, outcome)} source={DiagnosticLineFormat.ShortId(id, true)} type={DiagnosticLineFormat.Token(type.ToString())} power={power:F2}/{maxPower:F2} rev={revision} owner={(id == null ? DiagnosticLineFormat.NOT_APPLICABLE : DescribeOwner(id))} amount={amount}{(occurrences > 1 ? $" rep={DiagnosticLineFormat.Number(occurrences)}" : "")} reason={DiagnosticLineFormat.Token(reason, REASON_LENGTH)}");
    }

    private string FormatMarker(string eventName, string reason) =>
        $"{DiagnosticLineFormat.Header(PREFIX, null, epoch, DiagnosticLineFormat.SERVER_SIDE, null, eventName, "info")} utc={DiagnosticLineFormat.UtcStamp(timeProvider.GetUtcNow())} reason={DiagnosticLineFormat.Token(reason, REASON_LENGTH)}";

    /// <summary>
    ///     Writes a line without letting a failing log sink undo the bookkeeping already done for the event. The first
    ///     line of a server run is preceded by a marker: its UTC stamp next to the logger's own local time is what
    ///     lets this log be lined up with the client logs, which carry the same kind of marker.
    /// </summary>
    private void Write(LogLevel level, string line)
    {
        try
        {
            if (!epochAnnounced)
            {
                epochAnnounced = true;
                string marker = FormatMarker("epoch", "server_diagnostics_started");
                logger.ZLogInformation($"{marker}");
            }
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
    ///     Rejection reasons quote the offending value ("power 250.3 is outside 0-250"). Repeats are recognised by the
    ///     reason without its numbers, or every packet with a slightly different value would count as a new reason.
    /// </summary>
    private static string WithoutDigits(string? reason) =>
        string.IsNullOrEmpty(reason) ? "" : string.Concat(reason.Where(character => !char.IsDigit(character)));

    private sealed class SourceState(NitroxId id)
    {
        public NitroxId Id { get; } = id;
        public BasePowerSourceType Type { get; private set; }
        public float Power { get; private set; }
        public float MaxPower { get; private set; }
        public long Revision { get; private set; }
        public bool HasAcceptedUpdate { get; set; }
        public SessionId? LastOwner { get; set; }
        public long LastAcceptedTimestamp { get; set; }
        public long AcceptedUpdates { get; set; }
        public long RejectedUpdates { get; set; }
        public string? LastRejection { get; set; }
        public long RevisionRegressions { get; set; }
        public long HandBacks { get; set; }
        public Dictionary<SessionId, ConsumerState> Consumers { get; } = [];

        public void Apply(PowerSourceMetadata metadata)
        {
            Type = metadata.SourceType;
            Power = metadata.Power;
            MaxPower = metadata.MaxPower;
            Revision = metadata.Revision;
        }

        public ConsumerState? GetOrAddConsumer(SessionId sessionId)
        {
            if (Consumers.TryGetValue(sessionId, out ConsumerState? consumer))
            {
                return consumer;
            }
            if (Consumers.Count >= CONSUMER_CAPACITY)
            {
                return null;
            }
            consumer = new ConsumerState();
            Consumers.Add(sessionId, consumer);
            return consumer;
        }

        public BasePowerSourceDiagnostics ToSnapshot(TimeProvider timeProvider) => new(
            HasAcceptedUpdate ? LastOwner : null,
            HasAcceptedUpdate ? timeProvider.GetElapsedTime(LastAcceptedTimestamp) : null,
            AcceptedUpdates,
            RejectedUpdates,
            LastRejection,
            RevisionRegressions,
            Consumers.OrderBy(pair => pair.Key)
                     .Select(pair => new BasePowerConsumerDiagnostics(pair.Key, pair.Value.Forwarded, pair.Value.ForwardedAmount,
                         pair.Value.ServerApplied, pair.Value.ServerAppliedAmount, pair.Value.Rejected))
                     .ToArray(),
            HandBacks);
    }

    private sealed class ConsumerState
    {
        public long Forwarded { get; private set; }
        public float ForwardedAmount { get; private set; }
        public long ServerApplied { get; private set; }
        public float ServerAppliedAmount { get; private set; }
        public long Rejected { get; private set; }

        /// <returns>What this consumer's reports of the same kind add up to so far.</returns>
        public float Add(ConsumptionRoute route, float amount)
        {
            // A rejected amount may be the very reason for the rejection (NaN, negative), so it is counted, not summed.
            float accepted = float.IsFinite(amount) && amount > 0f ? amount : 0f;
            switch (route)
            {
                case ConsumptionRoute.FORWARD_TO_OWNER:
                    Forwarded++;
                    ForwardedAmount += accepted;
                    return ForwardedAmount;
                case ConsumptionRoute.APPLIED_BY_SERVER:
                    ServerApplied++;
                    ServerAppliedAmount += accepted;
                    return ServerAppliedAmount;
                default:
                    Rejected++;
                    return 0f;
            }
        }
    }
}

/// <param name="LastOwner">The player whose update was accepted last; null when no update was accepted yet.</param>
/// <param name="SinceLastUpdate">
///     Time since that update. A live owner reports at least every thirty seconds even when nothing changes, so a
///     larger value means the owner's simulation of the source is not running.
/// </param>
internal readonly record struct BasePowerSourceDiagnostics(
    SessionId? LastOwner,
    TimeSpan? SinceLastUpdate,
    long AcceptedUpdates,
    long RejectedUpdates,
    string? LastRejection,
    long RevisionRegressions,
    IReadOnlyList<BasePowerConsumerDiagnostics> Consumers,
    long HandBacks = 0);

internal readonly record struct BasePowerConsumerDiagnostics(
    SessionId Consumer,
    long Forwarded,
    float ForwardedAmount,
    long ServerApplied,
    float ServerAppliedAmount,
    long Rejected);

internal readonly record struct BasePowerDiagnosticTotals(
    long Events,
    long AcceptedUpdates,
    long RejectedUpdates,
    long ConsumptionForwarded,
    float ConsumptionForwardedAmount,
    long ConsumptionAppliedByServer,
    float ConsumptionAppliedByServerAmount,
    long ConsumptionRejected,
    long RevisionRegressions,
    long UntrackedSourceEvents);
