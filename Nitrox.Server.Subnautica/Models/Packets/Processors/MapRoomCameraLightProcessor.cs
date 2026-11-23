using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Bases;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;
using Nitrox.Server.Subnautica.Models.GameLogic;
using Nitrox.Server.Subnautica.Models.GameLogic.Entities;
using Nitrox.Server.Subnautica.Models.Packets.Core;

namespace Nitrox.Server.Subnautica.Models.Packets.Processors;

internal sealed class MapRoomCameraLightProcessor(SimulationOwnershipData simulationOwnershipData,
    EntityRegistry entityRegistry, MapRoomCameraControlLifecycle controlLifecycle,
    ScannerRoomDiagnostics diagnostics, MapRoomLooseCameraState looseCameraState) : IAuthPacketProcessor<MapRoomCameraLight>
{
    public async Task Process(AuthProcessorContext context, MapRoomCameraLight packet)
    {
        MapRoomEntity? mapRoom = FindUniqueRoom(packet.CameraId, out int registrations);
        if (!packet.IsServerResponse && registrations == 0 && looseCameraState.TryGetCamera(packet.CameraId, out WorldEntity looseCamera))
        {
            await ProcessLooseCameraAsync(context, packet, looseCamera);
            return;
        }
        if (packet.IsServerResponse || mapRoom == null)
        {
            diagnostics.RecordRejected("light", mapRoom, packet.CameraId, context.Sender.SessionId, reason:
                packet.IsServerResponse ? "server_response" : "invalid_assoc");
            await context.ReplyAsync(new MapRoomCameraLight(packet.CameraId, packet.On, 0, true, false));
            return;
        }

        long revision = 0;
        bool changed = false;
        bool accepted = false;
        string rejectionReason = "association_changed";
        Task sendTask = Task.CompletedTask;
        lock (mapRoom)
        {
            MapRoomCameraRecord? record = mapRoom.GetCameraRecord(packet.CameraId);
            sendTask = simulationOwnershipData.ExecuteForOwner(context.Sender, [packet.CameraId], ownedIds =>
            {
                bool hasCanonicalControl =
                    simulationOwnershipData.TryGetLock(packet.CameraId,
                        out SimulationOwnershipData.PlayerLock cameraLock) &&
                    cameraLock.Player == context.Sender &&
                    cameraLock.LockType == SimulationLockType.EXCLUSIVE &&
                    controlLifecycle.IsActiveController(packet.CameraId, context.Sender.SessionId);
                if (record == null || !ownedIds.Contains(packet.CameraId) || !hasCanonicalControl)
                {
                    rejectionReason = record == null ? "association_changed" :
                        !ownedIds.Contains(packet.CameraId) ? "non_owner" : "control_required";
                    return Task.CompletedTask;
                }

                lock (record)
                {
                    accepted = true;
                    changed = record.LightOn != packet.On;
                    revision = record.LightRevision;
                    if (changed)
                    {
                        revision++;
                        record.LightOn = packet.On;
                        record.LightRevision = revision;
                    }
                }

                // LiteNetLib enqueues synchronously. Queue the accepted state while ownership is
                // locked so a release or reassignment packet cannot overtake this transition.
                return context.SendToAllAsync(new MapRoomCameraLight(packet.CameraId, packet.On, revision, true, true));
            });
        }
        if (!accepted)
        {
            diagnostics.RecordRejected("light", mapRoom, packet.CameraId, context.Sender.SessionId,
                reason: rejectionReason);
            await context.ReplyAsync(new MapRoomCameraLight(packet.CameraId, packet.On, 0, true, false));
            return;
        }
        if (changed)
        {
            diagnostics.RecordAccepted("light", mapRoom, packet.CameraId, context.Sender.SessionId,
                reason: packet.On ? "on" : "off");
        }
        await sendTask;
    }

    /// <summary>
    ///     A camera no Scanner Room has a record for (never docked, dropped after a pickup, or its room is gone) keeps
    ///     its light state on its own entity. The same control rule applies as for a registered camera.
    /// </summary>
    private async Task ProcessLooseCameraAsync(AuthProcessorContext context, MapRoomCameraLight packet, WorldEntity looseCamera)
    {
        bool accepted = false;
        bool changed = false;
        string rejectionReason = "non_owner";
        Task sendTask = simulationOwnershipData.ExecuteForOwner(context.Sender, [packet.CameraId], ownedIds =>
        {
            bool hasCanonicalControl =
                simulationOwnershipData.TryGetLock(packet.CameraId, out SimulationOwnershipData.PlayerLock cameraLock) &&
                cameraLock.Player == context.Sender &&
                cameraLock.LockType == SimulationLockType.EXCLUSIVE &&
                controlLifecycle.IsActiveController(packet.CameraId, context.Sender.SessionId);
            if (!ownedIds.Contains(packet.CameraId) || !hasCanonicalControl)
            {
                rejectionReason = !ownedIds.Contains(packet.CameraId) ? "non_owner" : "control_required";
                return Task.CompletedTask;
            }

            MapRoomCameraMetadata state = looseCameraState.SetLight(looseCamera, packet.On, out changed);
            accepted = true;
            // Queued while ownership is locked, like the registered path, so a release cannot overtake it.
            return context.SendToAllAsync(new MapRoomCameraLight(packet.CameraId, state.LightOn, state.LightRevision, true, true));
        });
        if (!accepted)
        {
            diagnostics.RecordRejected("light", null, packet.CameraId, context.Sender.SessionId, reason: rejectionReason);
            await context.ReplyAsync(new MapRoomCameraLight(packet.CameraId, packet.On, 0, true, false));
            return;
        }
        if (changed)
        {
            diagnostics.RecordAccepted("light", null, packet.CameraId, context.Sender.SessionId,
                reason: packet.On ? "on_loose" : "off_loose");
        }
        await sendTask;
    }

    private MapRoomEntity? FindUniqueRoom(NitroxId cameraId, out int registrations)
    {
        MapRoomEntity? found = null;
        registrations = 0;
        foreach (MapRoomEntity room in entityRegistry.GetEntities<MapRoomEntity>())
        {
            lock (room)
            {
                if (room.GetCameraRecord(cameraId) == null)
                {
                    continue;
                }
                registrations++;
                found = room;
            }
        }
        return registrations == 1 ? found : null;
    }
}
