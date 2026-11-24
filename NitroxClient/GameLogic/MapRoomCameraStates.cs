using System;
using System.Collections.Generic;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;
using Nitrox.Model.Subnautica.Packets;
using NitroxClient.Communication.Abstract;

namespace NitroxClient.GameLogic;

/// <summary>
///     Light, battery and health of Scanner Room cameras. The server holds them as the metadata of the camera's entity
///     and decides who may change what: the light belongs to the player who holds the camera exclusively (who flies
///     it), battery and health to the player who simulates it. That player's game is where they change, which
///     includes the recharging of a docked camera, and this class reports the change; everybody else is told by the
///     server, and this class applies it.
/// </summary>
/// <remarks>
///     What this client decides is never taken back from the server. Its own report comes back in the camera's state
///     whenever the server sends that state for another reason, and it is older than what the game holds by then: put
///     into the game, it would undo the recharging, the drain or the damage of the time in between.
/// </remarks>
public sealed partial class MapRoomCameraStates
{
    // How far battery and health have to move before the server is told. Guesses, not measured. The battery of a
    // camera that is flown or recharged changes every frame, and is reported in steps of half a unit of charge.
    // Health only changes by damage and repair, so its threshold just keeps float noise from being sent.
    private const float ENERGY_REPORT_THRESHOLD = 0.5f;
    private const float HEALTH_REPORT_THRESHOLD = 0.05f;

    private readonly IPacketSender packetSender;
    private readonly SimulationOwnership simulationOwnership;
    private readonly ScannerRoomClientDiagnostics diagnostics;

    /// <summary>
    ///     For each camera, what the server and this client last told each other, and the object of the game it was
    ///     about. A camera that is picked up or dropped gets a new object, which starts with what its prefab holds.
    /// </summary>
    private readonly Dictionary<NitroxId, Known> knownByCameraId = [];

    internal MapRoomCameraStates(IPacketSender packetSender, SimulationOwnership simulationOwnership, ScannerRoomClientDiagnostics diagnostics)
    {
        this.packetSender = packetSender;
        this.simulationOwnership = simulationOwnership;
        this.diagnostics = diagnostics;
    }

    /// <summary>
    ///     Takes over what the server holds for a camera: when the camera spawns, when another player's change is
    ///     passed on, and when the server answers something it refused from this client.
    /// </summary>
    internal void Apply<TCamera>(TCamera camera, MapRoomCameraMetadata state) where TCamera : ICamera
    {
        bool known = knownByCameraId.TryGetValue(camera.Id, out Known previous) && ReferenceEquals(previous.Body, camera.Body);
        // A new object knows nothing yet, whoever simulates it.
        bool takeComponents = !known || !simulationOwnership.HasAnyLockType(camera.Id);
        bool takeLight = !known || !simulationOwnership.HasExclusiveLock(camera.Id);
        if (takeComponents)
        {
            camera.ApplyComponents(state.Energy, state.Health);
            diagnostics.RecordComponentApplied(camera.Id, state.Energy, state.Health, true);
        }
        if (takeLight)
        {
            if (!known || previous.LightOn != state.LightOn)
            {
                diagnostics.Record("light_apply", "ok", camera.Id, reason: state.LightOn ? "on" : "off");
            }
            camera.LightOn = state.LightOn;
        }
        knownByCameraId[camera.Id] = new Known(camera.Body, takeLight ? state.LightOn : previous.LightOn, takeComponents ? state.Energy : previous.Energy,
            takeComponents ? state.Health : previous.Health, state.CameraNumber, state.DockingIndex);
    }

