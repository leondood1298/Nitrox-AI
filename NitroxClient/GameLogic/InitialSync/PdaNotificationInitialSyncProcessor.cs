using NitroxClient.GameLogic.InitialSync.Abstract;

namespace NitroxClient.GameLogic.InitialSync;

public sealed class PdaNotificationInitialSyncProcessor : InitialSyncProcessor
{
    public PdaNotificationInitialSyncProcessor(PdaNotifications pdaNotifications)
    {
        // The PDA's content has to be there first, and the game time the log's clock is set from
        AddDependency<PdaInitialSyncProcessor>();
        AddDependency<StoryGoalInitialSyncProcessor>();

        AddStep(sync => pdaNotifications.Restore(sync.UnreadPdaNotifications));
    }
}
