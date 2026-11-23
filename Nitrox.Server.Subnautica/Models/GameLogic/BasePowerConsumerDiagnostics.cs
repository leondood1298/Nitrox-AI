using Nitrox.Model.Core;

namespace Nitrox.Server.Subnautica.Models.GameLogic;

/// <summary>
///     What one player reported as consumed from one source that player does not simulate, and what became of the
///     reports. Two players reporting for the same consumer, or a consumer that reports nothing, shows here.
/// </summary>
internal readonly record struct BasePowerConsumerDiagnostics(
    SessionId Consumer,
    long Forwarded,
    float ForwardedAmount,
    long ServerApplied,
    float ServerAppliedAmount,
    long Rejected);
