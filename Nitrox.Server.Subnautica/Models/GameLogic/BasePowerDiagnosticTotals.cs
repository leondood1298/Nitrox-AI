namespace Nitrox.Server.Subnautica.Models.GameLogic;

/// <summary>
///     The counters of <see cref="BasePowerDiagnostics" /> over all sources since the server started.
/// </summary>
/// <param name="UntrackedSourceEvents">
///     Events for sources beyond <see cref="BasePowerDiagnostics.SOURCE_CAPACITY" />, which are counted here and
///     nowhere else.
/// </param>
internal readonly record struct BasePowerDiagnosticTotals(
    long Events,
    long AcceptedUpdates,
    long RejectedUpdates,
    long ConsumptionForwarded,
    float ConsumptionForwardedAmount,
    long ConsumptionAppliedByServer,
    float ConsumptionAppliedByServerAmount,
    long ConsumptionRejected,
    long UntrackedSourceEvents);
