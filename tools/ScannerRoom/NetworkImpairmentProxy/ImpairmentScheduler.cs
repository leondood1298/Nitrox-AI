namespace ScannerRoom.NetworkImpairmentProxy;

internal sealed record ImpairmentSettings(
    int DelayMilliseconds,
    int JitterMilliseconds,
    int LossBasisPoints,
    int ReorderEvery,
    int ReorderHoldMilliseconds,
    int MaximumQueuedPackets);

internal enum ScheduleOutcome
{
    Queued,
    HeldForReorder,
    ImpairmentDrop,
    QueueOverflowDrop
}

internal readonly record struct ScheduledItem<T>(T Value, long DueMilliseconds);

internal sealed class ImpairmentScheduler<T>
{
    private sealed record PendingItem(T Value, long NaturalDueMilliseconds, long AcceptedSequence);
    private sealed record HeldItem(PendingItem Item, long ExpiresMilliseconds);

    private readonly IMonotonicClock clock;
    private readonly ImpairmentSettings settings;
    private readonly DeterministicRandom random;
    private readonly PriorityQueue<PendingItem, (long Due, long Order)> queue = new();
    private HeldItem? held;
    private long queueOrder;
    private long acceptedSequence;

    public ImpairmentScheduler(IMonotonicClock clock, ImpairmentSettings settings, ulong seed)
    {
        this.clock = clock;
        this.settings = settings;
        random = new DeterministicRandom(seed);
    }

    public long ReceivedPackets { get; private set; }
    public long ImpairmentDroppedPackets { get; private set; }
    public long OverflowDroppedPackets { get; private set; }
    public long ReorderedPairs { get; private set; }
    public long ExpiredReorderHolds { get; private set; }
    public int QueuedPackets => queue.Count + (held is null ? 0 : 1);

    public ScheduleOutcome Enqueue(T value)
    {
        FlushExpiredHold();
        ReceivedPackets++;

        if (QueuedPackets >= settings.MaximumQueuedPackets)
        {
            OverflowDroppedPackets++;
            return ScheduleOutcome.QueueOverflowDrop;
        }

        if (settings.LossBasisPoints > 0 && random.Next(10_000) < settings.LossBasisPoints)
        {
            ImpairmentDroppedPackets++;
            return ScheduleOutcome.ImpairmentDrop;
        }

        long sequence = ++acceptedSequence;
        long due = checked(clock.Milliseconds + settings.DelayMilliseconds + NextJitter());
        PendingItem item = new(value, due, sequence);

        if (held is not null)
        {
            long pairDue = Math.Max(clock.Milliseconds, Math.Max(due, held.Item.NaturalDueMilliseconds));
            Queue(item, pairDue);
            Queue(held.Item, pairDue);
            held = null;
            ReorderedPairs++;
            return ScheduleOutcome.Queued;
        }

        if (settings.ReorderEvery > 0 && sequence % settings.ReorderEvery == 0)
        {
            held = new HeldItem(item, checked(clock.Milliseconds + settings.ReorderHoldMilliseconds));
            return ScheduleOutcome.HeldForReorder;
        }

        Queue(item, due);
        return ScheduleOutcome.Queued;
    }

    public IReadOnlyList<ScheduledItem<T>> DrainDue()
    {
        FlushExpiredHold();
        if (queue.Count == 0)
        {
            return Array.Empty<ScheduledItem<T>>();
        }

        List<ScheduledItem<T>> due = new();
        while (queue.TryPeek(out _, out (long Due, long Order) priority) && priority.Due <= clock.Milliseconds)
        {
            PendingItem item = queue.Dequeue();
            due.Add(new ScheduledItem<T>(item.Value, priority.Due));
        }
        return due;
    }

    public long? MillisecondsUntilNext()
    {
        FlushExpiredHold();
        long? next = null;
        if (queue.TryPeek(out _, out (long Due, long Order) priority))
        {
            next = priority.Due;
        }
        if (held is not null)
        {
            next = next is null ? held.ExpiresMilliseconds : Math.Min(next.Value, held.ExpiresMilliseconds);
        }
        return next is null ? null : Math.Max(0, next.Value - clock.Milliseconds);
    }

    private int NextJitter()
    {
        if (settings.JitterMilliseconds == 0)
        {
            return 0;
        }
        return random.Next(checked(settings.JitterMilliseconds * 2 + 1)) - settings.JitterMilliseconds;
    }

    private void FlushExpiredHold()
    {
        if (held is null || held.ExpiresMilliseconds > clock.Milliseconds)
        {
            return;
        }

        Queue(held.Item, Math.Max(held.Item.NaturalDueMilliseconds, clock.Milliseconds));
        held = null;
        ExpiredReorderHolds++;
    }

    private void Queue(PendingItem item, long dueMilliseconds)
    {
        queue.Enqueue(item, (dueMilliseconds, queueOrder++));
    }
}
