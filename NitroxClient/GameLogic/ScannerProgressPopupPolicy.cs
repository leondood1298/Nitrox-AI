namespace NitroxClient.GameLogic;

public static class ScannerProgressPopupPolicy
{
    public static bool ShouldShow(bool wasAlreadyResearched, bool fullyResearched, int previousUnlocked, int updatedUnlocked, int totalFragments, bool multiplayerActive, bool initialSyncCompleted) =>
        !wasAlreadyResearched && !fullyResearched && updatedUnlocked > previousUnlocked && totalFragments > 1 && multiplayerActive && initialSyncCompleted;
}
