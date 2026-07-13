using System.Threading;

namespace Nitrox.Server.Subnautica.Models.GameLogic;

internal sealed class BasePowerDiagnostics
{
    private long acceptedUpdates;
    private long rejectedUpdates;

    public bool TraceEnabled { get; set; }
    public long AcceptedUpdates => Interlocked.Read(ref acceptedUpdates);
    public long RejectedUpdates => Interlocked.Read(ref rejectedUpdates);

    public void RecordAccepted() => Interlocked.Increment(ref acceptedUpdates);
    public void RecordRejected() => Interlocked.Increment(ref rejectedUpdates);
}
