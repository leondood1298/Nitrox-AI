using System;
using System.Collections.Generic;
using Nitrox.Model.Core;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.Packets;
using NitroxClient.Communication.Abstract;
using NitroxClient.GameLogic.Simulation;
using NitroxClient.MonoBehaviours;
using UnityEngine;

namespace NitroxClient.GameLogic;

/// <summary>
///     Who controls which Scanner Room camera. A player controls a camera with the exclusive simulation lock on it.
///     This class asks for that lock and gives it back, tells the server when this player takes a camera or lets go
///     of it, and keeps what the server says about the cameras other players control.
/// </summary>
public class MapRoomCameras
{
    private readonly IPacketSender packetSender;
    private readonly IMultiplayerSession multiplayerSession;
    private readonly SimulationOwnership simulationOwnership;
    private readonly ScannerRoomClientDiagnostics diagnostics;

    /// <summary>
    ///     The cameras this player controls. The game lets a player control one camera at a time; while it changes
    ///     from one to the next, the first has not been given up yet.
    /// </summary>
    private readonly HashSet<NitroxId> locallyControlled = [];

    /// <summary>
    ///     The cameras this client has asked the server for and has no answer yet.
    /// </summary>
    private readonly HashSet<NitroxId> pendingControl = [];

    /// <summary>
    ///     The player who controls each camera that another player controls. The game offers such a camera to nobody.
    /// </summary>
    private readonly Dictionary<NitroxId, SessionId> remoteControllers = [];

    /// <summary>
    ///     The instance ids of the cameras that were last not offered to the player, so that the log says why once
    ///     and not every time the game asks.
    /// </summary>
    private readonly HashSet<int> loggedUnavailableCameras = [];

    public MapRoomCameras(IPacketSender packetSender, IMultiplayerSession multiplayerSession, SimulationOwnership simulationOwnership, ScannerRoomClientDiagnostics diagnostics)
    {
        this.packetSender = packetSender;
        this.multiplayerSession = multiplayerSession;
        this.simulationOwnership = simulationOwnership;
        this.diagnostics = diagnostics;
    }

    /// <summary>
    ///     The camera a screen would give the player: on a click (direction 0), or when the player cycles to the next
    ///     or previous one. The game itself is asked, and its screen is left as it was.
    /// </summary>
    public static MapRoomCamera PeekCamera(MapRoomScreen screen, int direction)
    {
        int index = screen.currentIndex;
        screen.currentIndex = screen.NormalizeIndex(index + direction);
        MapRoomCamera camera = screen.FindCamera(direction == 0 ? 1 : direction);
        screen.currentIndex = index;
        return camera;
    }

    /// <summary>
    ///     Called before the game puts the player into the view of a camera, by a click on a screen or by cycling. A
    ///     player controls a camera with the exclusive simulation lock on it, which is asked for like the lock on a
    ///     vehicle the player wants to pilot, after telling the server what it is for. Until the server grants it the
    ///     player stays where they are: in front of the screen, or in the view of the camera they control.
    /// </summary>
    /// <param name="runVanilla">Runs the game's method once the lock is granted.</param>
    /// <returns>True if the game may go on right away: there is no camera to take, or this player holds its lock.</returns>
    public bool TryStartControl(MapRoomScreen screen, int direction, Action runVanilla)
    {
        MapRoomCamera camera = PeekCamera(screen, direction);
        if (!camera || camera == screen.currentCamera || !camera.TryGetNitroxId(out NitroxId cameraId) || simulationOwnership.HasExclusiveLock(cameraId))
        {
            return true;
        }
        if (pendingControl.Add(cameraId))
        {
            // Said before the lock is asked for. The request is the same for every entity and every purpose; with
            // this the server knows that it is a player's, who controls the camera from the moment the lock is
            // granted, and not that of a client whose Stalker wants to carry the camera.
            packetSender.Send(new MapRoomCameraControlChanged(cameraId, multiplayerSession.Reservation.SessionId, true));
            diagnostics.Record("control_publish", "ok", cameraId, reason: "lock_requested");
            simulationOwnership.RequestSimulationLock(new LockRequest<CameraControlStart>(cameraId, SimulationLockType.EXCLUSIVE, LockAnswered, new CameraControlStart(screen, runVanilla)));
        }
        return false;
    }

    /// <summary>
    ///     Called before the game shows the player a camera's view. Only a camera this player holds exclusively is
    ///     shown: the game can choose another camera than the one the lock was asked for, if something changed while
    ///     the answer was on its way.
    /// </summary>
    public bool MayEnterView(MapRoomCamera camera, MapRoomScreen screen)
    {
        if (camera.TryGetNitroxId(out NitroxId cameraId) && simulationOwnership.HasExclusiveLock(cameraId))
        {
            return true;
        }
        // The game has made the camera the screen's current one already, and a screen with a camera keeps the
        // player's input. It must not keep a camera the player does not control.
        if (screen && screen.currentCamera == camera)
        {
            screen.currentCamera = null;
        }
        return false;
    }

