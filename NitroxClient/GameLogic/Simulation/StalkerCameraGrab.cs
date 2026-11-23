namespace NitroxClient.GameLogic.Simulation;

/// <summary>
///     The Stalker a Scanner Room camera's lock was asked for.
/// </summary>
internal sealed class StalkerCameraGrab(StalkerCameraGrabs.IStalker stalker) : LockRequestContext
{
    public StalkerCameraGrabs.IStalker Stalker { get; } = stalker;
}
