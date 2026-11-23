using NitroxClient.GameLogic.InitialSync.Abstract;

namespace NitroxClient.GameLogic.InitialSync;

/// <summary>
///     Restores the active precursor teleporters before any teleporter spawns: a teleporter reads them once, when it starts.
/// </summary>
public sealed class PrecursorTeleporterInitialSyncProcessor : InitialSyncProcessor
{
    public PrecursorTeleporterInitialSyncProcessor(PrecursorTeleporters precursorTeleporters)
    {
        AddStep(sync => precursorTeleporters.SetActive(sync.ActiveTeleporters));
    }
}
