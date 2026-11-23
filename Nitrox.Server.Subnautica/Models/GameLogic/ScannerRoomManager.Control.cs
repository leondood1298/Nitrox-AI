using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Nitrox.Model.Core;
using Nitrox.Model.DataStructures;
using Nitrox.Model.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.Helper;

namespace Nitrox.Server.Subnautica.Models.GameLogic;

/// <summary>
///     Who controls which camera. A player takes a camera with an exclusive simulation lock, asked for like the lock on
///     any other entity, and then says that it flies the camera with it (<see cref="MapRoomCameraControlChanged" />).
///     The lock alone is not control: a client also takes it while its Stalker carries the camera.
/// </summary>
internal sealed partial class ScannerRoomManager
{
    /// <summary>
    ///     The game's GameModeOption.NoEnergy, part of Creative. Nothing needs power in such a game.
    /// </summary>
    private const int NO_ENERGY_GAME_MODE_OPTION = 16;

    /// <summary>
    ///     The player controlling each camera, and whether they have used the one preview image a grant allows.
    ///     Written inside the gate, read from anywhere.
    /// </summary>
    private readonly ConcurrentDictionary<NitroxId, ControlSession> controllers = new();

    /// <summary>
    ///     The players who have said that they are about to take a camera and whose request for its lock has not been
    ///     answered yet. The lock request itself is the same for every entity and every purpose; this is what tells a
    ///     player who wants to control a camera from a client whose Stalker wants to carry it. Only used inside the gate.
    /// </summary>
    private readonly HashSet<(NitroxId CameraId, SessionId SessionId)> pendingControllers = [];

    /// <summary>
    ///     Whether <paramref name="player" /> holds this Scanner Room camera exclusively: by controlling it, or while
    ///     their Stalker carries it. Such a lock is only ended by the camera's own lifecycle (release, pickup, docking,
    ///     destruction, disconnect), never as a side effect of the player losing sight of the camera or handing off
    ///     its movement. Does not enter the gate.
    /// </summary>
    public bool IsControlledBy(NitroxId cameraId, Player player) =>
        simulationOwnershipData.TryGetLock(cameraId, out SimulationOwnershipData.PlayerLock currentLock) &&
        currentLock.Player.SessionId == player.SessionId && currentLock.LockType == SimulationLockType.EXCLUSIVE &&
        IsScannerCamera(cameraId);

    /// <summary>
    ///     The player who said they control this camera and has not given it up. Does not enter the gate.
    /// </summary>
    public bool TryGetController(NitroxId cameraId, out SessionId controller)
    {
        if (controllers.TryGetValue(cameraId, out ControlSession? session))
        {
            controller = session.SessionId;
            return true;
        }
        controller = default;
        return false;
    }

    /// <summary>
    ///     Called for a request for an exclusive lock before it is decided like for any other entity. An exclusive lock
    ///     on a camera that no player controls the camera with is held for a Stalker that carries it. It is only given
    ///     back when the Stalker's client says so, so it must not keep players out: a Stalker that unloads mid-carry
    ///     would leave the camera uncontrollable until its simulating player disconnects. In the game a carried camera
    ///     can be controlled. Such a lock gives way to a player who has said that they are taking the camera; its
    ///     holder is told like of any other change of ownership and makes its Stalker drop the camera. It gives way to
    ///     nobody else: not to another Stalker, and a player's own lock never does.
    /// </summary>
    public async Task MakeWayForExclusiveRequestAsync(Player requester, SimulationOwnershipRequest request)
    {
        if (request.LockType != SimulationLockType.EXCLUSIVE || !IsScannerCamera(request.Id))
        {
            return;
        }
        using Hold held = await EnterAsync();
        // An exclusive lock on a camera is either a player's, who controls the camera or is about to, or the lock a
        // client takes while its Stalker carries the camera. Only the second kind gives way.
        if (pendingControllers.Contains((request.Id, requester.SessionId)) &&
            simulationOwnershipData.TryGetLock(request.Id, out SimulationOwnershipData.PlayerLock heldLock) && heldLock.Player != requester &&
            heldLock.LockType == SimulationLockType.EXCLUSIVE && !controllers.ContainsKey(request.Id) && !pendingControllers.Contains((request.Id, heldLock.Player.SessionId)) &&
            simulationOwnershipData.RevokeIfOwner(request.Id, heldLock.Player))
        {
            CameraView camera = GetCamera(request.Id);
            diagnostics.RecordAccepted("control_preempt", camera.DockedRoom, request.Id, heldLock.Player.SessionId, camera.DockedSlot, $"exclusive_lock_taken_by_{requester.SessionId}");
        }
    }

