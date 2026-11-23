using System.Collections.Generic;
using System.Linq;
using NitroxClient.GameLogic.InitialSync.Abstract;
using NitroxClient.MonoBehaviours;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.Packets;
using Story;

namespace NitroxClient.GameLogic.InitialSync;

public sealed class StoryGoalInitialSyncProcessor : InitialSyncProcessor
{
    private readonly TimeManager timeManager;

    public StoryGoalInitialSyncProcessor(TimeManager timeManager)
    {
        this.timeManager = timeManager;

        AddStep(SetTimeData);
        AddStep(SetupStoryGoalManager);
        AddStep(SetupTrackers);
        AddStep(SetupAuroraAndSunbeam);
        AddStep(SetScheduledGoals);
    }

    private static void SetupStoryGoalManager(InitialPlayerSync packet)
    {
        List<string> completedGoals = packet.StoryGoalData.CompletedGoals;
        List<string> radioQueue = packet.StoryGoalData.RadioQueue;
        Dictionary<string, float> personalGoals = packet.StoryGoalData.PersonalCompletedGoalsWithTimestamp;
        StoryGoalManager storyGoalManager = StoryGoalManager.main;

        storyGoalManager.completedGoals.AddRange(completedGoals);

        // Packets are processed during the loading screen, a message may already be pending, or played by another player
        PendingRadioMessages.Merge(storyGoalManager.pendingRadioMessages, radioQueue, storyGoalManager.completedGoals.Contains);
        storyGoalManager.PulsePendingMessages();

        // Restore states of GoalManager and the (tutorial) arrow system
        foreach (KeyValuePair<string, float> entry in personalGoals)
        {
            Goal entryGoal = GoalManager.main.goals.Find(goal => goal.customGoalName.Equals(entry.Key));
            if (entryGoal != null)
            {
                entryGoal.SetTimeCompleted(entry.Value);
            }
        }
        GoalManager.main.completedGoalNames.AddRange(personalGoals.Keys);
        PlayerWorldArrows.main.completedCustomGoals.AddRange(personalGoals.Keys);

        // Deactivate the current arrow if it was completed
        if (personalGoals.Any(goal => goal.Key.Equals(WorldArrowManager.main.currentGoalText)))
        {
            WorldArrowManager.main.DeactivateArrow();
        }

        Log.Info($"""
        Received initial sync packet with:
        - Completed story goals : {completedGoals.Count}
        - Personal goals        : {personalGoals.Count}
        - Radio queue           : {radioQueue.Count}
        """);
    }

    private static void SetupTrackers(InitialPlayerSync packet)
    {
        List<string> completedGoals = packet.StoryGoalData.CompletedGoals;
        StoryGoalManager storyGoalManager = StoryGoalManager.main;
        OnGoalUnlockTracker onGoalUnlockTracker = storyGoalManager.onGoalUnlockTracker;
        CompoundGoalTracker compoundGoalTracker = storyGoalManager.compoundGoalTracker;

        // Initializing CompoundGoalTracker and OnGoalUnlockTracker again (with OnSceneObjectsLoaded) requires us to
        // we first clear what was done in the first iteration of OnSceneObjectsLoaded
        onGoalUnlockTracker.goalUnlocks.Clear();
        compoundGoalTracker.goals.Clear();
        // we force initialized to false so OnSceneObjectsLoaded actually does something
        storyGoalManager.initialized = false;
        storyGoalManager.OnSceneObjectsLoaded();
        
        // Clean LocationGoalTracker, BiomeGoalTracker and ItemGoalTracker already completed goals
        storyGoalManager.locationGoalTracker.goals.RemoveAll(goal => completedGoals.Contains(goal.key));
        storyGoalManager.biomeGoalTracker.goals.RemoveAll(goal => completedGoals.Contains(goal.key));

        List<TechType> techTypesToRemove = new();
        foreach (KeyValuePair<TechType, List<ItemGoal>> entry in storyGoalManager.itemGoalTracker.goals)
        {
            // Goals are all triggered at the same time but we don't know if some entries share certain goals
            if (entry.Value.All(goal => completedGoals.Contains(goal.key)))
            {
                techTypesToRemove.Add(entry.Key);
                continue;
            }
        }
        techTypesToRemove.ForEach(techType => storyGoalManager.itemGoalTracker.goals.Remove(techType));

        // OnGoalUnlock might trigger the creation of a signal which is later on set to invisible when getting close to it
        // the invisibility is managed by PingInstance_Set_Patches and is restored during PlayerPreferencesInitialSyncProcessor
        // So we still need to recreate the signals at every game launch

        // To avoid having the SignalPing play its sound we just make its notification null while triggering it
        // (the sound is something like "coordinates added to the gps" or something)
        SignalPing prefabSignalPing = onGoalUnlockTracker.signalPrefab.GetComponent<SignalPing>();
        PDANotification pdaNotification = prefabSignalPing.vo;
        prefabSignalPing.vo = null;

        foreach (OnGoalUnlock onGoalUnlock in onGoalUnlockTracker.unlockData.onGoalUnlocks)
        {
            if (completedGoals.Contains(onGoalUnlock.goal))
            {
                // Code adapted from OnGoalUnlock.Trigger
                foreach (UnlockSignalData unlockSignalData in onGoalUnlock.signals)
                {
                    unlockSignalData.Trigger(onGoalUnlockTracker);
                }
            }
        }
        
        // recover the notification sound
        prefabSignalPing.vo = pdaNotification;
    }

