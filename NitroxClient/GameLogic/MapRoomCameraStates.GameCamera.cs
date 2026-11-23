using System;
using System.Collections;
using System.Collections.Generic;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;
using NitroxClient.Communication.Abstract;
using NitroxClient.GameLogic.Helper;
using UnityEngine;

namespace NitroxClient.GameLogic;

/// <summary>
///     The part that reads and writes the game's cameras.
/// </summary>
public sealed partial class MapRoomCameraStates
{
    private const float CAMERA_BATTERY_WAIT_SECONDS = 10f;

    private readonly LiveMixinManager liveMixinManager;
    private readonly Dictionary<NitroxId, float> pendingCameraEnergy = [];
    private readonly Dictionary<NitroxId, CameraBatteryInitialization> initializingCameraBatteries = [];
    private long nextCameraBatteryInitializationGeneration;

    public MapRoomCameraStates(IPacketSender packetSender, SimulationOwnership simulationOwnership, LiveMixinManager liveMixinManager,
        BasePowerConsumptionForwarder powerConsumptionForwarder, ScannerRoomClientDiagnostics diagnostics)
        : this(packetSender, simulationOwnership, diagnostics)
    {
        this.liveMixinManager = liveMixinManager;
        this.powerConsumptionForwarder = powerConsumptionForwarder;
    }

    /// <inheritdoc cref="Apply{TCamera}(TCamera, MapRoomCameraMetadata)" />
    public void Apply(MapRoomCamera camera, NitroxId cameraId, MapRoomCameraMetadata state) => Apply(new GameCamera(this, camera, cameraId), state);

    /// <inheritdoc cref="ReportIfChanged{TCamera}(TCamera)" />
    public void ReportIfChanged(MapRoomCamera camera)
    {
        if (camera && camera.TryGetNitroxId(out NitroxId cameraId))
        {
            ReportIfChanged(new GameCamera(this, camera, cameraId));
        }
    }

    /// <inheritdoc cref="ChangesHealthHere(NitroxId, bool)" />
    public bool ChangesHealthHere(MapRoomCamera camera) =>
        // A camera that is no entity is nobody's to decide.
        !camera.TryGetNitroxId(out NitroxId cameraId) || ChangesHealthHere(cameraId, liveMixinManager.IsRemoteHealthChanging);

    /// <summary>
    ///     What the game shows for a camera, for the entity a client makes of it. The server keeps its own state for
    ///     a camera it knows and takes nothing from here.
    /// </summary>
    /// <param name="dockingIndex">The dock the camera sits in, which <see cref="MapRoomCameraDocks" /> knows.</param>
    public static MapRoomCameraMetadata Extract(MapRoomCamera camera, int dockingIndex) =>
        new(IsLightOn(camera), camera.energyMixin.charge, camera.liveMixin.health, camera.cameraNumber, dockingIndex);

    /// <summary>
    ///     Gives a camera the number the server assigned to it. Without one, the game numbers a camera by its place in
    ///     this client's own list of cameras.
    /// </summary>
    /// <returns>True if the camera had another number before.</returns>
    public static bool SetCameraNumber(MapRoomCamera camera, int cameraNumber)
    {
        if (camera.cameraNumber == cameraNumber)
        {
            return false;
        }
        camera.cameraNumber = cameraNumber;
        camera.UpdatePingLabel();
        return true;
    }

    internal static void SetLight(MapRoomCamera camera, bool on)
    {
        if (!camera)
        {
            return;
        }
        // MapRoomCamera.Start restores lightsParent from this serialized field, so it has to be right even when the
        // state arrives between the creation of the object and its Start.
        camera.lightState = on;
        if (camera.lightsParent && camera.lightsParent.activeSelf != on)
        {
            camera.lightsParent.SetActive(on);
        }
    }

    private static bool IsLightOn(MapRoomCamera camera) => camera.lightsParent ? camera.lightsParent.activeSelf : camera.lightState;

    /// <summary>
    ///     A camera's battery is no entity of its own, and a camera that has just spawned has none yet. The energy is
    ///     kept until the battery is there.
    /// </summary>
    private void SetEnergy(MapRoomCamera camera, NitroxId cameraId, float energy)
    {
        if (camera.energyMixin.battery != null)
        {
            camera.energyMixin.battery.charge = energy;
            pendingCameraEnergy.Remove(cameraId);
            // A replacement camera can become ready while an older instance still owns the wait.
            // Supersede that generation; its coroutine will observe that it is stale and exit.
            initializingCameraBatteries.Remove(cameraId);
            return;
        }
        pendingCameraEnergy[cameraId] = energy;
        if (!initializingCameraBatteries.TryGetValue(cameraId, out CameraBatteryInitialization initialization) || !ReferenceEquals(initialization.Camera, camera))
        {
            long generation = ++nextCameraBatteryInitializationGeneration;
            initializingCameraBatteries[cameraId] = new CameraBatteryInitialization(camera, generation);
            UWE.CoroutineHost.StartCoroutine(InitializeCameraBattery(camera, cameraId, generation));
        }
    }

