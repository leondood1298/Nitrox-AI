using System.Collections;
using System.Collections.Generic;
using NitroxClient.Communication.Abstract;
using NitroxClient.GameLogic.Simulation;
using NitroxClient.MonoBehaviours;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities;
using Nitrox.Model.Subnautica.Packets;
using UnityEngine;

namespace NitroxClient.GameLogic;

/// <summary>
///     Vehicles built at a mobile vehicle bay (<see cref="Constructor" />). A build made here is announced to the others;
///     another player's build is replayed here with the build animation (see VehicleEntitySpawner).
/// </summary>
public class MobileVehicleBay
{
    private readonly IPacketSender packetSender;
    private readonly SimulationOwnership simulationOwnership;

    /// <summary>Bays replaying another player's build, with the id that build's vehicle gets.</summary>
    private readonly Dictionary<ConstructorInput, NitroxId> replayingBays = [];

    /// <summary>Vehicles made by a replay, until the replay takes them.</summary>
    private readonly Dictionary<NitroxId, GameObject> replayedVehicles = [];

    /// <summary>The bay whose build was granted its lock and is being started (ConstructorInput.Craft runs again).</summary>
    private ConstructorInput grantedBay;

    public MobileVehicleBay(IPacketSender packetSender, SimulationOwnership simulationOwnership)
    {
        this.packetSender = packetSender;
        this.simulationOwnership = simulationOwnership;
    }

    /// <summary>
    ///     Called before ConstructorInput.Craft, which takes the resources and starts the build. As a pilot's seat, a bay
    ///     builds for one player at a time: the build only starts once the bay's exclusive lock is granted to the player who
    ///     picked the vehicle. Two players who pick within one round trip both see the bay free, the server grants one of
    ///     them, and the other's resources are left alone.
    /// </summary>
    /// <returns>True if the build may start now; otherwise it starts, or not, once the lock is answered.</returns>
    public bool MayStartBuild(ConstructorInput bay, TechType techType, float duration)
    {
        if (grantedBay == bay)
        {
            grantedBay = null;
            return true;
        }
        if (!bay.constructor || !bay.constructor.TryGetNitroxId(out NitroxId bayId) || simulationOwnership.HasExclusiveLock(bayId))
        {
            return true;
        }
        simulationOwnership.RequestSimulationLock(new LockRequest<BuildRequest>(bayId, SimulationLockType.EXCLUSIVE, OnBuildLockAnswered, new BuildRequest(bay, techType, duration)));
        return false;
    }

    private void OnBuildLockAnswered(NitroxId bayId, bool granted, BuildRequest request)
    {
        ConstructorInput bay = request.Bay;
        if (!bay)
        {
            return;
        }
        // Granted after another player's build reached this game (its replay made the bay busy): that build has the bay
        if (!granted || (bay.logic && bay.logic.inProgress))
        {
            ErrorMessage.AddMessage(Language.main.Get("Nitrox_DenyOwnershipHand"));
            if (granted)
            {
                simulationOwnership.RequestSimulationLock(bayId, SimulationLockType.TRANSIENT);
            }
            return;
        }

        grantedBay = bay;
        try
        {
            bay.Craft(request.TechType, request.Duration);
        }
        finally
        {
            grantedBay = null;
        }
        if (bay.logic && bay.logic.inProgress)
        {
            // The bay is free for the others again once the build is done
            bay.StartCoroutine(ReleaseBayAfterBuild(bayId, bay));
        }
        else
        {
            // Not started (no resources, no room): nothing to hold the bay for
            simulationOwnership.RequestSimulationLock(bayId, SimulationLockType.TRANSIENT);
        }
    }

    private IEnumerator ReleaseBayAfterBuild(NitroxId bayId, ConstructorInput bay)
    {
        // ConstructorInput.Craft sets the build time itself (10 s for a Seamoth, 20 s for a Cyclops...)
        yield return new WaitUntil(() => !bay || !bay.logic || !bay.logic.inProgress);
        if (simulationOwnership.HasExclusiveLock(bayId))
        {
            simulationOwnership.RequestSimulationLock(bayId, SimulationLockType.TRANSIENT);
        }
    }

    private sealed record BuildRequest(ConstructorInput Bay, TechType TechType, float Duration) : LockRequestContext;

    /// <summary>
    ///     Replays another player's build at this bay: the vehicle the game makes next at this bay gets
    ///     <paramref name="vehicleId" /> and is not announced. As in the game, the bay is busy until the build is done, so
    ///     that nobody starts a second build into the first one.
    /// </summary>
    public void BeginReplay(ConstructorInput bay, NitroxId vehicleId, TechType techType, float remainingDuration)
    {
        replayingBays[bay] = vehicleId;
        MarkBuilding(bay, techType, remainingDuration);
    }

    /// <summary>
    ///     Another player builds at this bay. The game refuses to open a bay whose crafter logic is in progress
    ///     (ConstructorInput.OnHandClick); a build only set that in the game of the player who started it.
    /// </summary>
    public void MarkBuilding(ConstructorInput bay, TechType techType, float remainingDuration)
    {
        CrafterLogic logic = bay.logic;
        if (logic && remainingDuration > 0f)
        {
            logic.Craft(techType, remainingDuration);
        }
    }

    /// <returns>The vehicle the replay made, or null if the game made none.</returns>
    public GameObject EndReplay(ConstructorInput bay, NitroxId vehicleId)
    {
        replayingBays.Remove(bay);
        if (!replayedVehicles.TryGetValue(vehicleId, out GameObject vehicle))
        {
            return null;
        }
        replayedVehicles.Remove(vehicleId);
        return vehicle;
    }

    /// <summary>
    ///     Called by the game (ConstructorInput.OnCraftingBeginAsync) once a vehicle was made at a bay.
    /// </summary>
    public void BeginCrafting(ConstructorInput constructor, GameObject constructedObject, TechType techType, float duration)
    {
        // Sometimes build templates, such as the cyclops, are already tagged with IDs.  Remove any that exist to retag.
        // TODO: this seems to happen because various patches execute when the cyclops template loads (on game load).
        // This will leave vehicles with NitroxEntity but an empty NitroxId.  We need to chase these down and only call
        // the code paths when the owner has a simulation lock.
        Vehicles.RemoveNitroxEntitiesTagging(constructedObject);

        if (replayingBays.TryGetValue(constructor, out NitroxId replayedId))
        {
            replayingBays.Remove(constructor);
            NitroxEntity.SetNewId(constructedObject, replayedId);
            replayedVehicles[replayedId] = constructedObject;
            return;
        }

        constructor.constructor.TryGetIdOrWarn(out NitroxId constructorId);

        NitroxId constructedObjectId = NitroxEntity.GenerateNewId(constructedObject);

        VehicleEntity vehicleEntity = Vehicles.BuildVehicleEntity(constructedObject, constructedObjectId, techType, constructorId);

        packetSender.Send(new EntitySpawnedByClient(vehicleEntity));
    }
}
