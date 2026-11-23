using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;
using Nitrox.Server.Subnautica.Models.GameLogic.Entities;

namespace Nitrox.Server.Subnautica.Models.GameLogic;

/// <summary>
///     Light, battery, health and number of every camera. A camera keeps them on its own entity as
///     <see cref="MapRoomCameraMetadata" />, whether it is docked, loose in the world or in an inventory, so they are
///     saved and sent to joining players with it. Players change them with an entity metadata update like the state of
///     any other entity; <see cref="TryAcceptCameraMetadata" /> decides which parts of an update count.
/// </summary>
internal sealed partial class ScannerRoomManager
{
    /// <summary>
    ///     The highest camera number handed out so far. Numbers are unique in the world and never reused while the
    ///     server runs: the game has one list of all cameras, and every screen cycles through all of them.
    /// </summary>
    private int lastCameraNumber;

    /// <summary>
    ///     The gate for a camera's state sent as entity metadata. A client sends everything it has for the camera, and
    ///     only part of that is its to decide: the light belongs to the player who holds the camera exclusively (who
    ///     flies it), battery and health to the player who simulates it, the number and the dock to the server. What
    ///     the sender may set is stored and goes to the other players. If it sent something else that it may not set,
    ///     it shows a state nobody else has, and is sent what the server holds.
    /// </summary>
    public MetadataUpdateResult TryAcceptCameraMetadata(Player sender, Entity target, MapRoomCameraMetadata requested)
    {
        using Hold held = Enter();
        CameraView camera = GetCamera(target.Id);
        if (camera.WorldCamera is not { } worldCamera)
        {
            // In an inventory nothing happens to a camera, and the id may be of something that is no camera at all.
            diagnostics.RecordRejected("component", cameraId: target.Id, sessionId: sender.SessionId, reason: "invalid_assoc");
            return camera.Exists ? MetadataUpdateResult.RejectAndCorrect(camera.State) : MetadataUpdateResult.Reject;
        }

        MapRoomCameraMetadata stored = camera.State;
        // Who the sender is to the camera is read, and what it may set is stored, while the lock cannot change hands:
        // two players' updates can then never end up as a state that is partly the one's and partly the other's.
        (bool simulates, MapRoomCameraMetadata accepted) = simulationOwnershipData.ExecuteForOwner(sender, [worldCamera.Id], ownedIds =>
        {
            bool isSimulatingPlayer = ownedIds.Contains(worldCamera.Id);
            bool holdsExclusively = isSimulatingPlayer && simulationOwnershipData.TryGetLock(worldCamera.Id, out SimulationOwnershipData.PlayerLock cameraLock) &&
                                    cameraLock.LockType == SimulationLockType.EXCLUSIVE;
            MapRoomCameraMetadata merged = Merge(stored, requested, holdsExclusively, isSimulatingPlayer && IsValidComponentState(requested.Energy, requested.Health));
            SetState(worldCamera, merged);
            return (isSimulatingPlayer, merged);
        });

        bool lightRefused = requested.LightOn != accepted.LightOn;
        bool componentsRefused = requested.Energy != accepted.Energy || requested.Health != accepted.Health;
        if (lightRefused)
        {
            diagnostics.RecordRejected("light", camera.DockedRoom, worldCamera.Id, sender.SessionId, reason: simulates ? "control_required" : "non_owner");
        }
        if (componentsRefused)
        {
            diagnostics.RecordRejected("component", camera.DockedRoom, worldCamera.Id, sender.SessionId, reason: simulates ? "invalid_value" : "non_owner");
        }
        RecordStateChange(camera, stored, accepted, sender);

        bool changed = !ReferenceEquals(accepted, stored);
        if (lightRefused || componentsRefused)
        {
            return changed ? MetadataUpdateResult.AcceptAndEcho(accepted) : MetadataUpdateResult.RejectAndCorrect(stored);
        }
        return changed ? MetadataUpdateResult.Accept(accepted) : MetadataUpdateResult.Reject;
    }

    /// <summary>
    ///     Records an update for a camera the server does not have. Its simulating player can report its battery once
    ///     more while the camera's removal is on its way to that player, which is no error.
    /// </summary>
    public void RejectMetadataForUnknownCamera(Player sender, NitroxId cameraId) =>
        diagnostics.RecordRejected("component", cameraId: cameraId, sessionId: sender.SessionId, reason: "removed");

    /// <summary>
    ///     Called before a camera's entity is replaced by another one with the same id (picked up into an inventory,
    ///     dropped back into the world), and before a camera a client made is stored for the first time. The entity is
    ///     built by a client; whatever camera state it carries is dropped and the state the server holds moves over to
    ///     it. A camera that is new to the server gets its number.
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

    /// <summary>
    ///     Which parts of a client's update count, like <see cref="CyclopsMetadataAuthority.Merge" /> does for a
    ///     Cyclops. The number and the dock are never the client's to set.
    /// </summary>
    /// <returns>The state to store, which is <paramref name="stored" /> itself if nothing changes.</returns>
    internal static MapRoomCameraMetadata Merge(MapRoomCameraMetadata stored, MapRoomCameraMetadata requested, bool senderMaySetLight, bool senderMaySetComponents)
    {
        bool lightOn = senderMaySetLight ? requested.LightOn : stored.LightOn;
        float energy = senderMaySetComponents ? requested.Energy : stored.Energy;
        float health = senderMaySetComponents ? requested.Health : stored.Health;
        if (lightOn == stored.LightOn && energy == stored.Energy && health == stored.Health)
        {
            return stored;
        }
        return new MapRoomCameraMetadata(lightOn, energy, health, stored.CameraNumber, stored.DockingIndex);
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
    ///     Writes the lines for what an accepted update changed: the light, and battery and health when they cross
    ///     into another band. The battery of a camera that is flown drains all the time, which is not worth a line.
    /// </summary>
    private void RecordStateChange(CameraView camera, MapRoomCameraMetadata previous, MapRoomCameraMetadata stored, Player sender)
    {
        NitroxId cameraId = camera.Entity!.Id;
        if (previous.LightOn != stored.LightOn)
        {
            diagnostics.RecordAccepted("light", camera.DockedRoom, cameraId, sender.SessionId,
                reason: camera.DockedRoom == null ? stored.LightOn ? "on_loose" : "off_loose" : stored.LightOn ? "on" : "off");
        }
        if (camera.DockedRoom != null && GetEnergyBand(previous.Energy) != GetEnergyBand(stored.Energy))
        {
            diagnostics.RecordAccepted("component_energy", camera.DockedRoom, cameraId, sender.SessionId, reason: $"band_{GetEnergyBand(stored.Energy)}");
        }
        if (GetHealthBand(previous.Health) != GetHealthBand(stored.Health))
        {
            string reason = previous.Health > 0f && stored.Health <= 0f ? "death" : previous.Health <= 0f && stored.Health > 0f ? "repair" : $"band_{GetHealthBand(stored.Health)}";
            diagnostics.RecordAccepted("component_health", camera.DockedRoom, cameraId, sender.SessionId, reason: reason);
        }
    }

    /// <summary>
    ///     Gives a camera that has none the next free number.
    /// </summary>
    private MapRoomCameraMetadata EnsureCameraNumber(MapRoomCameraMetadata state) =>
        state.CameraNumber > 0 ? state : state.WithCameraNumber(++lastCameraNumber);

    private static MapRoomCameraMetadata ReadState(Entity camera) => camera.Metadata as MapRoomCameraMetadata ?? new MapRoomCameraMetadata();

    private static void SetState(Entity camera, MapRoomCameraMetadata state) => camera.Metadata = state;
}
