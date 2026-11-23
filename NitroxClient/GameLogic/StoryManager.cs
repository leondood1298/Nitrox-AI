using System;
using NitroxClient.MonoBehaviours;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Story;
using UnityEngine;

namespace NitroxClient.GameLogic;

public static class StoryManager
{
    public static void ScanCompleted(NitroxId entityId, bool destroy)
    {
        string fragmentKey = entityId.ToString();
        PDAScanner.cachedProgress.Remove(fragmentKey);
        NitroxEntity.TryGetObjectFrom(entityId, out GameObject scanObject);
        if (destroy)
        {
            PDAScanner.fragments.Remove(fragmentKey);
            if (scanObject)
            {
                // Copy the SendMessage from PDAScanner.Scan() but we don't care about the EntryData
                scanObject.SendMessage("OnScanned", null, SendMessageOptions.DontRequireReceiver);
                GameObject.Destroy(scanObject);
            }
            return;
        }

        // The scanning player's game recorded the fragment already when the server sends its scan back
        if (PDAScanner.fragments.ContainsKey(fragmentKey))
        {
            return;
        }
        // Recorded even when the object is not loaded here, else it could be scanned again once it is
        PDAScanner.fragments.Add(fragmentKey, 1f);
        if (scanObject)
        {
            scanObject.SendMessage("OnScanned", null, SendMessageOptions.DontRequireReceiver);
        }
    }

    /// <summary>
    ///     The game sets <see cref="StoryGoalCustomEventHandler.gunDisabled" /> only in DisableGun (the player at the
    ///     terminal) and in Awake. Players who see the goal complete through the network need it too, else they cannot
    ///     launch the rocket and the Sunbeam logic takes the branch of a gun that still works.
    /// </summary>
    public static void OnCustomEventGoalCompleted(StoryGoalCustomEventHandler handler, string key)
    {
        if (string.Equals(key, handler.gunDeactivate.key, StringComparison.OrdinalIgnoreCase))
        {
            handler.gunDisabled = true;
        }
    }

    /// <summary>
    ///     Each game picks the Sunbeam shoot-down voice line from its own player's position (in or out of range), so the
    ///     line another player's game picked must not be played here.
    /// </summary>
    public static bool IsSunbeamDestroyEventPickedByPosition(string key)
    {
        StoryGoalCustomEventHandler handler = StoryGoalCustomEventHandler.main;
        if (!handler)
        {
            return false;
        }
        return string.Equals(key, handler.sunbeamDestroyEventInRange.key, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(key, handler.sunbeamDestroyEventOutOfRange.key, StringComparison.OrdinalIgnoreCase);
    }

    public static void UpdateAuroraData(AuroraEventData auroraEventData)
    {
        // A time skip sends this to players still in the loading screen too; initial sync applies the Aurora data later
        if (!CrashedShipExploder.main)
        {
            return;
        }
        CrashedShipExploder.main.timeToStartCountdown = auroraEventData.TimeToStartCountdown;
        CrashedShipExploder.main.timeToStartWarning = auroraEventData.TimeToStartWarning;
    }

    public static void RestoreAurora()
    {
        CleanAuroraEvents();
        // Same logic as in OnConsoleCommand_restoreship but without the time recalculation
        CrashedShipExploder.main.SwapModels(false);
        CrashedShipExploder.main.fxControl.StopAndDestroy(0, 0f);
        CrashedShipExploder.main.fxControl.StopAndDestroy(1, 0f);
    }

    /// <summary>
    /// Removes the aurora-related entries from StoryGoalManager's completedEvents and PDALog
    /// </summary>
    public static void CleanAuroraEvents()
    {
        foreach (string eventKey in AuroraEventData.GoalNames)
        {
            StoryGoalManager.main.completedGoals.Remove(eventKey);
            PDALog.Remove(eventKey);
            // A warning of the previous run still waiting would play right away
            StoryGoalScheduler.main.schedule.RemoveAll(goal => goal.goalKey == eventKey);
        }
    }
}