    /// <summary>
    ///     Called once a request for an exclusive lock has been decided. A player who had said that they are taking the
    ///     camera controls it from the moment the lock is theirs: nothing can come between the grant and their control,
    ///     so nobody else's request can take the camera from them.
    /// </summary>
    public async Task ExclusiveRequestAnsweredAsync(Player requester, SimulationOwnershipRequest request, bool acquired)
    {
        if (request.LockType != SimulationLockType.EXCLUSIVE || !IsScannerCamera(request.Id))
        {
            return;
        }
        using Hold held = await EnterAsync();
        if (!pendingControllers.Remove((request.Id, requester.SessionId)))
        {
            return;
        }
        if (acquired)
        {
            await BeginControlInGateAsync(requester, request.Id);
            return;
        }
        CameraView camera = GetCamera(request.Id);
        diagnostics.RecordRejected("control_acquire", camera.DockedRoom, request.Id, requester.SessionId, camera.DockedSlot, "locked");
    }

    /// <summary>
    ///     Handles a player's word that they control a camera now, or no longer. A player says that they take a camera
    ///     before they ask for its exclusive lock, which is what control takes; they control the camera once they hold
    ///     the lock (<see cref="ExclusiveRequestAnsweredAsync" />). A player who holds the lock already, because their
    ///     Stalker carries the camera, controls it at once. The other players are told when control begins and ends.
    /// </summary>
    public async Task SetControlAsync(Player sender, MapRoomCameraControlChanged packet)
    {
        using Hold held = await EnterAsync();
        if (!packet.IsControlling)
        {
            CameraView camera = GetCamera(packet.CameraId);
            bool hadSaidSo = pendingControllers.Remove((packet.CameraId, sender.SessionId));
            // A release can cross the end of control by the server (the camera was picked up, say), and another
            // player may control the camera by the time it arrives.
            if (!IsController(packet.CameraId, sender.SessionId))
            {
                if (!hadSaidSo)
                {
                    diagnostics.RecordRejected("control_release", camera.DockedRoom, packet.CameraId, sender.SessionId, camera.DockedSlot, "not_controller");
                }
                return;
            }
            EndControl(packet.CameraId, sender.SessionId);
            diagnostics.RecordAccepted("control_release", camera.DockedRoom, packet.CameraId, sender.SessionId, camera.DockedSlot);
            await SendToOthersAsync(sender, new MapRoomCameraControlChanged(packet.CameraId, sender.SessionId, false));
            return;
        }

        if (HoldsExclusively(packet.CameraId, sender))
        {
            await BeginControlInGateAsync(sender, packet.CameraId);
            return;
        }
        pendingControllers.Add((packet.CameraId, sender.SessionId));
    }

    private bool HoldsExclusively(NitroxId cameraId, Player player) =>
        simulationOwnershipData.TryGetLock(cameraId, out SimulationOwnershipData.PlayerLock heldLock) && heldLock.Player == player && heldLock.LockType == SimulationLockType.EXCLUSIVE;

    /// <summary>
    ///     Makes the holder of a camera's exclusive lock its controller and tells the other players, or refuses and
    ///     tells the holder that their control ended, because their game shows the camera's view by then or is about to.
    /// </summary>
    private async Task BeginControlInGateAsync(Player holder, NitroxId cameraId)
    {
        CameraView camera = GetCamera(cameraId);
        // Only a camera in a dock or out in the world can be controlled, not one in an inventory. Battery and health
        // are judged by what the server holds: the lock is the same for every entity and knows nothing of cameras, so
        // this is where the game's rule for a camera (MapRoomCamera.CanBeControlled) is the server's as well.
        string? refusal = camera.WorldCamera == null ? "invalid_assoc" : GetUnusableReason(camera.State.Energy, camera.State.Health, holder.GameMode);
        if (refusal != null)
        {
            diagnostics.RecordRejected("control_acquire", camera.DockedRoom, cameraId, holder.SessionId, camera.DockedSlot, refusal);
            // The lock was taken to control the camera. It goes back to what is left of it: simulating the camera.
            await GiveUpExclusiveLockAsync(cameraId, holder);
            await ReplyAsync(holder, new MapRoomCameraControlChanged(cameraId, holder.SessionId, false));
            return;
        }

        knownCameraIds.TryAdd(cameraId, 0);
        if (IsController(cameraId, holder.SessionId))
        {
            return;
        }
        // A controller on record who is not the holder has lost the lock without saying that their control ended.
        if (controllers.TryRemove(cameraId, out ControlSession? stale))
        {
            diagnostics.RecordAccepted("control_revoke", camera.DockedRoom, cameraId, stale.SessionId, camera.DockedSlot, "lock_lost");
            await SendToAllAsync(new MapRoomCameraControlChanged(cameraId, stale.SessionId, false));
        }
        // A player controls one camera at a time. Taking another one ends control of the previous one here: its
        // release may still be on its way or may never come.
        NitroxId[] previousCameraIds = GetCamerasControlledBy(holder.SessionId).ToArray();
        controllers[cameraId] = new ControlSession(holder.SessionId);
        diagnostics.RecordAccepted("control_acquire", camera.DockedRoom, cameraId, holder.SessionId, camera.DockedSlot);
        await SendToOthersAsync(holder, new MapRoomCameraControlChanged(cameraId, holder.SessionId, true));
        foreach (NitroxId previousCameraId in previousCameraIds)
        {
            if (EndControlOfLock(previousCameraId, holder.SessionId, "switch") is { } release)
            {
                await SendToAllAsync(release);
            }
            await GiveUpExclusiveLockAsync(previousCameraId, holder);
        }
    }