    /// <summary>
    ///     Called after the game put the player into a camera's view. Everybody else is told who controls the camera.
    /// </summary>
    public void ControlStarted(MapRoomCamera camera)
    {
        if (!camera.TryGetNitroxId(out NitroxId cameraId) || !locallyControlled.Add(cameraId))
        {
            return;
        }
        remoteControllers.Remove(cameraId);
        // A player who held the lock already, because their Stalker carries the camera, has not said yet that they
        // take it. Everybody else has, when they asked for the lock.
        if (!pendingControl.Remove(cameraId))
        {
            packetSender.Send(new MapRoomCameraControlChanged(cameraId, multiplayerSession.Reservation.SessionId, true));
        }
        MovementBroadcaster.RegisterWatched(camera.gameObject, cameraId);
        diagnostics.Record("control_publish", "ok", cameraId, reason: "control_taken");
    }

    /// <summary>
    ///     Called after the game took the player out of a camera's view: the player left it, cycled on, or the screen
    ///     is gone (the game does this itself when a screen is disabled, MapRoomScreen.OnDisable). Control is given up
    ///     first and the lock second, so that nobody sees an exclusive lock that stands for nothing.
    /// </summary>
    public void ControlEnded(MapRoomCamera camera)
    {
        if (!camera || !camera.TryGetNitroxId(out NitroxId cameraId) || !locallyControlled.Remove(cameraId))
        {
            return;
        }
        MovementBroadcaster.UnregisterWatched(cameraId);
        packetSender.Send(new MapRoomCameraControlChanged(cameraId, multiplayerSession.Reservation.SessionId, false));
        if (simulationOwnership.HasExclusiveLock(cameraId))
        {
            // Known here at once: a player who takes the camera again before the answer arrives has to ask for the
            // exclusive lock again, or the server would see their control without it.
            simulationOwnership.SimulateEntity(cameraId, SimulationLockType.TRANSIENT);
            simulationOwnership.RequestSimulationLock(cameraId, SimulationLockType.TRANSIENT);
        }
        diagnostics.Record("control_publish", "ok", cameraId, reason: "control_given_up");
    }

    /// <summary>
    ///     Called when the simulation lock on an entity went to another player. If it was a camera this player
    ///     controlled, the player is taken out of its view.
    /// </summary>
    public void LockLost(NitroxId cameraId) => EndLocalControl(cameraId, "lock_lost");

    private void LockAnswered(NitroxId cameraId, bool acquired, CameraControlStart start)
    {
        if (!acquired)
        {
            pendingControl.Remove(cameraId);
            // Another player controls the camera, and this client had not heard of it when the player chose it.
            diagnostics.Record("control_apply", "reject", cameraId, reason: "lock_denied");
            if (Language.main != null)
            {
                ErrorMessage.AddMessage(Language.main.Get("Nitrox_DenyOwnershipHand"));
            }
            return;
        }
        if (start.Screen)
        {
            start.RunVanilla();
        }
        if (pendingControl.Remove(cameraId))
        {
            // The game did not take the camera after all: the screen is gone, or it chose another camera. The
            // server has this player down as its controller since it granted the lock.
            diagnostics.Record("control_apply", "reject", cameraId, reason: "not_taken");
            packetSender.Send(new MapRoomCameraControlChanged(cameraId, multiplayerSession.Reservation.SessionId, false));
            simulationOwnership.SimulateEntity(cameraId, SimulationLockType.TRANSIENT);
            simulationOwnership.RequestSimulationLock(cameraId, SimulationLockType.TRANSIENT);
        }
    }

    /// <summary>
    ///     Takes the player out of a camera's view that is no longer theirs, without telling the server, which knows.
    /// </summary>
    private void EndLocalControl(NitroxId cameraId, string reason)
    {
        if (!locallyControlled.Remove(cameraId))
        {
            return;
        }
        MovementBroadcaster.UnregisterWatched(cameraId);
        diagnostics.Record("control_apply", "reject", cameraId, reason: reason);
        if (NitroxEntity.TryGetComponentFrom(cameraId, out MapRoomCamera camera))
        {
            // The game always pairs FreeCamera (stops the drone's sounds and screen effect) with ExitLockedMode
            // (returns the player and resets the view).
            camera.FreeCamera(resetPlayerPosition: true);
            camera.ExitLockedMode(resetPlayerPosition: true);
        }
    }

