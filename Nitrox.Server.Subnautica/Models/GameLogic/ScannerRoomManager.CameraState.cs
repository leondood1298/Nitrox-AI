using System.Collections.Generic;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Bases;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;

namespace Nitrox.Server.Subnautica.Models.GameLogic;

/// <summary>
///     Light, battery and health of every camera. A camera that a room has a record for keeps them in that record; a
///     camera no room has a record for (never docked, dropped after a pickup, or its room is gone) keeps them on its
///     own entity as <see cref="MapRoomCameraMetadata" />, so they are saved and sent to joining players with it.
/// </summary>
internal sealed partial class ScannerRoomManager
{
    /// <summary>
    ///     The last docking revision each removed room used, see <see cref="RememberFloor(MapRoomEntity)" />.
    /// </summary>
    private readonly Dictionary<NitroxId, long> dockingRevisionFloors = [];

    private readonly Dictionary<NitroxId, CameraRevisions> cameraRevisionFloors = [];

    /// <summary>
    ///     Handles a controlling player's report that they switched a camera's light, and tells everyone if it counts.
    /// </summary>
    public async Task SetCameraLightAsync(Player sender, MapRoomCameraLight packet)
    {
        using Hold held = await EnterAsync();
        CameraTopology topology = packet.CameraId == null ? default : GetTopology(packet.CameraId);
        MapRoomEntity? mapRoom = topology.UniqueRoom;
        WorldEntity? looseCamera = null;
        bool isLoose = !packet.IsServerResponse && packet.CameraId != null && topology.Registrations == 0 && TryGetWorldCamera(packet.CameraId, out looseCamera);
        if (!isLoose && (packet.IsServerResponse || mapRoom == null))
        {
            diagnostics.RecordRejected("light", mapRoom, packet.CameraId, sender.SessionId, reason: packet.IsServerResponse ? "server_response" : "invalid_assoc");
            await ReplyAsync(sender, new MapRoomCameraLight(packet.CameraId, packet.On, 0, true, false));
            return;
        }

        // A camera with a record keeps its light there, any other camera on its own entity.
        MapRoomCameraRecord? record = mapRoom?.GetCameraRecord(packet.CameraId);
        bool accepted = false;
        bool changed = false;
        string rejectionReason = "non_owner";
        ValueTask sendTask = simulationOwnershipData.ExecuteForOwner(sender, [packet.CameraId], ownedIds =>
        {
            bool hasCanonicalControl = simulationOwnershipData.TryGetLock(packet.CameraId, out SimulationOwnershipData.PlayerLock cameraLock) &&
                                       cameraLock.Player == sender && cameraLock.LockType == SimulationLockType.EXCLUSIVE &&
                                       IsController(packet.CameraId, sender.SessionId);
            if (!ownedIds.Contains(packet.CameraId) || !hasCanonicalControl)
            {
                rejectionReason = !ownedIds.Contains(packet.CameraId) ? "non_owner" : "control_required";
                return ValueTask.CompletedTask;
            }

            accepted = true;
            long revision;
            if (record != null)
            {
                changed = record.LightOn != packet.On;
                if (changed)
                {
                    record.LightOn = packet.On;
                    record.LightRevision++;
                }
                revision = record.LightRevision;
            }
            else
            {
                revision = SetLooseLight(looseCamera!, packet.On, out changed).LightRevision;
            }

            // The packet sender queues synchronously. Queued while ownership is locked, a release or reassignment
            // packet cannot overtake this change.
            return SendToAllAsync(new MapRoomCameraLight(packet.CameraId, packet.On, revision, true, true));
        });
        if (!accepted)
        {
            diagnostics.RecordRejected("light", mapRoom, packet.CameraId, sender.SessionId, reason: rejectionReason);
            await ReplyAsync(sender, new MapRoomCameraLight(packet.CameraId, packet.On, 0, true, false));
            return;
        }
        if (changed)
        {
            diagnostics.RecordAccepted("light", mapRoom, packet.CameraId, sender.SessionId,
                reason: isLoose ? packet.On ? "on_loose" : "off_loose" : packet.On ? "on" : "off");
        }
        await sendTask;
    }

