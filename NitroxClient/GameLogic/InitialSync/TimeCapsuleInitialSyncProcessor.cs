using NitroxClient.GameLogic.InitialSync.Abstract;

namespace NitroxClient.GameLogic.InitialSync;

/// <summary>
///     Restores the encyclopedia entries of the collected time capsules, after the other encyclopedia entries.
/// </summary>
public sealed class TimeCapsuleInitialSyncProcessor : InitialSyncProcessor
{
    public TimeCapsuleInitialSyncProcessor(TimeCapsules timeCapsules)
    {
        AddDependency<PdaInitialSyncProcessor>();
        AddStep(sync => timeCapsules.RestoreCollected(sync.CollectedTimeCapsules));
    }
}