    /// <summary>
    ///     Handles the word that a player controls a camera now, or no longer.
    /// </summary>
    public void ProcessControlChanged(MapRoomCameraControlChanged packet)
    {
        bool isLocalPlayer = packet.SessionId == multiplayerSession.Reservation.SessionId;
        if (packet.IsControlling)
        {
            if (isLocalPlayer)
            {
                return;
            }
            remoteControllers[packet.CameraId] = packet.SessionId;
            // Two players cannot control one camera. If this player thought they did, they lost the lock on the way.
            EndLocalControl(packet.CameraId, $"taken_by_{packet.SessionId}");
            EnsureMovedByItsController(packet.CameraId);
            diagnostics.Record("control_apply", "ok", packet.CameraId, reason: $"remote_pilot_{packet.SessionId}");
            return;
        }
        if (isLocalPlayer)
        {
            // The server ended this player's control: it refused it, or the camera is gone. This also reaches a
            // player who is in the camera's view already, however late it comes.
            EndLocalControl(packet.CameraId, "ended_by_server");
            return;
        }
        // A release ends the control of the player it names and of nobody else. The next player's control can have
        // been announced before the release of the previous one arrives.
        if (!remoteControllers.TryGetValue(packet.CameraId, out SessionId controller) || controller != packet.SessionId)
        {
            diagnostics.Record("control_release_apply", "stale", packet.CameraId, reason: $"remote_pilot_{packet.SessionId}");
            return;
        }
        remoteControllers.Remove(packet.CameraId);
        StopBeingMovedByAController(packet.CameraId);
        diagnostics.Record("control_release_apply", "ok", packet.CameraId, reason: $"remote_pilot_{packet.SessionId}");
    }

    /// <summary>
    ///     A camera another player flies is moved by their reports, like a vehicle another player pilots.
    /// </summary>
    private static void EnsureMovedByItsController(NitroxId cameraId)
    {
        if (NitroxEntity.TryGetObjectFrom(cameraId, out GameObject cameraObject) && cameraObject.GetComponent<MapRoomCamera>() &&
            !cameraObject.GetComponent<MapRoomCameraMovementReplicator>())
        {
            cameraObject.AddComponent<MapRoomCameraMovementReplicator>();
        }
    }

    private static void StopBeingMovedByAController(NitroxId cameraId)
    {
        if (NitroxEntity.TryGetObjectFrom(cameraId, out GameObject cameraObject) && cameraObject.TryGetComponent(out MapRoomCameraMovementReplicator replicator))
        {
            UnityEngine.Object.Destroy(replicator);
            MapRoomCameraDocks.SettleInDock(cameraObject);
        }
    }

    /// <summary>
    ///     Called after the game decided whether it offers a camera to the player. A camera another player controls
    ///     is offered to nobody.
    /// </summary>
    /// <param name="gameOffers">What the game decided.</param>
    /// <returns>Whether the camera is offered.</returns>
    public bool CanBeControlled(MapRoomCamera camera, bool gameOffers)
    {
        bool controllerStateAllows = CanSelectForControl(camera);
        bool offered = gameOffers && controllerStateAllows;
        int instanceId = camera.GetInstanceID();
        if (offered)
        {
            loggedUnavailableCameras.Remove(instanceId);
        }
        else if (loggedUnavailableCameras.Add(instanceId))
        {
            float energy = camera.energyMixin ? camera.energyMixin.charge : -1f;
            bool alive = camera.liveMixin && camera.liveMixin.IsAlive();
            bool attached = camera.pickupAble && camera.pickupAble.attached;
            Log.Warn($"[MapRoomCamera] Camera {camera.cameraNumber} unavailable: vanilla={gameOffers}, controllerState={controllerStateAllows}, energy={energy:F2}, alive={alive}, attached={attached}, activeAndEnabled={camera.isActiveAndEnabled}");
        }
        return offered;
    }

    /// <summary>
    ///     Whether the game may offer a camera to the player. A camera another player controls is offered to nobody.
    /// </summary>
    internal bool CanSelectForControl(MapRoomCamera camera) => !camera || !camera.TryGetNitroxId(out NitroxId cameraId) || CanSelectForControl(cameraId);

    internal bool CanSelectForControl(NitroxId cameraId) => !remoteControllers.ContainsKey(cameraId);

    /// <summary>
    ///     Whether this player controls the camera or has asked for it.
    /// </summary>
    public bool HasPendingControl(NitroxId cameraId) => pendingControl.Contains(cameraId);

    public bool HasLocalControl(NitroxId cameraId) => locallyControlled.Contains(cameraId);

    /// <summary>
    ///     Called when the server says that nobody simulates an entity any more, which for a camera means that it is
    ///     gone with its room. Nobody controls it then.
    /// </summary>
    internal void ClearControlState(NitroxId cameraId)
    {
        pendingControl.Remove(cameraId);
        bool wasControlled = remoteControllers.Remove(cameraId) | locallyControlled.Contains(cameraId);
        EndLocalControl(cameraId, "ownership_dropped");
        StopBeingMovedByAController(cameraId);
        if (wasControlled)
        {
            diagnostics.Record("ownership_clear", "ok", cameraId, reason: "canonical_drop");
        }
    }

    /// <summary>
    ///     Called when a camera has spawned and the state it was sent with is applied. If the server said before that
    ///     somebody controls it, as it does for a player who has just joined, the camera follows that player now.
    /// </summary>
    public void CameraSpawned(NitroxId cameraId)
    {
        if (remoteControllers.ContainsKey(cameraId))
        {
            EnsureMovedByItsController(cameraId);
        }
    }
}