    /// <summary>
    ///     Handles a report of a camera's battery and health from the player who simulates it (or, for a docked camera
    ///     nobody controls, from the room's simulating player, who recharges it), and tells everyone if it counts.
    /// </summary>
    public async Task SetCameraComponentsAsync(Player sender, MapRoomCameraComponentState packet)
    {
        using Hold held = await EnterAsync();
        CameraTopology topology = packet.CameraId == null ? default : GetTopology(packet.CameraId);
        MapRoomEntity? room = topology.UniqueRoom;
        WorldEntity? looseCamera = null;
        bool isLoose = !packet.IsServerResponse && packet.CameraId != null && topology.Registrations == 0 && IsValidComponentState(packet.Energy, packet.Health) &&
                       TryGetWorldCamera(packet.CameraId, out looseCamera);
        if (!isLoose && (packet.IsServerResponse || room == null || !IsValidComponentState(packet.Energy, packet.Health)))
        {
            diagnostics.RecordRejected("component", room, packet.CameraId, sender.SessionId, reason:
                packet.IsServerResponse ? "server_response" : room == null ? "invalid_assoc" : "invalid_value");
            await ReplyAsync(sender, new MapRoomCameraComponentState(packet.CameraId, packet.Energy, packet.Health, 0, true, false));
            return;
        }

        // A camera with a record keeps its battery and health there, any other camera on its own entity.
        MapRoomCameraRecord? record = room?.GetCameraRecord(packet.CameraId);
        float previousEnergy = 0f;
        float previousHealth = 0f;
        float acceptedEnergy = 0f;
        float acceptedHealth = 0f;
        bool changed = false;
        bool accepted = false;
        string rejectionReason = "non_owner";
        ValueTask sendTask = simulationOwnershipData.ExecuteForOwner(sender, room == null ? [packet.CameraId] : [packet.CameraId, room.Id], ownedIds =>
        {
            long acceptedRevision;
            if (record == null || room == null)
            {
                // Whoever simulates the camera may report for it.
                if (!ownedIds.Contains(packet.CameraId))
                {
                    return ValueTask.CompletedTask;
                }
                MapRoomCameraMetadata state = SetLooseComponents(looseCamera!, packet.Energy, packet.Health, out MapRoomCameraMetadata previous);
                previousEnergy = previous.Energy;
                previousHealth = previous.Health;
                acceptedEnergy = state.Energy;
                acceptedHealth = state.Health;
                acceptedRevision = state.ComponentRevision;
                changed = true;
            }
            else
            {
                bool hasCameraLock = simulationOwnershipData.TryGetLock(packet.CameraId, out SimulationOwnershipData.PlayerLock cameraLock);
                bool senderIsCameraOwner = ownedIds.Contains(packet.CameraId);
                bool hasExclusiveCameraOwner = hasCameraLock && cameraLock.LockType == SimulationLockType.EXCLUSIVE;
                bool senderIsDockedRoomOwner = room.IsCameraDocked(packet.CameraId) && ownedIds.Contains(room.Id);
                if (!senderIsCameraOwner && (!senderIsDockedRoomOwner || hasExclusiveCameraOwner))
                {
                    rejectionReason = hasExclusiveCameraOwner ? "exclusive_owner" : "non_owner";
                    return ValueTask.CompletedTask;
                }

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
            accepted = true;

            // The packet sender queues synchronously. Queued while ownership is locked, a release or reassignment
            // packet cannot overtake this change.
            return SendToAllAsync(new MapRoomCameraComponentState(packet.CameraId, acceptedEnergy, acceptedHealth, acceptedRevision, true, true));
        });
        if (!accepted)
        {
            diagnostics.RecordRejected("component", room, packet.CameraId, sender.SessionId, reason: rejectionReason);
            await ReplyAsync(sender, new MapRoomCameraComponentState(packet.CameraId, packet.Energy, packet.Health, 0, true, false));
            return;
        }
        // A loose camera's battery drains while it is flown, which is not worth a line; its health is.
        if (!isLoose && changed && GetEnergyBand(previousEnergy) != GetEnergyBand(acceptedEnergy))
        {
            diagnostics.RecordAccepted("component_energy", room, packet.CameraId, sender.SessionId, reason: $"band_{GetEnergyBand(acceptedEnergy)}");
        }
        if (changed && GetHealthBand(previousHealth) != GetHealthBand(acceptedHealth))
        {
            string reason = previousHealth > 0f && acceptedHealth <= 0f ? "death" : previousHealth <= 0f && acceptedHealth > 0f ? "repair" : $"band_{GetHealthBand(acceptedHealth)}";
            diagnostics.RecordAccepted("component_health", room, packet.CameraId, sender.SessionId, reason: reason);
        }
        await sendTask;
    }

    /// <summary>
    ///     Called before a camera's entity is replaced by another one with the same id: picked up into an inventory, or
    ///     dropped back into the world. The replacement is built by a client and carries no camera state, so the state
    ///     the server holds moves over to it. Without this the camera would come back with a full battery and
    ///     revisions that start again at zero, which clients that saw the earlier ones discard as stale. Anything the
    ///     client did put there is dropped: only the light and component packets may change this state.
    /// </summary>
    public void KeepCameraStateOnReplacement(Entity replacement)
    {
        if (!mapRoomCameraTechType.Equals(replacement.TechType))
        {
            return;
        }
        using Hold held = Enter();
        KeepCameraStateOnReplacementInGate(replacement);
    }

    internal static bool IsValidComponentState(float energy, float health) =>
        float.IsFinite(energy) && energy is >= 0f and <= MapRoomCameraRecord.MAX_ENERGY &&
        float.IsFinite(health) && health is >= 0f and <= MapRoomCameraRecord.MAX_HEALTH;

    internal static int GetEnergyBand(float energy) => MapRoomCameraRecord.GetComponentBand(energy, MapRoomCameraRecord.MAX_ENERGY);

    internal static int GetHealthBand(float health) => MapRoomCameraRecord.GetComponentBand(health, MapRoomCameraRecord.MAX_HEALTH);

    private void KeepCameraStateOnReplacementInGate(Entity replacement)
    {
        if (mapRoomCameraTechType.Equals(replacement.TechType))
        {
            replacement.Metadata = entityRegistry.TryGetEntityById(replacement.Id, out Entity? current) ? current.Metadata as MapRoomCameraMetadata : null;
        }
    }

    /// <summary>
    ///     Stores a new light state on a camera's own entity. The revision only advances when the light actually changes.
    /// </summary>
    private static MapRoomCameraMetadata SetLooseLight(Entity camera, bool lightOn, out bool changed)
    {
        MapRoomCameraMetadata current = ReadLooseState(camera);
        changed = current.LightOn != lightOn;
        if (changed)
        {
            current = new MapRoomCameraMetadata(lightOn, current.LightRevision + 1, current.Energy, current.Health, current.ComponentRevision);
            camera.Metadata = current;
        }
        return current;
    }

    /// <summary>
    ///     Stores new battery and health values on a camera's own entity. The revision only advances when a value
    ///     actually changes.
    /// </summary>
    private static MapRoomCameraMetadata SetLooseComponents(Entity camera, float energy, float health, out MapRoomCameraMetadata previous)
    {
        previous = ReadLooseState(camera);
        if (previous.Energy == energy && previous.Health == health)
        {
            return previous;
        }
        MapRoomCameraMetadata updated = new(previous.LightOn, previous.LightRevision, energy, health, previous.ComponentRevision + 1);
        camera.Metadata = updated;
        return updated;
    }

    /// <summary>
    ///     Called when a camera gets its first record in a room. The record takes over the camera's state, keeping the
    ///     revisions so clients that already saw them accept what follows.
    /// </summary>
    private void MoveStateIntoRecord(NitroxId cameraId, MapRoomCameraRecord record)
    {
        if (!TryGetWorldCamera(cameraId, out WorldEntity? camera) || camera.Metadata is not MapRoomCameraMetadata loose)
        {
            return;
        }
        record.LightOn = loose.LightOn;
        record.LightRevision = loose.LightRevision;
        record.Energy = loose.Energy;
        record.Health = loose.Health;
        record.ComponentRevision = loose.ComponentRevision;
        camera.Metadata = null;
    }

    /// <summary>
    ///     Called when a room is removed while one of its cameras still exists, out in the world or in an inventory.
    ///     The camera keeps the state its record held.
    /// </summary>
    private void MoveStateOutOfRecord(MapRoomCameraRecord record)
    {
        if (entityRegistry.TryGetEntityById(record.CameraId, out Entity? camera) && mapRoomCameraTechType.Equals(camera.TechType))
        {
            camera.Metadata = new MapRoomCameraMetadata(record.LightOn, record.LightRevision, record.Energy, record.Health, record.ComponentRevision);
        }
    }

    private static MapRoomCameraMetadata ReadLooseState(Entity camera) => camera.Metadata as MapRoomCameraMetadata ?? new MapRoomCameraMetadata();

    /// <summary>
    ///     Remembers the last docking revision a removed room used, so the same id never starts over. A room that is
    ///     deconstructed and built again keeps its id, and the ids of its two default cameras are derived from it.
    ///     Clients keep the highest docking, light and component revision they saw per id for as long as they are
    ///     connected and discard anything lower, so a new room or camera record starting at zero would be ignored by
    ///     every player who saw the old one. Scan state needs no floor: clients forget it when their copy of the
    ///     room is destroyed. Kept in memory only: after a restart every client reconnects with nothing remembered.
    /// </summary>
    private void RememberFloor(MapRoomEntity removedRoom) =>
        dockingRevisionFloors[removedRoom.Id] = Math.Max(dockingRevisionFloors.GetValueOrDefault(removedRoom.Id), removedRoom.DockingRevision);

    /// <summary>
    ///     Called when a camera's record is removed for good: the camera was destroyed, alone or with its room.
    /// </summary>
    private void RememberFloor(MapRoomCameraRecord removedRecord)
    {
        CameraRevisions known = cameraRevisionFloors.GetValueOrDefault(removedRecord.CameraId);
        cameraRevisionFloors[removedRecord.CameraId] = new CameraRevisions(Math.Max(known.Light, removedRecord.LightRevision),
            Math.Max(known.Component, removedRecord.ComponentRevision));
    }

    /// <summary>
    ///     Called before a newly built room becomes reachable. Its docking revision starts above the one its id used.
    /// </summary>
    private void RaiseAboveFloor(MapRoomEntity newRoom)
    {
        if (dockingRevisionFloors.TryGetValue(newRoom.Id, out long used))
        {
            newRoom.DockingRevision = Math.Max(newRoom.DockingRevision, used + 1);
        }
    }

    /// <summary>
    ///     Called when a camera gets a new record. Its light and component revisions start above the ones its id used.
    /// </summary>
    private void RaiseAboveFloor(MapRoomCameraRecord newRecord)
    {
        if (cameraRevisionFloors.TryGetValue(newRecord.CameraId, out CameraRevisions used))
        {
            newRecord.LightRevision = Math.Max(newRecord.LightRevision, used.Light + 1);
            newRecord.ComponentRevision = Math.Max(newRecord.ComponentRevision, used.Component + 1);
        }
    }

    private readonly record struct CameraRevisions(long Light, long Component);
}
