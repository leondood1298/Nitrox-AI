using System.Collections.Generic;
using Nitrox.Model.DataStructures;

namespace NitroxClient.GameLogic;

/// <summary>
///     Adds up the energy this client took from power sources it does not simulate, so it can be reported in a few
///     packets per second instead of one per frame (a charger or a docked vehicle draws a little every frame).
/// </summary>
internal sealed class BasePowerConsumptionLedger
{
    internal const float REPORT_INTERVAL_SECONDS = 0.25f;

    /// <summary>
    ///     Below this a source's pending amount is kept for a later report rather than sent on its own.
    /// </summary>
    internal const float MIN_REPORT_AMOUNT = 0.001f;

    private readonly Dictionary<NitroxId, float> pendingBySource = [];
    private readonly List<NitroxId> dueSources = [];
    private float lastReportTime = float.NegativeInfinity;

    public void Record(NitroxId sourceId, float consumed)
    {
        if (consumed <= 0f || float.IsNaN(consumed) || float.IsInfinity(consumed))
        {
            return;
        }
        pendingBySource.TryGetValue(sourceId, out float pending);
        pendingBySource[sourceId] = pending + consumed;
    }

    /// <summary>
    ///     Returns what should be reported now and forgets it. Empty until the report interval has passed.
    /// </summary>
    public IReadOnlyList<KeyValuePair<NitroxId, float>> TakeDue(float now)
    {
        if (pendingBySource.Count == 0 || now - lastReportTime < REPORT_INTERVAL_SECONDS)
        {
            return [];
        }

        List<KeyValuePair<NitroxId, float>> due = [];
        dueSources.Clear();
        foreach (KeyValuePair<NitroxId, float> pending in pendingBySource)
        {
            if (pending.Value >= MIN_REPORT_AMOUNT)
            {
                due.Add(pending);
                dueSources.Add(pending.Key);
            }
        }
        foreach (NitroxId sourceId in dueSources)
        {
            pendingBySource.Remove(sourceId);
        }
        if (due.Count > 0)
        {
            lastReportTime = now;
        }
        return due;
    }
}
