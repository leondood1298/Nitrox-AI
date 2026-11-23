using System.Collections.Generic;
using Nitrox.Model.DataStructures;
using NitroxClient.GameLogic.Simulation;
using NitroxClient.MonoBehaviours;
using UnityEngine;

namespace NitroxClient.GameLogic;

/// <summary>
///     Stalkers pick Scanner Room cameras up and carry them around. The client that simulates a Stalker takes the
///     camera's exclusive simulation lock for that, the same lock a player controls the camera with, and gives it back
///     when the Stalker drops the camera. Nobody is told that the lock is held for a Stalker: the server takes an
///     exclusive lock that no player has said they control the camera with for one, and gives the camera to any
///     player who asks for it. The Stalker then drops it.
/// </summary>
public sealed class StalkerCameraGrabs(SimulationOwnership simulationOwnership, MapRoomCameras mapRoomCameras)
{
    /// <summary>
    ///     The Stalker of this client that carries each camera.
    /// </summary>
    private readonly Dictionary<NitroxId, IStalker> carriersByCameraId = [];

    private bool applyingGrantedPickup;

    /// <summary>
    ///     Called before the game lets a Stalker pick up what it went for.
    /// </summary>
    /// <returns>False if it is a camera whose lock has to be asked for first; the Stalker picks it up when granted.</returns>
    public bool MayPickUp(CollectShiny collectShiny)
    {
        if (applyingGrantedPickup || !collectShiny.shinyTarget || !collectShiny.shinyTarget.TryGetComponent(out MapRoomCamera camera) || !camera.TryGetNitroxId(out NitroxId cameraId))
        {
            return true;
        }
        return MayPickUp(new GameStalker(collectShiny, camera.gameObject), cameraId);
    }

    /// <summary>
    ///     Called before the game makes a Stalker drop what it carries.
    /// </summary>
    public void Dropped(GameObject target)
    {
        if (target && target.TryGetComponent(out MapRoomCamera camera) && camera.TryGetNitroxId(out NitroxId cameraId))
        {
            Dropped(cameraId);
        }
    }

    /// <summary>
    ///     Called when the simulation lock on an entity went to another player. If it is a camera a Stalker of this
    ///     client carries, a player has taken the camera, and the Stalker lets go of it.
    /// </summary>
    public void LockLost(NitroxId cameraId)
    {
        if (carriersByCameraId.TryGetValue(cameraId, out IStalker stalker))
        {
            carriersByCameraId.Remove(cameraId);
            stalker.Drop();
        }
    }

    internal bool MayPickUp(IStalker stalker, NitroxId cameraId)
    {
        // The player in front of this screen is about to get the camera, with the same lock.
        if (mapRoomCameras.HasPendingControl(cameraId))
        {
            return false;
        }
        if (simulationOwnership.HasExclusiveLock(cameraId))
        {
            Carry(stalker, cameraId);
            return true;
        }
        simulationOwnership.RequestSimulationLock(new LockRequest<StalkerCameraGrab>(cameraId, SimulationLockType.EXCLUSIVE, LockAnswered, new StalkerCameraGrab(stalker)));
        return false;
    }

    internal void Dropped(NitroxId cameraId)
    {
        if (carriersByCameraId.Remove(cameraId))
        {
            GiveLockBack(cameraId);
        }
    }

    private void LockAnswered(NitroxId cameraId, bool acquired, StalkerCameraGrab grab)
    {
        IStalker stalker = grab.Stalker;
        if (!acquired)
        {
            // A player controls the camera. The Stalker looks for something else.
            stalker.ForgetTarget();
            return;
        }
        if (!stalker.StillWantsTarget)
        {
            GiveLockBack(cameraId);
            stalker.ForgetTarget();
            return;
        }
        Carry(stalker, cameraId);
        bool pickedUp = false;
        try
        {
            applyingGrantedPickup = true;
            stalker.PickUp();
            pickedUp = true;
        }
        finally
        {
            applyingGrantedPickup = false;
            if (!pickedUp)
            {
                carriersByCameraId.Remove(cameraId);
                GiveLockBack(cameraId);
            }
        }
    }

    private void Carry(IStalker stalker, NitroxId cameraId)
    {
        carriersByCameraId[cameraId] = stalker;
        // A camera does not report where it is unless a player flies it. Carried, it moves.
        if (EntityPositionBroadcaster.Instance)
        {
            EntityPositionBroadcaster.Instance.WatchEntity(cameraId);
        }
    }

    /// <summary>
    ///     The lock a Stalker needed is the lock the local player controls the camera with, if they do.
    /// </summary>
    private void GiveLockBack(NitroxId cameraId)
    {
        if (simulationOwnership.HasExclusiveLock(cameraId) && !mapRoomCameras.HasLocalControl(cameraId) && !mapRoomCameras.HasPendingControl(cameraId))
        {
            // Known here at once, so that the next Stalker, or the player, asks for the exclusive lock again.
            simulationOwnership.SimulateEntity(cameraId, SimulationLockType.TRANSIENT);
            simulationOwnership.RequestSimulationLock(cameraId, SimulationLockType.TRANSIENT);
        }
    }

    /// <summary>
    ///     A Stalker of the game and the camera it is after. Tests put a Stalker without the game here.
    /// </summary>
    internal interface IStalker
    {
        /// <summary>
        ///     False if the Stalker went for something else, or is gone, while the lock was asked for.
        /// </summary>
        bool StillWantsTarget { get; }

        void PickUp();
        void Drop();
        void ForgetTarget();
    }

    private sealed class GameStalker(CollectShiny collectShiny, GameObject camera) : IStalker
    {
        public bool StillWantsTarget => collectShiny && camera && collectShiny.shinyTarget == camera;

        public void PickUp() => collectShiny.TryPickupShinyTarget();

        public void Drop()
        {
            if (StillWantsTarget)
            {
                collectShiny.DropShinyTarget(camera);
            }
        }

        public void ForgetTarget()
        {
            if (StillWantsTarget)
            {
                collectShiny.shinyTarget = null;
                collectShiny.targetPickedUp = false;
            }
        }
    }
}