    // Must happen after CompletedGoals
    private static void SetupAuroraAndSunbeam(InitialPlayerSync packet)
    {
        TimeData timeData = packet.TimeData;

        AuroraWarnings auroraWarnings = Player.mainObject.GetComponentInChildren<AuroraWarnings>(true);
        auroraWarnings.timeSerialized = DayNightCycle.main.timePassedAsFloat;
        auroraWarnings.OnProtoDeserialize(null);

        CrashedShipExploder.main.version = 2;
        CrashedShipExploder.main.initialized = true;
        StoryManager.UpdateAuroraData(timeData.AuroraEventData);
        CrashedShipExploder.main.timeSerialized = DayNightCycle.main.timePassedAsFloat;
        CrashedShipExploder.main.OnProtoDeserialize(null);

        // The Sunbeam countdown is deduced from whichever of its goals is still scheduled: in its last 30 seconds the
        // first one (PrecursorGunAimCheck) has run already
        StoryGoalCustomEventHandler handler = StoryGoalCustomEventHandler.main;
        if (SunbeamTimeline.TryGetCountdownStart(handler.sunbeamGoals.Select(step => (step.trigger, step.key, step.delay)), packet.StoryGoalData.ScheduledGoals, out float countdownStart))
        {
            handler.countdownActive = true;
            handler.countdownStartingTime = countdownStart;
        }
    }

    // Must happen after CompletedGoals
    private static void SetScheduledGoals(InitialPlayerSync packet)
    {
        List<NitroxScheduledGoal> scheduledGoals = packet.StoryGoalData.ScheduledGoals;

        // We don't want any scheduled goal we add now to be executed before initial sync has finished, else they might not get broadcasted
        StoryGoalScheduler.main.paused = true;
        Multiplayer.OnLoadingComplete += OnLoadingComplete;

        foreach (NitroxScheduledGoal scheduledGoal in scheduledGoals)
        {
            // Clear duplicated goals that might have appeared during loading and before sync
            StoryGoalScheduler.main.schedule.RemoveAll(goal => goal.goalKey == scheduledGoal.GoalKey);

            // The server keeps the shoot-down line of each player's game; this game gets only its own, once its player's
            // position is restored
            if (StoryManager.IsSunbeamDestroyEventPickedByPosition(scheduledGoal.GoalKey))
            {
                continue;
            }

            ScheduledGoal goal = new()
            {
                goalKey = scheduledGoal.GoalKey,
                goalType = (Story.GoalType)scheduledGoal.GoalType,
                timeExecute = scheduledGoal.TimeExecute,
            };
            if (!StoryGoalManager.main.completedGoals.Contains(goal.goalKey))
            {
                StoryGoalScheduler.main.schedule.Add(goal);
            }
        }

        RefreshStoryWithLatestData();

        void OnLoadingComplete()
        {
            Multiplayer.OnLoadingComplete -= OnLoadingComplete;
            ScheduleOwnSunbeamDestroyLine(scheduledGoals);
            StoryGoalScheduler.main.paused = false;
        }
    }

    /// <summary>
    ///     Each game picks the Sunbeam shoot-down line from its own player's position when the range check runs. A player
    ///     who joins after it gets the line for its position now, at the time it would have had it, or none if that time
    ///     has passed.
    /// </summary>
    private static void ScheduleOwnSunbeamDestroyLine(List<NitroxScheduledGoal> scheduledGoals)
    {
        StoryGoalCustomEventHandler handler = StoryGoalCustomEventHandler.main;
        StoryGoalManager storyGoalManager = StoryGoalManager.main;
        // Until the range check runs, this game picks its line itself
        if (!handler || !storyGoalManager || !storyGoalManager.IsGoalComplete(SunbeamTimeline.RANGE_CHECK_GOAL))
        {
            return;
        }

        bool inRange = handler.IsPlayerAtLandingSite();
        StoryGoal ownLine = inRange ? handler.sunbeamDestroyEventInRange : handler.sunbeamDestroyEventOutOfRange;
        StoryGoal otherLine = inRange ? handler.sunbeamDestroyEventOutOfRange : handler.sunbeamDestroyEventInRange;
        if (storyGoalManager.IsGoalComplete(ownLine.key) ||
            !SunbeamTimeline.TryGetOwnDestroyLineTime(ownLine.key, ownLine.delay, otherLine.key, otherLine.delay, scheduledGoals, DayNightCycle.main.timePassedAsDouble, out float timeExecute))
        {
            return;
        }
        StoryGoalScheduler.main.schedule.Add(new ScheduledGoal { goalKey = ownLine.key, goalType = ownLine.goalType, timeExecute = timeExecute });
    }

    // Must happen after CompletedGoals
    private static void RefreshStoryWithLatestData()
    {
        // If those aren't set up yet, they'll initialize correctly in time
        // Else, we need to force them to acquire the right data
        if (StoryGoalCustomEventHandler.main)
        {
            StoryGoalCustomEventHandler.main.Awake();
        }
        if (PrecursorGunStoryEvents.main)
        {
            PrecursorGunStoryEvents.main.Start();
        }
    }

    private void SetTimeData(InitialPlayerSync packet)
    {
        timeManager.ProcessUpdate(packet.TimeData.TimePacket);
        timeManager.InitRealTimeElapsed(packet.TimeData.TimePacket.RealTimeElapsed, packet.TimeData.TimePacket.UpdateTime, packet.IsFirstPlayer);
        timeManager.AuroraRealExplosionTime = packet.TimeData.AuroraEventData.AuroraRealExplosionTime;
    }
}