    /// <summary>
    ///     Tells the server what the game changed about a camera, if this client is the one who decides it. Called
    ///     whenever the game may have changed something, which is every frame for a camera that is recharged.
    /// </summary>
    internal void ReportIfChanged<TCamera>(TCamera camera) where TCamera : ICamera
    {
        if (!simulationOwnership.HasAnyLockType(camera.Id))
        {
            return;
        }
        // Until the server's state has been put into this object it shows what its prefab came with, and while its
        // battery is being put in it shows none.
        if (!knownByCameraId.TryGetValue(camera.Id, out Known known) || !ReferenceEquals(known.Body, camera.Body) || !camera.CanReport)
        {
            return;
        }
        // The light is sent as the server has it unless this client flies the camera: a report of battery and health
        // must not look like an attempt to switch the light.
        bool lightOn = simulationOwnership.HasExclusiveLock(camera.Id) ? camera.LightOn : known.LightOn;
        float energy = camera.Energy;
        float health = camera.Health;
        if (lightOn == known.LightOn && !HasChangedEnough(known.Energy, energy, camera.EnergyCapacity, ENERGY_REPORT_THRESHOLD) &&
            !HasChangedEnough(known.Health, health, camera.MaxHealth, HEALTH_REPORT_THRESHOLD))
        {
            return;
        }
        knownByCameraId[camera.Id] = known with { LightOn = lightOn, Energy = energy, Health = health };
        // The number and the dock are the server's; it ignores what is sent for them.
        packetSender.Send(new EntityMetadataUpdate(camera.Id, new MapRoomCameraMetadata(lightOn, energy, health, known.CameraNumber, known.DockingIndex)));
    }

    /// <summary>
    ///     Whether a battery or health value moved far enough from what the server was last told to be told again.
    ///     Reaching empty or full is always told once: the step that gets there is mostly smaller than the threshold
    ///     (a docked camera back from a short flight is refilled by 0.13), and without it the server would keep the
    ///     value just short of the end for good, also across a restart.
    /// </summary>
    /// <param name="known">What the server was last told, or told this client.</param>
    /// <param name="current">What the game holds now.</param>
    /// <param name="maximum">The battery's capacity or the camera's maximum health.</param>
    internal static bool HasChangedEnough(float known, float current, float maximum, float threshold) =>
        Math.Abs(known - current) >= threshold || current != known && (current <= 0f || current >= maximum);

    /// <summary>
    ///     Whether the game may damage or repair a camera here. A camera's health is the simulating player's to decide,
    ///     like a vehicle's: in anybody else's game the change would show there alone, until the server's state undoes
    ///     it. A repair by another player is passed on to the simulating player instead (see Welder_Weld_Patch).
    /// </summary>
    /// <param name="applyingServerState">Whether the change is the server's state being put into the camera.</param>
    internal bool ChangesHealthHere(NitroxId cameraId, bool applyingServerState) => applyingServerState || simulationOwnership.HasAnyLockType(cameraId);

    /// <summary>
    ///     Whether a camera that another player's game removed was destroyed by damage, from the health that player
    ///     reported last. Its game reports the health of zero before it says that the camera is gone (see
    ///     LiveMixin_Kill_Patch); a camera that is taken away for any other reason, such as with its room or in a
    ///     trash can, still has health.
    /// </summary>
    /// <param name="reportedHealth">The health as the simulating player last reported it.</param>
    internal static bool WasKilled(float reportedHealth) =>
        // Zero is always reported (see HasChangedEnough); the margin covers a report of an older build.
        reportedHealth < HEALTH_REPORT_THRESHOLD;

    /// <summary>
    ///     A camera of the game, as far as its light, battery and health go. Tests put a camera without the game here.
    /// </summary>
    internal interface ICamera
    {
        NitroxId Id { get; }

        /// <summary>
        ///     The object of the game. It stays the same for as long as the camera is not picked up or dropped.
        /// </summary>
        object Body { get; }

        bool LightOn { get; set; }
        float Energy { get; }
        float Health { get; }

        /// <summary>
        ///     The energy of a full battery.
        /// </summary>
        float EnergyCapacity { get; }

        float MaxHealth { get; }

        /// <summary>
        ///     False while the camera's battery is being put in, when the game shows no energy at all.
        /// </summary>
        bool CanReport { get; }

        void ApplyComponents(float energy, float health);
    }

    private readonly record struct Known(object Body, bool LightOn, float Energy, float Health, int CameraNumber, int DockingIndex);
}