    /// <summary>
    ///     Handles the one picture a player may publish of what a camera sees after taking control of it. It goes to
    ///     the other players, and is kept for players who join while the camera is still controlled. Anything else is
    ///     dropped: a picture from a player who does not control the camera, a second one, or one that is no picture.
    /// </summary>
    public async Task PublishPreviewAsync(Player sender, MapRoomCameraPreview packet)
    {
        using Hold held = await EnterAsync();
        CameraView camera = GetCamera(packet.CameraId);
        int width = 0;
        int height = 0;
        // The one opportunity is used up even by malformed data. A controller cannot use an invalid payload to turn
        // this presentation packet into an unbounded retry channel.
        string? refusal = packet.CameraId == null || !controllers.TryGetValue(packet.CameraId, out ControlSession? session) || session.SessionId != sender.SessionId ? "not_controller" :
                          !TryUsePreview(session) ? "already_published" :
                          !MapRoomCameraPreviewImage.TryValidate(packet.JpegBytes, out width, out height) ? "invalid_jpeg" : null;
        if (refusal != null)
        {
            diagnostics.RecordRejected("preview", camera.DockedRoom, packet.CameraId, sender.SessionId, camera.State.CameraNumber, $"{refusal}_bytes_{packet.JpegBytes?.Length ?? 0}_{width}x{height}");
            return;
        }
        controllers[packet.CameraId].Preview = packet.JpegBytes;
        diagnostics.RecordAccepted("preview", camera.DockedRoom, packet.CameraId, sender.SessionId, camera.State.CameraNumber, $"bytes_{packet.JpegBytes.Length}_{width}x{height}");
        await SendToOthersAsync(sender, packet);
    }

    /// <summary>
    ///     Tells a player who has just joined which cameras are being controlled, and by whom. Control is announced
    ///     when it is taken, which they missed: such a camera would stay frozen for them while its controller flies
    ///     it, and their Scanner Room screen would offer it for control.
    /// </summary>
    public async Task SendControlGrantsAsync(SessionId joinedSessionId)
    {
        if (controllers.IsEmpty)
        {
            return;
        }

        // Read and sent inside the gate: a release could otherwise slip in between reading a controller and sending
        // it, which would leave the joined player with a controller for good.
        using Hold held = await EnterAsync();
        foreach ((NitroxId cameraId, ControlSession session) in controllers.ToArray())
        {
            CameraView camera = GetCamera(cameraId);
            if (camera.WorldCamera == null)
            {
                continue;
            }
            diagnostics.RecordAccepted("control_sync", camera.DockedRoom, cameraId, session.SessionId, camera.DockedSlot, "current_pilot_sent_to_joined_player");
            await packetSender.SendPacketAsync(new MapRoomCameraControlChanged(cameraId, session.SessionId, true), joinedSessionId);
            if (session.Preview != null)
            {
                // What the screens of everyone else show for this camera.
                await packetSender.SendPacketAsync(new MapRoomCameraPreview(cameraId, session.Preview), joinedSessionId);
            }
        }
    }

    /// <summary>
    ///     Takes every simulation lock from a session that ended and announces the end of its camera control. Both
    ///     happen inside the gate, so no other player can take one of the cameras before everyone has been told that
    ///     the previous control ended.
    /// </summary>
    /// <returns>The locks that were taken, for the caller to hand the entities on to other players.</returns>
    public async Task<List<SimulationOwnershipData.RevokedLock>> RevokeLocksOfSessionAsync(SessionId sessionId)
    {
        using Hold held = await EnterAsync();
        List<SimulationOwnershipData.RevokedLock> revokedLocks = simulationOwnershipData.RevokeAllLocksForOwner(sessionId);
        pendingControllers.RemoveWhere(pending => pending.SessionId == sessionId);
        foreach (NitroxId cameraId in GetCamerasControlledBy(sessionId).ToArray())
        {
            if (EndControlOfLock(cameraId, sessionId, "disconnect") is { } release)
            {
                await SendToAllAsync(release);
            }
        }
        return revokedLocks;
    }

