using System.Collections.Generic;
using Nitrox.Model.Core;

namespace Nitrox.Server.Subnautica.Models.GameLogic;

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
    IReadOnlyList<BasePowerConsumerDiagnostics> Consumers,
    long HandBacks = 0);
