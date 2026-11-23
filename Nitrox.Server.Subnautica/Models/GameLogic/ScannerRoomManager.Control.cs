using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Nitrox.Model.Core;
using Nitrox.Model.DataStructures;
using Nitrox.Model.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Bases;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;
using Nitrox.Model.Subnautica.Helper;

namespace Nitrox.Server.Subnautica.Models.GameLogic;

/// <summary>
///     Who controls which camera. Control is an exclusive simulation lock on the camera plus an entry in
///     <see cref="controllers" />; a generic exclusive lock alone (a Stalker carrying the camera) is not control.
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

    private long previewRevision;

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
    ///     The player who was granted control of this camera and has not given it up. Does not enter the gate.
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
    ///     Handles a player's request to take or give up control of a camera, and tells everyone the outcome.
    /// </summary>
    public async Task SetControlAsync(Player sender, MapRoomCameraControl packet)
    {
        using Hold held = await EnterAsync();
        if (packet.IsServerResponse || !IsValidAssociation(packet))
        {
            diagnostics.RecordRejected("control", FindRoom(packet.MapRoomId), packet.CameraId, sender.SessionId, packet.CameraIndex,
                packet.IsServerResponse ? "server_response" : "invalid_assoc");
            await ReplyAsync(sender, CreateControlResponse(packet, false, sender.SessionId));
            return;
        }
        knownCameraIds.TryAdd(packet.CameraId, 0);

        if (packet.IsControlling)
        {
            // A player controls one camera at a time. Taking another one ends control of the previous one here.
            NitroxId[] previousCameraIds = GetCamerasControlledBy(sender.SessionId).Where(cameraId => cameraId != packet.CameraId).ToArray();
            string? unusableReason = GetUnusableReason(packet.CameraId, sender.GameMode);
            bool takenFromAnotherPlayer = simulationOwnershipData.TryGetLock(packet.CameraId, out SimulationOwnershipData.PlayerLock previousLock) &&
                                          previousLock.Player != sender;
            bool granted = unusableReason == null && simulationOwnershipData.TryAcquire(packet.CameraId, sender, SimulationLockType.EXCLUSIVE);
            if (!granted && unusableReason == null && takenFromAnotherPlayer && CanPreempt(previousLock.LockType, IsController(packet.CameraId, previousLock.Player.SessionId)))
            {
                simulationOwnershipData.ForceAcquire(packet.CameraId, sender, SimulationLockType.EXCLUSIVE);
                diagnostics.RecordAccepted("control_preempt", FindRoom(packet.MapRoomId), packet.CameraId, previousLock.Player.SessionId, packet.CameraIndex, $"exclusive_lock_taken_by_{sender.SessionId}");
                granted = true;
            }
            SessionId controller = granted ? sender.SessionId : simulationOwnershipData.GetPlayerForLock(packet.CameraId)?.SessionId ?? sender.SessionId;
            MapRoomCameraControl response = CreateControlResponse(packet, granted, controller);
            if (!granted)
            {
                diagnostics.RecordRejected("control_acquire", FindRoom(packet.MapRoomId), packet.CameraId, sender.SessionId, packet.CameraIndex, unusableReason ?? "locked");
                await ReplyAsync(sender, response);
                return;
            }
            BeginControl(packet.CameraId, sender.SessionId);
            diagnostics.RecordAccepted("control_acquire", FindRoom(packet.MapRoomId), packet.CameraId, sender.SessionId, packet.CameraIndex);
            await SendToAllAsync(response);
            if (takenFromAnotherPlayer)
            {
                // The player who simulated the camera, or whose Stalker carried it, holds a lock in the ordinary
                // simulation bookkeeping of their client. That only ends when they are told who has it now.
                await SendToOthersAsync(sender, new SimulationOwnershipChange(packet.CameraId, sender.SessionId, SimulationLockType.EXCLUSIVE));
            }
            foreach (NitroxId previousCameraId in previousCameraIds)
            {
                await EndControlOfPreviousCameraAsync(sender, previousCameraId);
            }
            return;
        }

        Player? currentOwner = simulationOwnershipData.GetPlayerForLock(packet.CameraId);
        if (!CanAcknowledgeRelease(currentOwner != null, currentOwner == sender))
        {
            diagnostics.RecordRejected("control_release", FindRoom(packet.MapRoomId), packet.CameraId, sender.SessionId, packet.CameraIndex, "non_owner");
            await ReplyAsync(sender, CreateControlResponse(packet, false, currentOwner?.SessionId ?? sender.SessionId));
            return;
        }
        EndControl(packet.CameraId, sender.SessionId);
        SimulationOwnershipChange? keptSimulation = currentOwner != null ? EndControlLock(packet.CameraId, sender) : null;
        diagnostics.RecordAccepted("control_release", FindRoom(packet.MapRoomId), packet.CameraId, sender.SessionId, packet.CameraIndex,
            currentOwner == null ? "idempotent" : null);
        await SendToAllAsync(CreateControlResponse(packet, true, sender.SessionId));
        if (keptSimulation != null)
        {
            // Sent after the release, so no client learns of the new lock while it still counts the sender as controlling.
            await SendToAllAsync(keptSimulation);
        }
    }

    /// <summary>
    ///     Handles a generic simulation lock request if it is for a Scanner Room camera. A camera is locked through its
    ///     control lifecycle, and a docked camera is a record of its room, not a registered entity, so such a request
    ///     cannot take the path of other entities.
    /// </summary>
    /// <returns>False if the id is not a Scanner Room camera's and the request is still to be handled.</returns>
    public async Task<bool> TryHandleLockRequestAsync(Player sender, SimulationOwnershipRequest request)
    {
        if (!IsScannerCamera(request.Id))
        {
            return false;
        }
        using Hold held = await EnterAsync();
        knownCameraIds.TryAdd(request.Id, 0);
        CameraView camera = GetCamera(request.Id);
        if (!camera.Exists)
        {
            await ReplyAsync(sender, new SimulationOwnershipResponse(request.Id, false, request.LockType));
            diagnostics.RecordRejected("camera_lock", cameraId: request.Id, sessionId: sender.SessionId, reason: "removed");
            return true;
        }
        bool requesterOwnsExclusive = simulationOwnershipData.TryGetLock(request.Id, out SimulationOwnershipData.PlayerLock currentLock) &&
                                      currentLock.Player == sender && currentLock.LockType == SimulationLockType.EXCLUSIVE;
        if (!CanGrantGenericOwnership(camera, IsController(request.Id, sender.SessionId), controllers.ContainsKey(request.Id), request.LockType, requesterOwnsExclusive))
        {
            await ReplyAsync(sender, new SimulationOwnershipResponse(request.Id, false, request.LockType));
            diagnostics.RecordRejected("camera_lock", cameraId: request.Id, sessionId: sender.SessionId, reason: "control_required");
            return true;
        }
        bool acquiredLock = simulationOwnershipData.TryAcquire(request.Id, sender, request.LockType);
        if (acquiredLock && request.LockType != SimulationLockType.EXCLUSIVE)
        {
            EndControl(request.Id, sender.SessionId);
        }
        if (acquiredLock)
        {
            bool shouldEntityMove = entityRegistry.TryGetEntityById(request.Id, out WorldEntity? worldEntity) && SimulationWhitelist.ShouldSimulateEntityMovement(worldEntity);
            await SendToOthersAsync(sender, new SimulationOwnershipChange(request.Id, sender.SessionId, request.LockType, shouldEntityMove));
        }

        await ReplyAsync(sender, new SimulationOwnershipResponse(request.Id, acquiredLock, request.LockType));
        string reason = request.LockType == SimulationLockType.EXCLUSIVE ? "exclusive" : "transient";
        if (acquiredLock)
        {
            diagnostics.RecordAccepted("camera_lock", cameraId: request.Id, sessionId: sender.SessionId, reason: reason);
        }
        else
        {
            diagnostics.RecordRejected("camera_lock", cameraId: request.Id, sessionId: sender.SessionId, reason: reason);
        }
        return true;
    }

    /// <summary>
    ///     Handles the one picture a player may publish of what a camera sees after being granted control of it.
    /// </summary>
    public async Task PublishPreviewAsync(Player sender, MapRoomCameraPreview packet)
    {
        if (packet.IsServerResponse)
        {
            diagnostics.RecordRejected("preview", cameraId: packet.CameraId, sessionId: sender.SessionId, reason: "server_response");
            await ReplyAsync(sender, CreatePreviewDenial(packet.CameraId, 0));
            return;
        }
        // A preview is only valid after control was granted, which made the camera known. Taking an arbitrary id
        // further would mark it as a Scanner Room camera and block ownership requests for that entity.
        if (packet.CameraId == null || !knownCameraIds.ContainsKey(packet.CameraId))
        {
            diagnostics.RecordRejected("preview", cameraId: packet.CameraId, sessionId: sender.SessionId, reason: "unknown_camera");
            await ReplyAsync(sender, CreatePreviewDenial(packet.CameraId, 0));
            return;
        }

        using Hold held = await EnterAsync();
        bool validAssociation = TryResolveCameraNumber(packet.CameraId, out MapRoomEntity? room, out int cameraNumber);
        bool accepted = false;
        int width = 0;
        int height = 0;
        long revision = 0;
        string rejectionReason = "non_owner";
        ValueTask sendTask = ValueTask.CompletedTask;
        simulationOwnershipData.ExecuteForOwner(sender, [packet.CameraId], ownedIds =>
        {
            bool hasExclusiveLock = simulationOwnershipData.TryGetLock(packet.CameraId, out SimulationOwnershipData.PlayerLock playerLock) &&
                                    playerLock.Player == sender && playerLock.LockType == SimulationLockType.EXCLUSIVE;
            if (!ownedIds.Contains(packet.CameraId) || !hasExclusiveLock)
            {
                return false;
            }
            if (!TryUsePreview(packet.CameraId, sender.SessionId))
            {
                rejectionReason = "already_published";
                return false;
            }

            // The one opportunity is used up even by malformed data. A controller cannot use an invalid payload to
            // turn this presentation packet into an unbounded retry channel.
            if (!validAssociation)
            {
                rejectionReason = "invalid_assoc";
                return false;
            }
            if (!MapRoomCameraPreviewImage.TryValidate(packet.JpegBytes, out width, out height))
            {
                rejectionReason = "invalid_jpeg";
                return false;
            }

            accepted = true;
            revision = ++previewRevision;
            // Queued while ownership is locked, so a release or reassignment packet cannot overtake the picture.
            sendTask = SendToAllAsync(new MapRoomCameraPreview(packet.CameraId, cameraNumber, packet.JpegBytes, true, true, revision));
            return true;
        });

        if (!accepted)
        {
            diagnostics.RecordRejected("preview", room, packet.CameraId, sender.SessionId, cameraNumber,
                $"{rejectionReason}_bytes_{packet.JpegBytes?.Length ?? 0}_{width}x{height}");
            await ReplyAsync(sender, CreatePreviewDenial(packet.CameraId, cameraNumber));
            return;
        }
        diagnostics.RecordAccepted("preview", room, packet.CameraId, sender.SessionId, cameraNumber,
            $"rev_{revision}_bytes_{packet.JpegBytes.Length}_{width}x{height}");
        await sendTask;
    }

    /// <summary>
    ///     Tells a player who has just joined which cameras are being controlled, and by whom. Control is announced
    ///     when it is granted, which they missed: such a camera would stay frozen for them while its controller flies
    ///     it, and their Scanner Room screen would offer it for control.
    /// </summary>
    public async Task SendControlGrantsAsync(SessionId joinedSessionId)
    {
        if (controllers.IsEmpty)
        {
            return;
        }

        // Read and sent inside the gate: a release could otherwise slip in between reading a grant and sending it,
        // which would leave the joined player with a controller for good.
        using Hold held = await EnterAsync();
        foreach (NitroxId cameraId in controllers.Keys.ToArray())
        {
            if (simulationOwnershipData.TryGetLock(cameraId, out SimulationOwnershipData.PlayerLock controlLock) &&
                controlLock.LockType == SimulationLockType.EXCLUSIVE &&
                IsController(cameraId, controlLock.Player.SessionId) &&
                TryCreateCurrentGrant(cameraId, controlLock.Player.SessionId, out MapRoomCameraControl? grant))
            {
                await packetSender.SendPacketAsync(grant, joinedSessionId);
            }
        }
    }

    /// <summary>
    ///     Takes every simulation lock from a session that ended and announces the end of its camera control. Both
    ///     happen inside the gate, so no other player can be granted one of the cameras before everyone has been told
    ///     that the previous control ended.
    /// </summary>
    /// <returns>The locks that were taken, for the caller to hand the entities on to other players.</returns>
    public async Task<List<SimulationOwnershipData.RevokedLock>> RevokeLocksOfSessionAsync(SessionId sessionId)
    {
        using Hold held = await EnterAsync();
        List<SimulationOwnershipData.RevokedLock> revokedLocks = simulationOwnershipData.RevokeAllLocksForOwner(sessionId);
        foreach (SimulationOwnershipData.RevokedLock revokedLock in revokedLocks)
        {
            if (TryCreateControlRelease(revokedLock.EntityId, revokedLock.Lock, "disconnect", out MapRoomCameraControl? release))
            {
                await SendToAllAsync(release);
            }
        }
        return revokedLocks;
    }

    internal static bool CanAcknowledgeRelease(bool hasOwner, bool senderOwnsLock) => !hasOwner || senderOwnsLock;

    /// <summary>
    ///     An exclusive lock on a camera is either camera control, granted here, or the generic lock a client takes
    ///     while its Stalker carries the camera. The second kind is given to any client that asks and is only given back
    ///     when that client says so, so it must not keep players out: a Stalker that unloads mid-carry would leave the
    ///     camera uncontrollable until its simulating player disconnects. In the game a carried camera can be controlled.
    /// </summary>
    internal static bool CanPreempt(SimulationLockType heldLockType, bool holderIsActiveController) =>
        heldLockType == SimulationLockType.EXCLUSIVE && !holderIsActiveController;

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
    ///     Creates the packet that must accompany the end of an exclusive lock on a camera, and forgets the holder as
    ///     its controller. A generic simulation ownership packet cannot stand in for it, because it says nothing about
    ///     control. Call inside the gate.
    /// </summary>
    internal bool TryCreateControlRelease(NitroxId cameraId, SimulationOwnershipData.PlayerLock revokedLock, string reason,
        [NotNullWhen(true)] out MapRoomCameraControl? release) => TryCreateControlRelease(cameraId, revokedLock, reason, null, out release);

    /// <param name="removedCamera">The camera, if it has left the entity registry already and cannot be looked up.</param>
    private bool TryCreateControlRelease(NitroxId cameraId, SimulationOwnershipData.PlayerLock revokedLock, string reason, CameraView? removedCamera,
        [NotNullWhen(true)] out MapRoomCameraControl? release)
    {
        release = null;
        // The controller is forgotten even if an earlier bug or a future lifecycle path changed the lock type before
        // cleanup saw it. Only the end of an exclusive lock produces a release packet.
        EndControl(cameraId, revokedLock.Player.SessionId);
        if (revokedLock.LockType != SimulationLockType.EXCLUSIVE)
        {
            return false;
        }

        CameraView camera = removedCamera ?? GetCamera(cameraId);
        if (!camera.Exists)
        {
            return false;
        }

        Optional<NitroxId> roomId = camera.DockedRoom == null ? Optional.Empty : Optional.Of(camera.DockedRoom.Id);
        release = new MapRoomCameraControl(cameraId, roomId, camera.DockedSlot, false, camera.State.LightOn, true, true, revokedLock.Player.SessionId);
        diagnostics.RecordAccepted("control_revoke", camera.DockedRoom, cameraId, revokedLock.Player.SessionId, camera.DockedSlot, reason);
        return true;
    }

    /// <summary>
    ///     Generic ownership is legitimate only for Stalker interaction with a coherent loose world camera, or as an
    ///     idempotent exclusive refresh by the controller. A generic transient request may give back a Stalker-purpose
    ///     exclusive lock, but it cannot stand in for giving up control.
    /// </summary>
    private static bool CanGrantGenericOwnership(CameraView camera, bool requesterIsController, bool hasController,
        SimulationLockType requestedLock, bool requesterOwnsExclusive)
    {
        if (!camera.Exists)
        {
            return false;
        }
        return requestedLock switch
        {
            SimulationLockType.EXCLUSIVE when hasController => requesterIsController && requesterOwnsExclusive,
            SimulationLockType.EXCLUSIVE => camera.IsLooseWorldCamera,
            SimulationLockType.TRANSIENT => !hasController && requesterOwnsExclusive && camera.IsLooseWorldCamera,
            _ => false
        };
    }

    /// <summary>
    ///     The packet that announces control of a camera as it stands now, for a player who was not connected when it
    ///     was granted. It has the shape of the grant everyone else received, with the camera's current dock and light.
    /// </summary>
    private bool TryCreateCurrentGrant(NitroxId cameraId, SessionId controllerSessionId, [NotNullWhen(true)] out MapRoomCameraControl? grant)
    {
        grant = null;
        CameraView camera = GetCamera(cameraId);
        if (camera.WorldCamera == null)
        {
            return false;
        }

        Optional<NitroxId> roomId = camera.DockedRoom == null ? Optional.Empty : Optional.Of(camera.DockedRoom.Id);
        grant = new MapRoomCameraControl(cameraId, roomId, camera.DockedSlot, true, camera.State.LightOn, true, true, controllerSessionId);
        diagnostics.RecordAccepted("control_sync", camera.DockedRoom, cameraId, controllerSessionId, camera.DockedSlot, "current_pilot_sent_to_joined_player");
        return true;
    }

    /// <summary>
    ///     Ends the control lock of <paramref name="sender" />. A camera in a dock or out in the world keeps them as
    ///     its simulating player: battery and health reports are only accepted from that player, who also recharges a
    ///     docked camera, and a plain transient lock cannot be requested for a Scanner Room camera afterwards.
    /// </summary>
    /// <returns>The lock to announce, if the control lock became an ordinary simulation lock.</returns>
    private SimulationOwnershipChange? EndControlLock(NitroxId cameraId, Player sender)
    {
        if (GetCamera(cameraId).WorldCamera is not { } worldCamera)
        {
            simulationOwnershipData.RevokeIfOwner(cameraId, sender);
            return null;
        }
        if (simulationOwnershipData.TryGetLock(cameraId, out SimulationOwnershipData.PlayerLock heldLock) &&
            heldLock.Player == sender && heldLock.LockType == SimulationLockType.TRANSIENT)
        {
            // They only simulated the camera, so there is nothing to end.
            return null;
        }
        if (!simulationOwnershipData.TryAcquire(cameraId, sender, SimulationLockType.TRANSIENT))
        {
            return null;
        }
        return new SimulationOwnershipChange(cameraId, sender.SessionId, SimulationLockType.TRANSIENT,
            SimulationWhitelist.ShouldSimulateEntityMovement(worldCamera));
    }

    /// <summary>
    ///     The camera the sender controlled before taking another one. Its release may still be on its way or may never
    ///     come; without this the player would hold several cameras and keep everyone else out of them.
    /// </summary>
    private async Task EndControlOfPreviousCameraAsync(Player sender, NitroxId previousCameraId)
    {
        if (!IsController(previousCameraId, sender.SessionId))
        {
            return;
        }
        if (!simulationOwnershipData.TryGetLock(previousCameraId, out SimulationOwnershipData.PlayerLock previousLock) || previousLock.Player != sender)
        {
            EndControl(previousCameraId, sender.SessionId);
            return;
        }

        bool hasRelease = TryCreateControlRelease(previousCameraId, previousLock, "switch", out MapRoomCameraControl? release);
        SimulationOwnershipChange? keptSimulation = EndControlLock(previousCameraId, sender);
        if (hasRelease)
        {
            await SendToAllAsync(release!);
        }
        if (keptSimulation != null)
        {
            await SendToAllAsync(keptSimulation);
        }
    }

    /// <summary>
    ///     Why the game would not let this camera be controlled (MapRoomCamera.CanBeControlled), or null. Battery and
    ///     health are the values the server holds and every client was told.
    /// </summary>
    private string? GetUnusableReason(NitroxId cameraId, SubnauticaGameMode gameMode)
    {
        MapRoomCameraMetadata state = GetCamera(cameraId).State;
        return GetUnusableReason(state.Energy, state.Health, gameMode);
    }

    private bool IsValidAssociation(MapRoomCameraControl packet)
    {
        CameraView camera = GetCamera(packet.CameraId);
        if (!packet.IsControlling)
        {
            return camera.Exists;
        }
        // Only a camera in a dock or out in the world can be controlled, not one in an inventory; and an id can belong
        // to something that is no camera at all.
        if (camera.WorldCamera == null)
        {
            return false;
        }
        // The request says where the player's game has the camera. If the camera is somewhere else by now, the
        // player acts on something that has changed.
        return packet.MapRoomId.HasValue
            ? camera.DockedRoom != null && camera.DockedRoom.Id == packet.MapRoomId.Value && camera.State.DockingIndex == packet.CameraIndex
            : camera.DockedRoom == null;
    }

    /// <summary>
    ///     The number a camera's preview is shown under, which is the number the server gave the camera.
    /// </summary>
    private bool TryResolveCameraNumber(NitroxId cameraId, out MapRoomEntity? room, out int cameraNumber)
    {
        CameraView camera = GetCamera(cameraId);
        room = camera.DockedRoom;
        cameraNumber = camera.State.CameraNumber;
        return camera.WorldCamera != null && cameraNumber > 0;
    }

    private static MapRoomCameraControl CreateControlResponse(MapRoomCameraControl packet, bool granted, SessionId controller) =>
        new(packet.CameraId, packet.MapRoomId, packet.CameraIndex, packet.IsControlling, packet.LightOn, true, granted, controller);

    private static MapRoomCameraPreview CreatePreviewDenial(NitroxId cameraId, int cameraNumber) => new(cameraId, cameraNumber, [], true, false, 0);

    /// <summary>
    ///     Records a granted control. A replayed grant for the same player must not hand out a second preview.
    /// </summary>
    private void BeginControl(NitroxId cameraId, SessionId controllerSessionId)
    {
        if (!IsController(cameraId, controllerSessionId))
        {
            controllers[cameraId] = new ControlSession(controllerSessionId);
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

    private bool TryUsePreview(NitroxId cameraId, SessionId controllerSessionId)
    {
        if (!controllers.TryGetValue(cameraId, out ControlSession? session) || session.SessionId != controllerSessionId || session.PreviewUsed)
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
    }
}
