using System.Collections.Generic;
using System.Linq;
using NitroxClient.Communication;
using NitroxClient.GameLogic.Spawning.Metadata.Processor.Abstract;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;
using Nitrox.Model.Subnautica.Packets;
using UnityEngine;
using static Rocket;

namespace NitroxClient.GameLogic.Spawning.Metadata.Processor;

public class RocketMetadataProcessor : EntityMetadataProcessor<RocketMetadata>
{
    // For newly connected players, we will only build the previous stage with construction bots for a certain time period.
    private const float MAX_ALLOWABLE_TIME_FOR_CONSTRUCTOR_BOTS = 10;

    /** Rocket states :
     * 0 : Launch Platform
     * 1 : Gantry
     * 2 : Boosters
     * 3 : Fuel Reserve
     * 4 : Cockpit
     * 5 : Final rocket
     **/
    public override void ProcessMetadata(GameObject gameObject, RocketMetadata metadata)
    {
        Rocket rocket = gameObject.GetComponent<Rocket>();

        if (!rocket)
        {
            Log.Error($"Could not find Rocket on {gameObject.name}");
            return;
        }

        using (PacketSuppressor<EntityMetadataUpdate>.Suppress())
        {
            // The elevator only exists from the gantry on, and a rocket that has just spawned is at stage 0
            UpdateStage(rocket, metadata);
            UpdateElevator(rocket, metadata);
            UpdatePreflightChecks(rocket, metadata);
        }
    }

    private void UpdateElevator(Rocket rocket, RocketMetadata metadata)
    {
        // elevators will only be present on this model after the gantry (p1) is built
        if (rocket.currentRocketStage > 1)
        {
            rocket.elevatorPosition = metadata.ElevatorPosition;
            rocket.elevatorState = (RocketElevatorStates)metadata.ElevatorState;
            rocket.SetElevatorPosition();
        }
    }

    private void UpdateStage(Rocket rocket, RocketMetadata metadata)
    {
        if (rocket.currentRocketStage == metadata.CurrentStage)
        {
            return;
        }

        bool allowConstructorBots = DayNightCycle.main.timePassedAsFloat - metadata.LastStageTransitionTime < MAX_ALLOWABLE_TIME_FOR_CONSTRUCTOR_BOTS;

        RocketConstructor rocketConstructor = rocket.RequireComponentInChildren<RocketConstructor>(true);

        for (int stage = rocket.currentRocketStage; stage < metadata.CurrentStage; stage++)
        {
            bool lastStageToBuild = stage == metadata.CurrentStage - 1;

            // We only want to use construction bots for the last constructed stage (just in case the client is out dated by multiple stages).
            // For all others, just force the completion of that stage.
            if (lastStageToBuild && allowConstructorBots)
            {
                // Another player builds this stage now: it sounds as it does for the builder
                GameObject build = rocket.StartRocketConstruction();
                rocketConstructor.SendBuildBots(build);
            }
            else
            {
                GameObject build = StartRocketConstructionSilently(rocket);
                VFXConstructing vfxConstructing = build.GetComponent<VFXConstructing>();
                vfxConstructing.EndGracefully();
            }
        }
    }

    /// <summary>
    ///     What <see cref="Rocket.StartRocketConstruction" /> does without its stage sounds: a rocket that is restored for
    ///     a player who joins or arrives would play them once for every stage built before.
    /// </summary>
    private static GameObject StartRocketConstructionSilently(Rocket rocket)
    {
        rocket.isBuilding = true;
        GameObject stageObject = rocket.stageObjects[rocket.currentRocketStage];
        stageObject.SetActive(true);
        VFXConstructing vfxConstructing = stageObject.GetComponent<VFXConstructing>();
        if (vfxConstructing)
        {
            vfxConstructing.timeToConstruct = rocket.timeToConstruct;
            vfxConstructing.StartConstruction();
        }
        rocket.isReady = false;
        rocket.BroadcastMessage("SetBuildingAnimationScreen");
        rocket.AdvanceRocketStage();
        return rocket.stageObjects[rocket.currentRocketStage - 1];
    }