    private IEnumerator InitializeCameraBattery(MapRoomCamera camera, NitroxId cameraId, long generation)
    {
        bool canonicalEnergyApplied = false;
        bool populationFailed = false;
        Exception populationException = null;
        try
        {
            if (camera && camera.energyMixin.battery == null)
            {
                try
                {
                    BatteryChildEntityHelper.PopulateInstalledBattery(camera.energyMixin, [], cameraId);
                }
                catch (Exception ex)
                {
                    populationFailed = true;
                    populationException = ex;
                }

                if (!populationFailed)
                {
                    float timeoutAt = Time.time + CAMERA_BATTERY_WAIT_SECONDS;
                    yield return new WaitUntil(() => !camera || camera.energyMixin.battery != null ||
                        !IsCurrentCameraBatteryInitialization(cameraId, camera, generation) || Time.time >= timeoutAt);
                }
            }

            if (populationFailed && IsCurrentCameraBatteryInitialization(cameraId, camera, generation))
            {
                Log.Error(populationException, $"[MapRoomCameraStates] Failed to request a restored battery for camera {cameraId}; authoritative component broadcasts remain suppressed");
                diagnostics.Record("component_apply", "pending", cameraId, reason: "battery_spawn_exception");
            }
            else if (camera && camera.energyMixin.battery == null &&
                IsCurrentCameraBatteryInitialization(cameraId, camera, generation))
            {
                Log.Warn($"[MapRoomCameraStates] Restored camera {cameraId} battery is still unavailable after {CAMERA_BATTERY_WAIT_SECONDS} seconds; authoritative component broadcasts remain suppressed");
                diagnostics.Record("component_apply", "pending", cameraId, reason: "battery_wait_timeout");
            }

            if (camera && camera.energyMixin.battery == null &&
                IsCurrentCameraBatteryInitialization(cameraId, camera, generation))
            {
                yield return new WaitUntil(() => !camera || camera.energyMixin.battery != null ||
                    !IsCurrentCameraBatteryInitialization(cameraId, camera, generation));
            }

            if (IsCurrentCameraBatteryInitialization(cameraId, camera, generation) && camera &&
                camera.energyMixin.battery != null && pendingCameraEnergy.TryGetValue(cameraId, out float energy))
            {
                camera.energyMixin.battery.charge = energy;
                canonicalEnergyApplied = true;
                Log.Info($"[MapRoomCameraStates] Initialized restored camera {cameraId} battery with {energy:F2} energy");
            }
        }
        finally
        {
            bool cameraAlive = camera;
            bool batteryAvailable = cameraAlive && camera.energyMixin.battery != null;
            bool currentGeneration = IsCurrentCameraBatteryInitialization(cameraId, camera, generation);
            if (ShouldClearCameraBatteryInitialization(currentGeneration, cameraAlive, batteryAvailable, canonicalEnergyApplied))
            {
                pendingCameraEnergy.Remove(cameraId);
                initializingCameraBatteries.Remove(cameraId);
            }
        }
    }

    private bool IsCurrentCameraBatteryInitialization(NitroxId cameraId, MapRoomCamera camera, long generation) =>
        initializingCameraBatteries.TryGetValue(cameraId, out CameraBatteryInitialization initialization) &&
        initialization.Generation == generation && ReferenceEquals(initialization.Camera, camera);

    internal static bool ShouldClearCameraBatteryInitialization(bool currentGeneration, bool cameraAlive,
        bool batteryAvailable, bool canonicalEnergyApplied) =>
        currentGeneration && (!cameraAlive || batteryAvailable && canonicalEnergyApplied);

    private readonly record struct CameraBatteryInitialization(MapRoomCamera Camera, long Generation);

    /// <summary>
    ///     A struct, because one is made for every camera every frame.
    /// </summary>
    private readonly struct GameCamera(MapRoomCameraStates states, MapRoomCamera camera, NitroxId cameraId) : ICamera
    {
        public NitroxId Id => cameraId;
        public object Body => camera;

        public bool LightOn
        {
            get => IsLightOn(camera);
            set => SetLight(camera, value);
        }

        public float Energy => camera.energyMixin.charge;
        public float Health => camera.liveMixin.health;
        public bool CanReport => !states.initializingCameraBatteries.ContainsKey(cameraId);

        public void ApplyComponents(float energy, float health)
        {
            states.SetEnergy(camera, cameraId, energy);
            states.liveMixinManager.SyncRemoteHealth(camera.liveMixin, health);
        }
    }
}
