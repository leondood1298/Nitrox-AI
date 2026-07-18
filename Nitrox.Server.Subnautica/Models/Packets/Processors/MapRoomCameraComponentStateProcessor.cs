using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Bases;
using Nitrox.Model.Subnautica.Packets;
using Nitrox.Server.Subnautica.Models.GameLogic;
using Nitrox.Server.Subnautica.Models.GameLogic.Entities;
using Nitrox.Server.Subnautica.Models.Packets.Core;

namespace Nitrox.Server.Subnautica.Models.Packets.Processors;

internal sealed class MapRoomCameraComponentStateProcessor(SimulationOwnershipData simulationOwnershipData, EntityRegistry entityRegistry, ScannerRoomDiagnostics diagnostics) : IAuthPacketProcessor<MapRoomCameraComponentState>
{
    public async Task Process(AuthProcessorContext context, MapRoomCameraComponentState packet)
    {
        MapRoomEntity? room = FindUniqueRoom(packet.CameraId);
        if (packet.IsServerResponse || room == null || !IsValidComponentState(packet.Energy, packet.Health))
        {
            diagnostics.RecordRejected("component", room, packet.CameraId, context.Sender.SessionId, reason:
                packet.IsServerResponse ? "server_response" : room == null ? "invalid_assoc" : "invalid_value");
            await context.ReplyAsync(new MapRoomCameraComponentState(packet.CameraId, packet.Energy, packet.Health, 0, true, false));
            return;
        }

        float previousEnergy = 0f;
        float previousHealth = 0f;
        float acceptedEnergy = 0f;
        float acceptedHealth = 0f;
        long acceptedRevision = 0;
        bool changed = false;
        bool accepted = false;
        string rejectionReason = "association_changed";
        Task sendTask = Task.CompletedTask;
        lock (room)
        {
            MapRoomCameraRecord? record = room.GetCameraRecord(packet.CameraId);
            sendTask = simulationOwnershipData.ExecuteForOwner(context.Sender, [packet.CameraId, room.Id], ownedIds =>
            {
                bool hasCameraLock = simulationOwnershipData.TryGetLock(packet.CameraId, out SimulationOwnershipData.PlayerLock cameraLock);
                bool senderIsCameraOwner = ownedIds.Contains(packet.CameraId);
                bool hasExclusiveCameraOwner = hasCameraLock && cameraLock.LockType == SimulationLockType.EXCLUSIVE;
                bool senderIsDockedRoomOwner = record != null && room.IsCameraDocked(packet.CameraId) && ownedIds.Contains(room.Id);
                bool senderHasAuthority = senderIsCameraOwner || senderIsDockedRoomOwner && !hasExclusiveCameraOwner;
                rejectionReason = record == null ? "association_changed" :
                                  senderHasAuthority ? "-" : hasExclusiveCameraOwner ? "exclusive_owner" : "non_owner";
                if (record == null || !senderHasAuthority)
                {
                    return Task.CompletedTask;
                }

                lock (record)
                {
                    accepted = true;
                    previousEnergy = record.Energy;
                    previousHealth = record.Health;
                    changed = previousEnergy != packet.Energy || previousHealth != packet.Health;
                    if (changed)
                    {
                        record.Energy = packet.Energy;
                        record.Health = packet.Health;
                        record.ComponentRevision++;
                    }
                    acceptedEnergy = record.Energy;
                    acceptedHealth = record.Health;
                    acceptedRevision = record.ComponentRevision;
                }

                // LiteNetLib enqueues synchronously. Queue the accepted state while ownership is
                // locked so a release or reassignment packet cannot overtake this transition.
                return context.SendToAllAsync(new MapRoomCameraComponentState(packet.CameraId, acceptedEnergy,
                    acceptedHealth, acceptedRevision, true, true));
            });
        }
        if (!accepted)
        {
            diagnostics.RecordRejected("component", room, packet.CameraId, context.Sender.SessionId, reason: rejectionReason);
            await context.ReplyAsync(new MapRoomCameraComponentState(packet.CameraId, packet.Energy, packet.Health, 0, true, false));
            return;
        }
        if (changed && GetComponentBand(previousEnergy) != GetComponentBand(acceptedEnergy))
        {
            diagnostics.RecordAccepted("component_energy", room, packet.CameraId, context.Sender.SessionId, reason: $"band_{GetComponentBand(acceptedEnergy)}");
        }
        if (changed && GetComponentBand(previousHealth) != GetComponentBand(acceptedHealth))
        {
            string reason = previousHealth > 0f && acceptedHealth <= 0f ? "death" : previousHealth <= 0f && acceptedHealth > 0f ? "repair" : $"band_{GetComponentBand(acceptedHealth)}";
            diagnostics.RecordAccepted("component_health", room, packet.CameraId, context.Sender.SessionId, reason: reason);
        }
        await sendTask;
    }

    private MapRoomEntity? FindUniqueRoom(NitroxId cameraId)
    {
        MapRoomEntity? found = null;
        foreach (MapRoomEntity candidate in entityRegistry.GetEntities<MapRoomEntity>())
        {
            lock (candidate)
            {
                if (candidate.GetCameraRecord(cameraId) == null)
                {
                    continue;
                }
                if (found != null)
                {
                    return null;
                }
                found = candidate;
            }
        }
        return found;
    }

    internal static bool IsValidComponentState(float energy, float health) =>
        float.IsFinite(energy) && energy is >= 0f and <= 100f && float.IsFinite(health) && health is >= 0f and <= 100f;

    internal static int GetComponentBand(float value) => value switch
    {
        <= 0f => 0,
        <= 10f => 10,
        <= 25f => 25,
        <= 50f => 50,
        <= 75f => 75,
        _ => 100
    };
}
