using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;

namespace Nitrox.Server.Subnautica.Models.GameLogic;

/// <summary>
///     Light, battery, health and number of every camera. A camera keeps them on its own entity as
///     <see cref="MapRoomCameraMetadata" />, whether it is docked, loose in the world or in an inventory, so they are
///     saved and sent to joining players with it.
/// </summary>
internal sealed partial class ScannerRoomManager
{
    /// <summary>
    ///     The highest camera number handed out so far. Numbers are unique in the world and never reused while the
    ///     server runs: the game has one list of all cameras, and every screen cycles through all of them.
    /// </summary>
    private int lastCameraNumber;

    /// <summary>
    ///     Handles a controlling player's report that they switched a camera's light, and tells everyone if it counts.
    /// </summary>
    public async Task SetCameraLightAsync(Player sender, MapRoomCameraLight packet)
    {
        using Hold held = await EnterAsync();
        CameraView camera = GetCamera(packet.CameraId);
        if (packet.IsServerResponse || camera.WorldCamera is not { } worldCamera)
        {
            diagnostics.RecordRejected("light", camera.DockedRoom, packet.CameraId, sender.SessionId, reason: packet.IsServerResponse ? "server_response" : "invalid_assoc");
            await ReplyAsync(sender, new MapRoomCameraLight(packet.CameraId, packet.On, 0, true, false));
            return;
        }

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
            long revision = SetLight(worldCamera, packet.On, out changed).LightRevision;

            // The packet sender queues synchronously. Queued while ownership is locked, a release or reassignment
            // packet cannot overtake this change.
            return SendToAllAsync(new MapRoomCameraLight(packet.CameraId, packet.On, revision, true, true));
        });
        if (!accepted)
        {
            diagnostics.RecordRejected("light", camera.DockedRoom, packet.CameraId, sender.SessionId, reason: rejectionReason);
            await ReplyAsync(sender, new MapRoomCameraLight(packet.CameraId, packet.On, 0, true, false));
            return;
        }
        if (changed)
        {
            diagnostics.RecordAccepted("light", camera.DockedRoom, packet.CameraId, sender.SessionId,
                reason: camera.DockedRoom == null ? packet.On ? "on_loose" : "off_loose" : packet.On ? "on" : "off");
        }
        await sendTask;
    }

    /// <summary>
    ///     Handles a report of a camera's battery and health from the player who simulates it, and tells everyone if it
    ///     counts. A docked camera is recharged by its simulating player like any other.
    /// </summary>
    public async Task SetCameraComponentsAsync(Player sender, MapRoomCameraComponentState packet)
    {
        using Hold held = await EnterAsync();
        CameraView camera = GetCamera(packet.CameraId);
        if (packet.IsServerResponse || camera.WorldCamera is not { } worldCamera || !IsValidComponentState(packet.Energy, packet.Health))
        {
            diagnostics.RecordRejected("component", camera.DockedRoom, packet.CameraId, sender.SessionId, reason:
                packet.IsServerResponse ? "server_response" : camera.WorldCamera == null ? "invalid_assoc" : "invalid_value");
            await ReplyAsync(sender, new MapRoomCameraComponentState(packet.CameraId, packet.Energy, packet.Health, 0, true, false));
            return;
        }

        MapRoomCameraMetadata previous = camera.State;
        MapRoomCameraMetadata stored = previous;
        bool accepted = false;
        ValueTask sendTask = simulationOwnershipData.ExecuteForOwner(sender, [packet.CameraId], ownedIds =>
        {
            if (!ownedIds.Contains(packet.CameraId))
            {
                return ValueTask.CompletedTask;
            }
            accepted = true;
            stored = SetComponents(worldCamera, packet.Energy, packet.Health);

            // The packet sender queues synchronously. Queued while ownership is locked, a release or reassignment
            // packet cannot overtake this change.
            return SendToAllAsync(new MapRoomCameraComponentState(packet.CameraId, stored.Energy, stored.Health, stored.ComponentRevision, true, true));
        });
        if (!accepted)
        {
            diagnostics.RecordRejected("component", camera.DockedRoom, packet.CameraId, sender.SessionId, reason: "non_owner");
            await ReplyAsync(sender, new MapRoomCameraComponentState(packet.CameraId, packet.Energy, packet.Health, 0, true, false));
            return;
        }
        // The battery of a camera that is flown drains all the time, which is not worth a line; its health is.
        if (camera.DockedRoom != null && GetEnergyBand(previous.Energy) != GetEnergyBand(stored.Energy))
        {
            diagnostics.RecordAccepted("component_energy", camera.DockedRoom, packet.CameraId, sender.SessionId, reason: $"band_{GetEnergyBand(stored.Energy)}");
        }
        if (GetHealthBand(previous.Health) != GetHealthBand(stored.Health))
        {
            string reason = previous.Health > 0f && stored.Health <= 0f ? "death" : previous.Health <= 0f && stored.Health > 0f ? "repair" : $"band_{GetHealthBand(stored.Health)}";
            diagnostics.RecordAccepted("component_health", camera.DockedRoom, packet.CameraId, sender.SessionId, reason: reason);
        }
        await sendTask;
    }

    /// <summary>
    ///     Called before a camera's entity is replaced by another one with the same id (picked up into an inventory,
    ///     dropped back into the world), and before a camera a client made is stored for the first time. The entity is
    ///     built by a client and carries no camera state, so the state the server holds moves over to it; a camera
    ///     that is new to the server gets its number. Anything the client did put there is dropped: only the light and
    ///     component packets may change this state.
    /// </summary>
    /// <returns>The state the camera has now, for the client that built the entity, or null if it is no camera.</returns>
    public MapRoomCameraMetadata? KeepCameraStateOnReplacement(Entity replacement)
    {
        if (!IsCamera(replacement))
        {
            return null;
        }
        using Hold held = Enter();
        return KeepCameraStateOnReplacementInGate(replacement);
    }

    internal static bool IsValidComponentState(float energy, float health) =>
        float.IsFinite(energy) && energy is >= 0f and <= MapRoomCameraMetadata.MAX_ENERGY &&
        float.IsFinite(health) && health is >= 0f and <= MapRoomCameraMetadata.MAX_HEALTH;

    internal static int GetEnergyBand(float energy) => MapRoomCameraMetadata.GetComponentBand(energy, MapRoomCameraMetadata.MAX_ENERGY);

    internal static int GetHealthBand(float health) => MapRoomCameraMetadata.GetComponentBand(health, MapRoomCameraMetadata.MAX_HEALTH);

    private MapRoomCameraMetadata KeepCameraStateOnReplacementInGate(Entity replacement)
    {
        MapRoomCameraMetadata state = entityRegistry.TryGetEntityById(replacement.Id, out Entity? current) && IsCamera(current) ? ReadState(current) : new MapRoomCameraMetadata();
        // The entity that takes a camera's place is one in an inventory or one out in the world, never one in a dock.
        state = EnsureCameraNumber(state).WithDockingIndex(MapRoomCameraMetadata.NOT_DOCKED);
        SetState(replacement, state);
        knownCameraIds.TryAdd(replacement.Id, 0);
        return state;
    }

    /// <summary>
    ///     Gives a camera that has none the next free number.
    /// </summary>
    private MapRoomCameraMetadata EnsureCameraNumber(MapRoomCameraMetadata state) =>
        state.CameraNumber > 0 ? state : state.WithCameraNumber(++lastCameraNumber);

    /// <summary>
    ///     Stores a new light state. The revision only advances when the light actually changes.
    /// </summary>
    private static MapRoomCameraMetadata SetLight(Entity camera, bool lightOn, out bool changed)
    {
        MapRoomCameraMetadata current = ReadState(camera);
        changed = current.LightOn != lightOn;
        if (changed)
        {
            current = current.WithLight(lightOn, current.LightRevision + 1);
            SetState(camera, current);
        }
        return current;
    }

    /// <summary>
    ///     Stores new battery and health values. The revision only advances when a value actually changes.
    /// </summary>
    private static MapRoomCameraMetadata SetComponents(Entity camera, float energy, float health)
    {
        MapRoomCameraMetadata current = ReadState(camera);
        if (current.Energy == energy && current.Health == health)
        {
            return current;
        }
        current = current.WithComponents(energy, health, current.ComponentRevision + 1);
        SetState(camera, current);
        return current;
    }

    private static MapRoomCameraMetadata ReadState(Entity camera) => camera.Metadata as MapRoomCameraMetadata ?? new MapRoomCameraMetadata();

    private static void SetState(Entity camera, MapRoomCameraMetadata state) => camera.Metadata = state;
}
