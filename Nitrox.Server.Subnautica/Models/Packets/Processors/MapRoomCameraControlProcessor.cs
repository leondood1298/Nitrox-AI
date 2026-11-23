using System.Threading.Tasks;
using Nitrox.Model.Packets.Core;
using Nitrox.Model.Subnautica.Packets;
using Nitrox.Model.Core;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Bases;
using Nitrox.Model.Subnautica.Helper;
using Nitrox.Server.Subnautica.Models.GameLogic;
using Nitrox.Server.Subnautica.Models.GameLogic.Entities;
using Nitrox.Server.Subnautica.Models.Packets.Core;

namespace Nitrox.Server.Subnautica.Models.Packets.Processors;

internal sealed class MapRoomCameraControlProcessor(SimulationOwnershipData simulationOwnershipData, EntityRegistry entityRegistry,
    MapRoomCameraControlLifecycle controlLifecycle, MapRoomCameraControlReleaseFactory cameraControlReleaseFactory,
    ScannerRoomDiagnostics diagnostics) : IAuthPacketProcessor<MapRoomCameraControl>
{
    private readonly SimulationOwnershipData simulationOwnershipData = simulationOwnershipData;
    private readonly EntityRegistry entityRegistry = entityRegistry;
    private readonly MapRoomCameraControlLifecycle controlLifecycle = controlLifecycle;
    private readonly MapRoomCameraControlReleaseFactory cameraControlReleaseFactory = cameraControlReleaseFactory;
    private readonly ScannerRoomDiagnostics diagnostics = diagnostics;

    public async Task Process(AuthProcessorContext context, MapRoomCameraControl packet)
    {
        if (packet.IsServerResponse || !IsValidAssociation(packet))
        {
            diagnostics.RecordRejected("control", GetRoom(packet.MapRoomId), packet.CameraId, context.Sender.SessionId, packet.CameraIndex,
                packet.IsServerResponse ? "server_response" : "invalid_assoc");
            await context.ReplyAsync(CreateResponse(packet, false, context.Sender.SessionId));
            return;
        }
        using IDisposable lifecycleGate = await controlLifecycle.EnterAsync(packet.CameraId);
        if (!IsValidAssociation(packet))
        {
            diagnostics.RecordRejected("control", GetRoom(packet.MapRoomId), packet.CameraId, context.Sender.SessionId,
                packet.CameraIndex, "association_changed");
            await context.ReplyAsync(CreateResponse(packet, false, context.Sender.SessionId));
            return;
        }

        if (packet.IsControlling)
        {
            bool takenFromAnotherPlayer = simulationOwnershipData.TryGetLock(packet.CameraId, out SimulationOwnershipData.PlayerLock previousLock) &&
                                          previousLock.Player != context.Sender;
            bool granted = simulationOwnershipData.TryAcquire(packet.CameraId, context.Sender, SimulationLockType.EXCLUSIVE);
            if (!granted && takenFromAnotherPlayer && CanPreempt(previousLock.LockType, controlLifecycle.IsActiveController(packet.CameraId, previousLock.Player.SessionId)))
            {
                simulationOwnershipData.ForceAcquire(packet.CameraId, context.Sender, SimulationLockType.EXCLUSIVE);
                granted = true;
            }
            SessionId controller = granted ? context.Sender.SessionId : simulationOwnershipData.GetPlayerForLock(packet.CameraId)?.SessionId ?? context.Sender.SessionId;
            MapRoomCameraControl response = CreateResponse(packet, granted, controller);
            if (granted)
            {
                controlLifecycle.BeginPreviewAcquisition(packet.CameraId, context.Sender.SessionId);
                diagnostics.RecordAccepted("control_acquire", GetRoom(packet.MapRoomId), packet.CameraId, context.Sender.SessionId, packet.CameraIndex);
                await context.SendToAllAsync(response);
                if (takenFromAnotherPlayer)
                {
                    // The player who simulated the camera, or whose Stalker carried it, holds a lock in the ordinary
                    // simulation bookkeeping of their client. That only ends when they are told who has it now.
                    await context.SendToOthersAsync(new SimulationOwnershipChange(packet.CameraId, context.Sender.SessionId, SimulationLockType.EXCLUSIVE));
                }
            }
            else
            {
                diagnostics.RecordRejected("control_acquire", GetRoom(packet.MapRoomId), packet.CameraId, context.Sender.SessionId, packet.CameraIndex, "locked");
                await context.ReplyAsync(response);
            }
            return;
        }

        Player? currentOwner = simulationOwnershipData.GetPlayerForLock(packet.CameraId);
        if (CanAcknowledgeRelease(currentOwner != null, currentOwner == context.Sender))
        {
            controlLifecycle.EndPreviewAcquisition(packet.CameraId, context.Sender.SessionId);
            SimulationOwnershipChange? keptSimulation = currentOwner != null ? EndControlLock(packet.CameraId, context.Sender) : null;
            diagnostics.RecordAccepted("control_release", GetRoom(packet.MapRoomId), packet.CameraId, context.Sender.SessionId, packet.CameraIndex,
                currentOwner == null ? "idempotent" : null);
            await context.SendToAllAsync(CreateResponse(packet, true, context.Sender.SessionId));
            if (keptSimulation != null)
            {
                // Sent after the release, so no client learns of the new lock while it still counts the sender as controlling.
                await context.SendToAllAsync(keptSimulation);
            }
        }
        else
        {
            diagnostics.RecordRejected("control_release", GetRoom(packet.MapRoomId), packet.CameraId, context.Sender.SessionId, packet.CameraIndex, "non_owner");
            await context.ReplyAsync(CreateResponse(packet, false, simulationOwnershipData.GetPlayerForLock(packet.CameraId)?.SessionId ?? context.Sender.SessionId));
        }
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

    /// <summary>
    ///     Ends the control lock of <paramref name="sender" />. A camera out in the world keeps them as its simulating
    ///     player: battery and health reports are only accepted from that player, and a plain transient lock cannot be
    ///     requested for a Scanner Room camera afterwards. A docked camera is a record of its room and is left without
    ///     a lock.
    /// </summary>
    /// <returns>The lock to announce, if the control lock became an ordinary simulation lock.</returns>
    private SimulationOwnershipChange? EndControlLock(NitroxId cameraId, Player sender)
    {
        if (!cameraControlReleaseFactory.TryGetLooseWorldCamera(cameraId, out WorldEntity? looseCamera))
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
            SimulationWhitelist.ShouldSimulateEntityMovement(looseCamera));
    }

    private bool IsValidAssociation(MapRoomCameraControl packet)
    {
        if (!packet.IsControlling)
        {
            return IsKnownScannerCamera(packet.CameraId);
        }
        if (packet.MapRoomId.HasValue)
        {
            if (packet.CameraIndex is < 0 or > 1 || !entityRegistry.TryGetEntityById(packet.MapRoomId.Value, out MapRoomEntity mapRoom))
            {
                return false;
            }
            lock (mapRoom)
            {
                return mapRoom.GetCameraRecord(packet.CameraId) != null && mapRoom.GetDockedCamera(packet.CameraIndex) == packet.CameraId;
            }
        }

        int registrations = 0;
        bool isDocked = false;
        foreach (MapRoomEntity room in entityRegistry.GetEntities<MapRoomEntity>())
        {
            lock (room)
            {
                if (room.GetCameraRecord(packet.CameraId) != null)
                {
                    registrations++;
                    isDocked |= room.IsCameraDocked(packet.CameraId);
                }
            }
        }
        bool validWorldCamera = entityRegistry.TryGetEntityById(packet.CameraId, out WorldEntity worldCamera) &&
            new NitroxTechType("MapRoomCamera").Equals(worldCamera.TechType);
        return IsValidLooseAssociation(validWorldCamera, registrations, isDocked);
    }

    internal static bool IsValidLooseAssociation(bool validWorldCamera, int registrations, bool isDocked) =>
        registrations == 0 ? validWorldCamera : registrations == 1 && !isDocked;

    private bool IsKnownScannerCamera(NitroxId cameraId)
    {
        if (entityRegistry.TryGetEntityById(cameraId, out WorldEntity worldCamera) &&
            new NitroxTechType("MapRoomCamera").Equals(worldCamera.TechType))
        {
            return true;
        }
        foreach (MapRoomEntity room in entityRegistry.GetEntities<MapRoomEntity>())
        {
            lock (room)
            {
                if (room.GetCameraRecord(cameraId) != null)
                {
                    return true;
                }
            }
        }
        return false;
    }

    private MapRoomEntity? GetRoom(Optional<NitroxId> roomId) =>
        roomId.HasValue && entityRegistry.TryGetEntityById(roomId.Value, out MapRoomEntity room) ? room : null;

    private static MapRoomCameraControl CreateResponse(MapRoomCameraControl packet, bool granted, SessionId controller) =>
        new(packet.CameraId, packet.MapRoomId, packet.CameraIndex, packet.IsControlling, packet.LightOn, true, granted, controller);
}
