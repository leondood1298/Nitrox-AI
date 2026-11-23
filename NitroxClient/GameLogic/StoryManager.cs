using System;
using NitroxClient.Communication.Abstract;
using NitroxClient.MonoBehaviours;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.Packets;
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

    public static bool IsGoalComplete(string key) => !string.IsNullOrEmpty(key) && StoryGoalManager.main && StoryGoalManager.main.IsGoalComplete(key);

    /// <summary>
    ///     Reports a goal the game completed on the goal manager directly. Goals are reported from StoryGoal.Execute, but the
    ///     game completes some without it: the intro's PDA triggers (IntroLifepodDirector.OpenPDA and OnClosePDA) and the
    ///     goal of a Precursor door that opens (PrecursorGlobalKeyActivator.ToggleDoor). They were neither shared nor saved.
    /// </summary>
    /// <param name="wasComplete">
    ///     Whether the goal was complete before the game's call. Then the call did nothing, also when it was the network
    ///     that made this game complete it.
    /// </param>
    public static void ReportGoalCompletedDirectly(IPacketSender packetSender, string key, bool wasComplete)
    {
        // Goals the ending completes are not saved in single player, which saves nothing after the launch
        if (wasComplete || !Multiplayer.Main || !Multiplayer.Main.InitialSyncCompleted || LaunchRocket.launchStarted || !IsGoalComplete(key))
        {
            return;
        }
        // STORY: the receivers complete it through StoryGoal.Execute, which then only calls the goal manager, as the game did here
        packetSender.Send(new StoryGoalExecuted(key, StoryGoalExecuted.EventType.STORY));
    }

    /// <summary>
    ///     False until the game scene with its story objects exists. The server sends story packets to every connected
    ///     session, also to one that still loads the game. Such a session can drop them: the snapshot it is given when it
    ///     joins is made later and has what they say.
    /// </summary>
    public static bool IsStoryLoaded() => StoryGoalManager.main && StoryGoalScheduler.main && DayNightCycle.main;

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
