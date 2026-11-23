namespace NitroxClient.GameLogic.Simulation;

/// <summary>
///     The Stalker an item's lock was asked for.
/// </summary>
internal sealed class StalkerShinyGrab(StalkerShinyGrabs.IStalker stalker) : LockRequestContext
{
    public StalkerShinyGrabs.IStalker Stalker { get; } = stalker;
}
