using System.Collections.Generic;
using NitroxClient.Communication.Abstract;
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

    /// <summary>Bays replaying another player's build, with the id that build's vehicle gets.</summary>
    private readonly Dictionary<ConstructorInput, NitroxId> replayingBays = [];

    /// <summary>Vehicles made by a replay, until the replay takes them.</summary>
    private readonly Dictionary<NitroxId, GameObject> replayedVehicles = [];

    public MobileVehicleBay(IPacketSender packetSender)
    {
        this.packetSender = packetSender;
    }

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