    private void UpdatePreflightChecks(Rocket rocket, RocketMetadata metadata)
    {
        if (rocket.currentRocketStage < 4)
        {
            return;
        }

        IEnumerable<PreflightCheck> completedChecks = metadata.PreflightChecks.Select(i => (PreflightCheck)i);

        RocketPreflightCheckManager rocketPreflightCheckManager = rocket.RequireComponent<RocketPreflightCheckManager>();

        foreach (PreflightCheck completedCheck in completedChecks)
        {
            if (!rocketPreflightCheckManager.preflightChecks.Contains(completedCheck))
            {
                CompletePreflightCheck(rocket, completedCheck);
                MarkPreflightCheckComplete(rocketPreflightCheckManager, completedCheck);
            }
        }

        // The game makes the rocket launchable through a delayed RocketReady, which also plays the "ready" voice line
        if (rocketPreflightCheckManager.ReturnRocketReadyForLaunch(false) && !rocketPreflightCheckManager.rocketReadyDelay)
        {
            rocketPreflightCheckManager.stageThreeAnimator.SetBool("ready", true);
            rocketPreflightCheckManager.rocketReadyDelay = true;
        }
    }

    /// <summary>
    ///     What <see cref="RocketPreflightCheckManager.CompletePreflightCheck" /> does without its voice lines: they belong to
    ///     the player who completed the check, and a joiner would hear every completed check again.
    /// </summary>
    private static void MarkPreflightCheckComplete(RocketPreflightCheckManager rocketPreflightCheckManager, PreflightCheck preflightCheck)
    {
        rocketPreflightCheckManager.preflightChecks.Add(preflightCheck);
        rocketPreflightCheckManager.preflightCheckScreenHolder.BroadcastMessage("SetPreflightCheckComplete", preflightCheck, SendMessageOptions.DontRequireReceiver);
    }

    private void CompletePreflightCheck(Rocket rocket, PreflightCheck preflightCheck)
    {
        bool isCockpitCheck = preflightCheck == PreflightCheck.LifeSupport ||
                               preflightCheck == PreflightCheck.PrimaryComputer;

        if (isCockpitCheck)
        {
            CompleteCockpitPreflightCheck(rocket, preflightCheck);
        }
        else
        {
            CompleteBasicPreflightCheck(rocket, preflightCheck);
        }
    }

    private void CompleteCockpitPreflightCheck(Rocket rocket, PreflightCheck preflightCheck)
    {
        CockpitSwitch[] cockpitSwitches = rocket.GetComponentsInChildren<CockpitSwitch>(true);

        foreach (CockpitSwitch cockpitSwitch in cockpitSwitches)
        {
            if (!cockpitSwitch.completed && cockpitSwitch.preflightCheck == preflightCheck)
            {
                cockpitSwitch.animator.SetBool("Completed", true);
                cockpitSwitch.completed = true;

                if (cockpitSwitch.collision)
                {
                    cockpitSwitch.collision.SetActive(false);
                }
            }
        }
    }

    private void CompleteBasicPreflightCheck(Rocket rocket, PreflightCheck preflightCheck)
    {
        ThrowSwitch[] throwSwitches = rocket.GetComponentsInChildren<ThrowSwitch>(true);

        foreach (ThrowSwitch throwSwitch in throwSwitches)
        {
            if (!throwSwitch.completed && throwSwitch.preflightCheck == preflightCheck)
            {
                throwSwitch.animator.AliveOrNull()?.SetTrigger("Throw");
                throwSwitch.completed = true;
                throwSwitch.cinematicTrigger.showIconOnHandHover = false;
                throwSwitch.triggerCollider.enabled = false;
                throwSwitch.lamp.GetComponent<SkinnedMeshRenderer>().material = throwSwitch.completeMat;
            }
        }
    }
}