    internal static string? GetUnusableReason(float energy, float health, SubnauticaGameMode gameMode)
    {
        if (health <= 0f)
        {
            return "dead";
        }
        bool requiresPower = ((int)gameMode & NO_ENERGY_GAME_MODE_OPTION) == 0;
        return energy <= 0f && requiresPower ? "empty" : null;
    }

    /// <summary>
    ///     Ends the control of a camera by the player whose lock on it ends, if that player controlled it, and makes
    ///     the packet that tells everyone. A change of simulation ownership cannot stand in for it, because it says
    ///     nothing about control. Call inside the gate.
    /// </summary>
    /// <param name="removedCamera">The camera, if it has left the entity registry already and cannot be looked up.</param>
    /// <returns>Null if that player did not control the camera.</returns>
    private MapRoomCameraControlChanged? EndControlOfLock(NitroxId cameraId, SessionId holder, string reason, CameraView? removedCamera = null)
    {
        if (!IsController(cameraId, holder))
        {
            return null;
        }
        EndControl(cameraId, holder);
        CameraView camera = removedCamera ?? GetCamera(cameraId);
        diagnostics.RecordAccepted("control_revoke", camera.DockedRoom, cameraId, holder, camera.DockedSlot, reason);
        return new MapRoomCameraControlChanged(cameraId, holder, false);
    }

    /// <summary>
    ///     Ends whatever control there is of a camera that goes away, see <see cref="EndControlOfLock" />.
    /// </summary>
    private MapRoomCameraControlChanged? EndAnyControl(NitroxId cameraId, string reason, CameraView? removedCamera = null) =>
        controllers.TryGetValue(cameraId, out ControlSession? session) ? EndControlOfLock(cameraId, session.SessionId, reason, removedCamera) : null;

    /// <summary>
    ///     Turns the exclusive lock of <paramref name="holder" /> on a camera into what is left when the reason for it
    ///     is gone, and tells everyone. A camera in a dock or out in the world keeps them as its simulating player:
    ///     battery and health reports are only accepted from that player, who also recharges a docked camera. Nobody
    ///     simulates a camera in an inventory.
    /// </summary>
    private async Task GiveUpExclusiveLockAsync(NitroxId cameraId, Player holder)
    {
        if (!simulationOwnershipData.TryGetLock(cameraId, out SimulationOwnershipData.PlayerLock heldLock) || heldLock.Player != holder || heldLock.LockType != SimulationLockType.EXCLUSIVE)
        {
            return;
        }
        if (GetCamera(cameraId).WorldCamera is not { } worldCamera)
        {
            simulationOwnershipData.RevokeIfOwner(cameraId, holder);
            await SendToAllAsync(new SimulationOwnershipChange(cameraId, SessionId.SERVER_ID, SimulationLockType.TRANSIENT));
            return;
        }
        if (simulationOwnershipData.TryAcquire(cameraId, holder, SimulationLockType.TRANSIENT))
        {
            await SendToAllAsync(new SimulationOwnershipChange(cameraId, holder.SessionId, SimulationLockType.TRANSIENT, SimulationWhitelist.ShouldSimulateEntityMovement(worldCamera)));
        }
    }

    private void EndControl(NitroxId cameraId, SessionId controllerSessionId)
    {
        if (IsController(cameraId, controllerSessionId))
        {
            controllers.TryRemove(cameraId, out _);
        }
    }

    private bool IsController(NitroxId cameraId, SessionId sessionId) =>
        controllers.TryGetValue(cameraId, out ControlSession? session) && session.SessionId == sessionId;

    private static bool TryUsePreview(ControlSession session)
    {
        if (session.PreviewUsed)
        {
            return false;
        }
        session.PreviewUsed = true;
        return true;
    }

    /// <summary>
    ///     The cameras this player controls right now. The game lets a player control one camera at a time.
    /// </summary>
    private IEnumerable<NitroxId> GetCamerasControlledBy(SessionId controllerSessionId) =>
        controllers.Where(controller => controller.Value.SessionId == controllerSessionId).Select(controller => controller.Key);

    private sealed class ControlSession(SessionId sessionId)
    {
        public SessionId SessionId { get; } = sessionId;

        /// <summary>
        ///     Only read and written inside the gate.
        /// </summary>
        public bool PreviewUsed { get; set; }

        /// <summary>
        ///     The picture this controller published, for players who join while they still control the camera.
        /// </summary>
        public byte[]? Preview { get; set; }
    }
}
