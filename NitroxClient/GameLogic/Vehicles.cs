using System.Collections;
using System.Collections.Generic;
using Nitrox.Model.Core;
using NitroxClient.Communication;
using NitroxClient.Communication.Abstract;
using NitroxClient.GameLogic.Helper;
using NitroxClient.GameLogic.Spawning.Metadata;
using NitroxClient.GameLogic.Spawning.Metadata.Extractor;
using NitroxClient.MonoBehaviours;
using NitroxClient.Unity.Helper;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;
using Nitrox.Model.Subnautica.Packets;
using UnityEngine;

namespace NitroxClient.GameLogic;

public class Vehicles
{
    private readonly IPacketSender packetSender;
    private readonly IMultiplayerSession multiplayerSession;
    private readonly PlayerManager playerManager;
    private readonly EntityMetadataManager entityMetadataManager;
    private readonly Entities entities;
    private readonly SimulationOwnership simulationOwnership;

    private readonly Dictionary<TechType, string> pilotingChairByTechType = [];

    /// <summary>
    ///     Vehicles another player docked whose docking this game still plays (it waits for the vehicle's movement to catch
    ///     up), with the bay they go into and the docking's number. The bay counts as taken from the moment the docking is
    ///     heard of, so that this game's own vehicle cannot dock into it meanwhile.
    /// </summary>
    private readonly Dictionary<Vehicle, (VehicleDockingBay Bay, int Number)> remoteDockings = [];

    private int lastRemoteDockingNumber;

    public Vehicles(IPacketSender packetSender, IMultiplayerSession multiplayerSession, PlayerManager playerManager, EntityMetadataManager entityMetadataManager, Entities entities,
                    SimulationOwnership simulationOwnership)
    {
        this.simulationOwnership = simulationOwnership;
        this.packetSender = packetSender;
        this.multiplayerSession = multiplayerSession;
        this.playerManager = playerManager;
        this.entityMetadataManager = entityMetadataManager;
        this.entities = entities;
    }

    private PilotingChair FindPilotingChairWithCache(GameObject parent, TechType techType)
    {
        if (!parent)
        {
            return null;
        }
        if (pilotingChairByTechType.TryGetValue(techType, out string path))
        {
            if (path == string.Empty)
            {
                return null;
            }
            return parent.transform.Find(path).GetComponent<PilotingChair>();
        }
        else
        {
            PilotingChair chair = parent.GetComponentInChildren<PilotingChair>(true);
            pilotingChairByTechType.Add(techType, chair ? chair.gameObject.GetHierarchyPath(parent) : string.Empty);
            return chair;
        }
    }

    public void BroadcastDestroyedVehicle(NitroxId id)
    {
        using (PacketSuppressor<VehicleOnPilotModeChanged>.Suppress())
        {
            EntityDestroyed entityDestroyed = new(id);
            packetSender.Send(entityDestroyed);
        }
    }

    public void BroadcastDestroyedCyclops(SubRoot subRoot, NitroxId id)
    {
        Optional<EntityMetadata> metadata = entityMetadataManager.Extract(subRoot);

        if (metadata.HasValue && metadata.Value is CyclopsMetadata cyclopsMetadata)
        {
            // The flag the metadata extractor uses to check for this isn't set until 2.5s
            // after the Cyclops is destroyed, so set it manually instead
            cyclopsMetadata.IsDestroyed = true;
            entities.BroadcastMetadataUpdate(id, cyclopsMetadata);
        }
    }

    public static void EngagePlayerMovementSuppressor(Vehicle vehicle)
    {
        // TODO: Properly prevent the vehicle from sending position update as long as it's not free from the animation
        PacketSuppressor<PlayerMovement> playerMovementSuppressor = PacketSuppressor<PlayerMovement>.Suppress();
        vehicle.StartCoroutine(AllowMovementPacketsAfterDockingAnimation());
        return;

        /*
         A poorly timed movement packet will cause major problems when docking because the remote
         player will think that the player is no longer in a vehicle.  Unfortunately, the game calls
         the vehicle exit code before the animation completes so we need to suppress any side effects.
         Two thing we want to protect against:

             1) If a movement packet is received when docking, the player might exit the vehicle early,
                and it will show them sitting outside the vehicle during the docking animation.

             2) If a movement packet is received when undocking, the player game object will be stuck in
                place until after the player exits the vehicle.  This causes the player body to stretch to
                the current cyclops position.
        */
        IEnumerator AllowMovementPacketsAfterDockingAnimation()
        {
            yield return Yielders.WaitFor3Seconds;
            playerMovementSuppressor.Dispose();
        }
    }

    public void BroadcastOnPilotModeChanged(GameObject gameObject, bool isPiloting)
    {
        if (gameObject.TryGetIdOrWarn(out NitroxId vehicleId))
        {
            VehicleOnPilotModeChanged packet = new(vehicleId, multiplayerSession.Reservation.SessionId, isPiloting);
            packetSender.Send(packet);
        }
    }

    public void SetOnPilotMode(GameObject gameObject, SessionId sessionId, bool isPiloting)
    {
        if (playerManager.TryFind(sessionId, out RemotePlayer remotePlayer))
        {
            if (gameObject.TryGetComponent(out Vehicle vehicle))
            {
                remotePlayer.SetVehicle(isPiloting ? vehicle : null);
            }
            else if (gameObject.GetComponent<SubRoot>())
            {
                if (!isPiloting)
                {
                    remotePlayer.SetPilotingChair(null);
                    return;
                }
                PilotingChair pilotingChair = FindPilotingChairWithCache(gameObject, TechType.Cyclops);
                remotePlayer.SetPilotingChair(pilotingChair);
            }
            // TODO: [FUTURE] For any mods adding new vehicle with a piloting chair, there should be something done right here
        }
    }

