using System.Threading;

namespace Nitrox.Server.Subnautica.Models.GameLogic;

internal sealed class VehicleDiagnostics
{
    private long acceptedMovements;
    private long rejectedMovements;
    private long acceptedActions;
    private long rejectedActions;

    public bool TraceEnabled { get; set; }
    public long AcceptedMovements => Interlocked.Read(ref acceptedMovements);
    public long RejectedMovements => Interlocked.Read(ref rejectedMovements);
    public long AcceptedActions => Interlocked.Read(ref acceptedActions);
    public long RejectedActions => Interlocked.Read(ref rejectedActions);

    public void RecordMovement(bool accepted)
    {
        if (accepted)
        {
            Interlocked.Increment(ref acceptedMovements);
        }
        else
        {
            Interlocked.Increment(ref rejectedMovements);
        }
    }

    public void RecordAction(bool accepted)
    {
        if (accepted)
        {
            Interlocked.Increment(ref acceptedActions);
        }
        else
        {
            Interlocked.Increment(ref rejectedActions);
        }
    }
}