    public void SetOnPilotMode(NitroxId vehicleId, SessionId sessionId, bool isPiloting)
    {
        if (NitroxEntity.TryGetObjectFrom(vehicleId, out GameObject vehicleObject))
        {
            SetOnPilotMode(vehicleObject, sessionId, isPiloting);
        }
    }

    /// <returns>
    ///     True if <paramref name="part" /> belongs to a Seamoth or Prawn suit that another player's game simulates: the
    ///     vehicle's energy (its lights' drain, its charge reports) is that game's.
    /// </returns>
    public bool IsSimulatedElsewhere(Component part)
    {
        Vehicle vehicle = part.GetComponentInParent<Vehicle>();
        return vehicle && vehicle.TryGetNitroxId(out NitroxId vehicleId) && !simulationOwnership.HasAnyLockType(vehicleId);
    }

    /// <returns>The number of this docking, to be given to <see cref="IsRemoteDockingCurrent" />.</returns>
    public int BeginRemoteDocking(Vehicle vehicle, VehicleDockingBay bay)
    {
        lastRemoteDockingNumber++;
        remoteDockings[vehicle] = (bay, lastRemoteDockingNumber);
        return lastRemoteDockingNumber;
    }

    /// <summary>
    ///     False once the vehicle was undocked (or docked again) after this docking began: the docking must not be finished.
    /// </summary>
    public bool IsRemoteDockingCurrent(Vehicle vehicle, int number)
    {
        return remoteDockings.TryGetValue(vehicle, out (VehicleDockingBay Bay, int Number) docking) && docking.Number == number;
    }

    /// <returns>True if a docking of this vehicle was still being played.</returns>
    public bool EndRemoteDocking(Vehicle vehicle)
    {
        return remoteDockings.Remove(vehicle);
    }

    /// <summary>
    ///     A bay holds one vehicle. Another player's vehicle may hold it in this game's view before the docking shows here.
    /// </summary>
    public bool IsBayTakenByAnother(VehicleDockingBay bay, Vehicle vehicle)
    {
        Vehicle docked = bay.GetDockedVehicle();
        if (docked && docked != vehicle)
        {
            return true;
        }
        foreach (KeyValuePair<Vehicle, (VehicleDockingBay Bay, int Number)> docking in remoteDockings)
        {
            // A vehicle destroyed while it docked no longer takes the bay
            if (docking.Key && docking.Value.Bay == bay && docking.Key != vehicle)
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    ///     <see cref="VehicleDockingBay.DockVehicle" /> without what concerns the local player (seating, goals such as
    ///     "Dock_Seamoth"), for a vehicle docked by another player or loaded docked.
    /// </summary>
    public static void DockWithoutPlayer(VehicleDockingBay bay, Vehicle vehicle)
    {
        bay.dockedVehicle = vehicle;
        LargeWorldStreamer.main.cellManager.UnregisterEntity(vehicle.gameObject);
        vehicle.transform.parent = bay.GetSubRoot().transform;
        vehicle.docked = true;
        bay.vehicle_docked_param = true;
        SkyEnvironmentChanged.Broadcast(vehicle.gameObject, bay.subRoot);
        bay.GetSubRoot().BroadcastMessage("UnlockDoors", SendMessageOptions.DontRequireReceiver);

        // The health is only added in the game simulating the vehicle (LiveMixin.AddHealth is gated on the lock)
        bay.CancelInvoke(nameof(VehicleDockingBay.RepairVehicle));
        bay.InvokeRepeating(nameof(VehicleDockingBay.RepairVehicle), 0.0f, 5f);
    }

    /// <summary>
    /// Removes ALL <see cref="NitroxEntity"/> on the <see cref="GameObject"/> and its children.
    /// </summary>
    /// <remarks>
    /// Subnautica pre-emptively loads a prefab of each vehicle (such as a cyclops) during the initial game load.  This allows the game to instantaniously
    /// use this prefab for the first constructor event.  Subsequent constructor events will use this prefab as a template.  However, this is problematic
    /// because the template + children are now tagged with NitroxEntity because players are interacting with it. We need to remove any NitroxEntity from
    /// the new gameObject that used the template.
    /// </remarks>
    public static void RemoveNitroxEntitiesTagging(GameObject constructedObject)
    {
        NitroxEntity[] nitroxEntities = constructedObject.GetComponentsInChildren<NitroxEntity>(true);

        foreach (NitroxEntity nitroxEntity in nitroxEntities)
        {
            nitroxEntity.Remove();
            Object.DestroyImmediate(nitroxEntity);
        }
    }

    public static VehicleEntity BuildVehicleEntity(GameObject constructedObject, NitroxId constructedObjectId, TechType techType, NitroxId constructorId = null)
    {
        VehicleEntity vehicleEntity = new(constructorId, DayNightCycle.main.timePassedAsFloat, constructedObject.transform.ToLocalDto(), string.Empty, false, constructedObjectId, techType.ToDto(), null);
        VehicleChildEntityHelper.PopulateChildren(constructedObjectId, constructedObject.GetFullHierarchyPath(), vehicleEntity.ChildEntities, constructedObject);
        if (techType == TechType.RocketBase)
        {
            // The rocket's time capsule is a scene object outside the rocket, so PopulateChildren does not find it
            vehicleEntity.ChildEntities.Add(TimeCapsules.CreateRocketCapsule(constructedObject, constructedObjectId));
        }
        return vehicleEntity;
    }
}
