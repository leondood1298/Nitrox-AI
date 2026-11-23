using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using FMOD.Studio;
using Nitrox.Model.Core;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Bases;
using Nitrox.Model.Subnautica.Helper;
using Nitrox.Model.Subnautica.Packets;
using NitroxClient.Communication;
using NitroxClient.Communication.Abstract;
using NitroxClient.GameLogic.Helper;
using NitroxClient.MonoBehaviours;
using UnityEngine;

namespace NitroxClient.GameLogic;

public class MapRoomCameras
{
    private const float ENERGY_BROADCAST_THRESHOLD = 0.5f;
    private const float HEALTH_BROADCAST_THRESHOLD = 0.05f;
    private const float DOCK_CHARGE_FRACTION_PER_UPDATE = 0.1f;
    private const float CAMERA_BATTERY_WAIT_SECONDS = 10f;
    private const int PREVIEW_JPEG_QUALITY = 55;

    private readonly IPacketSender packetSender;
    private readonly IMultiplayerSession multiplayerSession;
    private readonly LiveMixinManager liveMixinManager;
    private readonly SimulationOwnership simulationOwnership;
    private readonly ScannerRoomClientDiagnostics diagnostics;
    private readonly StalkerCameraLockPurposeTracker stalkerCameraLockPurposeTracker;

    private readonly Dictionary<NitroxId, bool> lastBroadcastLightState = new();

    private readonly HashSet<NitroxId> locallyControlled = new();
    private readonly HashSet<NitroxId> pendingControl = new();
    private readonly HashSet<NitroxId> remotelyControlled = new();
    private readonly Dictionary<NitroxId, long> lightRevisions = new();
    private readonly Dictionary<NitroxId, (float Energy, float Health)> lastComponents = new();
    private readonly Dictionary<NitroxId, long> componentRevisions = new();
    private readonly Dictionary<NitroxId, float> pendingCameraEnergy = new();
    private readonly Dictionary<NitroxId, CameraBatteryInitialization> initializingCameraBatteries = new();

    /// <summary>
    ///     What the server sent for cameras that had not spawned on this client yet: who controls them, and the latest
    ///     light and battery and health that arrived after the camera's own entity was sent. Applied when the camera
    ///     spawns.
    /// </summary>
    private readonly Dictionary<NitroxId, MapRoomCameraControl> pendingControls = new();

    private readonly Dictionary<NitroxId, MapRoomCameraLight> pendingLights = new();
    private readonly Dictionary<NitroxId, MapRoomCameraComponentState> pendingComponents = new();
    private long nextCameraBatteryInitializationGeneration;
    private long latestPreviewRevision;
    private MapRoomCameraPreview pendingPreview;
    private bool pendingPreviewPixelsApplied;
    private long lastPreviewApplyDiagnosticRevision;
    private string lastPreviewApplyDiagnosticOutcome;

    public MapRoomCameras(IPacketSender packetSender, IMultiplayerSession multiplayerSession, LiveMixinManager liveMixinManager,
        SimulationOwnership simulationOwnership, ScannerRoomClientDiagnostics diagnostics,
        StalkerCameraLockPurposeTracker stalkerCameraLockPurposeTracker)
    {
        this.packetSender = packetSender;
        this.multiplayerSession = multiplayerSession;
        this.liveMixinManager = liveMixinManager;
        this.simulationOwnership = simulationOwnership;
        this.diagnostics = diagnostics;
        this.stalkerCameraLockPurposeTracker = stalkerCameraLockPurposeTracker;
    }

    public bool CanBeginControl(MapRoomCamera camera) => camera && HasReadyDockAssociation(camera);

    /// <summary>
    ///     A camera in a dock is asked for by naming the dock, which takes the id of the dock's room.
    /// </summary>
    private static bool HasReadyDockAssociation(MapRoomCamera camera)
    {
        if (!camera || !camera.dockingPoint)
        {
            return true;
        }
        return MapRoomCameraDocks.TryGetPlace(camera.dockingPoint, out MapRoomFunctionality mapRoom, out _) && mapRoom.TryGetNitroxId(out NitroxId _);
    }

    public void BroadcastControl(MapRoomCamera camera, bool isControlling)
    {
        if (!camera)
        {
            return;
        }
        if (isControlling)
        {
            if (!CanBeginControl(camera))
            {
                AbortLocalControl(camera);
                return;
            }
            Optional<NitroxId> mapRoomId = Optional.Empty;
            int cameraIndex = -1;
            if (camera.dockingPoint)
            {
                if (!MapRoomCameraDocks.TryGetPlace(camera.dockingPoint, out MapRoomFunctionality mapRoom, out cameraIndex) || !mapRoom.TryGetNitroxId(out NitroxId roomId))
                {
                    camera.TryGetNitroxId(out NitroxId unresolvedCameraId);
                    diagnostics.Record("control_publish", "pending", unresolvedCameraId, reason: "fresh_room_identity_wait");
                    AbortLocalControl(camera);
                    return;
                }
                mapRoomId = Optional.Of(roomId);
            }
            NitroxId cameraId = NitroxEntity.GetIdOrGenerateNew(camera.gameObject);
            bool lightOn = camera.lightsParent && camera.lightsParent.activeSelf;
            lastBroadcastLightState[cameraId] = lightOn;
            pendingControl.Add(cameraId);
            packetSender.Send(new MapRoomCameraControl(cameraId, mapRoomId, cameraIndex, isControlling: true, lightOn));
            diagnostics.Record("control_publish", "ok", cameraId, mapRoomId.HasValue ? mapRoomId.Value : null, slot: cameraIndex, reason: "acquire_requested");
        }
        else if (camera.TryGetNitroxId(out NitroxId cameraId))
        {
            pendingControl.Remove(cameraId);
            locallyControlled.Remove(cameraId);
            lastBroadcastLightState.Remove(cameraId);
            // Release the camera-specific lock before generic movement ownership is relinquished.
            // Otherwise the server can see an already-unlocked camera and fail to notify remote clients.
            packetSender.Send(new MapRoomCameraControl(cameraId, Optional.Empty, -1, isControlling: false, lightOn: false));
            diagnostics.Record("control_publish", "ok", cameraId, reason: "release_requested");
            MovementBroadcaster.UnregisterWatched(cameraId);
        }
    }

    private static void AbortLocalControl(MapRoomCamera camera)
    {
        if (!camera)
        {
            return;
        }
        camera.enabled = true;
        ExitCameraModeLocally(camera);
    }

    /// <summary>
    ///     Leaves camera mode without telling the server. The game always pairs FreeCamera (stops the drone's sounds and
    ///     screen effect) with ExitLockedMode (returns the player and resets the view); doing only the second leaves the
    ///     connecting/idle loops playing and the screen effect on.
    /// </summary>
    private static void ExitCameraModeLocally(MapRoomCamera camera)
    {
        using (PacketSuppressor<MapRoomCameraControl>.Suppress())
        {
            camera.FreeCamera(resetPlayerPosition: true);
            camera.ExitLockedMode(resetPlayerPosition: true);
        }
    }

    public void BroadcastPreview(MapRoomCamera camera)
    {
        if (!camera || !camera.TryGetNitroxId(out NitroxId cameraId) || !camera.mapRoomRenderTexture)
        {
            return;
        }

        try
        {
            byte[] jpegBytes = CapturePreviewJpeg(camera.mapRoomRenderTexture);
            if (MapRoomCameraPreviewImage.TryValidate(jpegBytes, out int width, out int height))
            {
                packetSender.Send(new MapRoomCameraPreview(cameraId, camera.GetCameraNumber(), jpegBytes));
                diagnostics.Record("preview_publish", "ok", cameraId, slot: camera.GetCameraNumber(),
                    reason: $"bytes_{jpegBytes.Length}_{width}x{height}");
            }
            else
            {
                diagnostics.Record("preview_publish", "reject", cameraId, slot: camera.GetCameraNumber(),
                    reason: $"invalid_jpeg_bytes_{jpegBytes?.Length ?? 0}");
                Log.Warn($"[MapRoomCameras] Dropped invalid or oversized local camera preview for {cameraId}");
            }
        }
        catch (Exception ex)
        {
            // Preview publication is cosmetic and must never strand the player in camera control mode.
            diagnostics.Record("preview_publish", "reject", cameraId, slot: camera.GetCameraNumber(),
                reason: "capture_exception");
            Log.Error(ex, $"[MapRoomCameras] Failed to capture camera preview for {cameraId}");
        }
    }

    public void ProcessControl(MapRoomCameraControl packet)
    {
        if (!packet.IsServerResponse)
        {
            return;
        }
        if (!packet.Granted || !packet.IsControlling)
        {
            pendingControls.Remove(packet.CameraId);
        }
        bool isLocalController = packet.ControllerSessionId == multiplayerSession.Reservation.SessionId;
        if (!packet.Granted)
        {
            if (packet.IsControlling &&
                stalkerCameraLockPurposeTracker.CompleteCameraControlRequest(packet.CameraId, false,
                    simulationOwnership.HasExclusiveLock(packet.CameraId)))
            {
                simulationOwnership.RequestSimulationLock(packet.CameraId, SimulationLockType.TRANSIENT);
            }
            diagnostics.Record("control_apply", "reject", packet.CameraId, packet.MapRoomId.HasValue ? packet.MapRoomId.Value : null,
                slot: packet.CameraIndex, reason: isLocalController ? "denied" : $"locked_by_{packet.ControllerSessionId}");
            if (!isLocalController)
            {
                remotelyControlled.Add(packet.CameraId);
            }
            if (pendingControl.Remove(packet.CameraId) && NitroxEntity.TryGetObjectFrom(packet.CameraId, out GameObject deniedObject) && deniedObject.TryGetComponent(out MapRoomCamera deniedCamera))
            {
                deniedCamera.enabled = true;
                ExitCameraModeLocally(deniedCamera);
            }
            return;
        }
        if (packet.IsControlling)
        {
            if (isLocalController)
            {
                stalkerCameraLockPurposeTracker.CompleteCameraControlRequest(packet.CameraId, true,
                    simulationOwnership.HasExclusiveLock(packet.CameraId));
            }
            else
            {
                stalkerCameraLockPurposeTracker.Forget(packet.CameraId);
            }
            if (!NitroxEntity.TryGetObjectFrom(packet.CameraId, out GameObject cameraObject) || !cameraObject.GetComponent<MapRoomCamera>())
            {
                // The camera has not spawned on this client yet, as for a player who just joined. Applied when it has.
                pendingControls[packet.CameraId] = packet;
                diagnostics.Record("control_apply", "pending", packet.CameraId, packet.MapRoomId.HasValue ? packet.MapRoomId.Value : null,
                    slot: packet.CameraIndex, reason: "waiting_object");
                return;
            }
            pendingControls.Remove(packet.CameraId);
            SetLight(cameraObject, packet.LightOn);
            if (isLocalController)
            {
                remotelyControlled.Remove(packet.CameraId);
                pendingControl.Remove(packet.CameraId);
                locallyControlled.Add(packet.CameraId);
                if (cameraObject.TryGetComponent(out MapRoomCamera localCamera))
                {
                    localCamera.enabled = true;
                    if (uGUI_CameraDrone.main && uGUI_CameraDrone.main.GetCamera() == localCamera)
                    {
                        uGUI_CameraDrone.main.noSignal.SetActive(false);
                    }
                }
                MovementBroadcaster.RegisterWatched(cameraObject, packet.CameraId);
                diagnostics.Record("control_apply", "ok", packet.CameraId, packet.MapRoomId.HasValue ? packet.MapRoomId.Value : null,
                    slot: packet.CameraIndex, reason: "local");
                return;
            }
            if (!cameraObject.GetComponent<MapRoomCameraMovementReplicator>())
            {
                cameraObject.AddComponent<MapRoomCameraMovementReplicator>();
            }
            remotelyControlled.Add(packet.CameraId);
            diagnostics.Record("control_apply", "ok", packet.CameraId, packet.MapRoomId.HasValue ? packet.MapRoomId.Value : null,
                slot: packet.CameraIndex, reason: $"remote_pilot_{packet.ControllerSessionId}");
        }
        else if (NitroxEntity.TryGetObjectFrom(packet.CameraId, out GameObject cameraObject) && cameraObject.TryGetComponent(out MapRoomCameraMovementReplicator replicator))
        {
            UnityEngine.Object.Destroy(replicator);
            MapRoomCameraDocks.SettleInDock(cameraObject);
        }
        if (!packet.IsControlling)
        {
            remotelyControlled.Remove(packet.CameraId);
            if (isLocalController)
            {
                pendingControl.Remove(packet.CameraId);
                // A release this client did not ask for (it still counts the camera as granted) means the server revoked
                // control. A release that merely echoes a local exit finds the camera already removed here, which also
                // keeps a quick re-take of the same camera from being kicked out by the old echo.
                bool revokedByServer = locallyControlled.Remove(packet.CameraId);
                if (revokedByServer && NitroxEntity.TryGetObjectFrom(packet.CameraId, out GameObject revokedObject) &&
                    revokedObject.TryGetComponent(out MapRoomCamera revokedCamera) && revokedCamera.IsControlled())
                {
                    ExitCameraModeLocally(revokedCamera);
                }
                stalkerCameraLockPurposeTracker.Forget(packet.CameraId);
                MovementBroadcaster.UnregisterWatched(packet.CameraId);
            }
            diagnostics.Record("control_release_apply", "ok", packet.CameraId, packet.MapRoomId.HasValue ? packet.MapRoomId.Value : null,
                slot: packet.CameraIndex, reason: isLocalController ? "local" : $"remote_pilot_{packet.ControllerSessionId}");
        }
    }

    public void ProcessPreview(MapRoomCameraPreview packet)
    {
        if (!packet.IsServerResponse || !packet.Granted)
        {
            diagnostics.Record("preview_receive", "reject", packet.CameraId, revision: packet.Revision,
                slot: packet.CameraNumber, reason: packet.IsServerResponse ? "denied" : "not_server");
            return;
        }
        if (!MapRoomCameraPreviewImage.TryValidate(packet.JpegBytes, out int width, out int height))
        {
            diagnostics.Record("preview_receive", "reject", packet.CameraId, revision: packet.Revision,
                slot: packet.CameraNumber, reason: $"invalid_jpeg_bytes_{packet.JpegBytes?.Length ?? 0}");
            return;
        }
        if (!TryAdvancePreviewRevision(ref latestPreviewRevision, packet.Revision))
        {
            diagnostics.Record("preview_receive", "stale", packet.CameraId, revision: packet.Revision,
                slot: packet.CameraNumber, reason: $"latest_{latestPreviewRevision}");
            return;
        }

        diagnostics.Record("preview_receive", "ok", packet.CameraId, revision: packet.Revision,
            slot: packet.CameraNumber, reason: $"bytes_{packet.JpegBytes.Length}_{width}x{height}");
        pendingPreview = packet;
        pendingPreviewPixelsApplied = false;
        TryApplyPendingPreview();
    }

    private void TryApplyPendingPreview()
    {
        if (pendingPreview == null)
        {
            return;
        }

        MapRoomCamera camera = FindCamera(pendingPreview.CameraId);
        RenderTexture renderTexture = FindSharedPreviewTexture(camera);
        bool attemptedDecode = !pendingPreviewPixelsApplied && renderTexture;
        if (!pendingPreviewPixelsApplied && renderTexture)
        {
            pendingPreviewPixelsApplied = TryApplyPreviewPixels(renderTexture, pendingPreview.JpegBytes);
        }
        if (attemptedDecode && !pendingPreviewPixelsApplied)
        {
            RecordPreviewApply(pendingPreview, "reject", "decode_failed");
            pendingPreview = null;
            return;
        }
        ApplyGlobalPreviewSelection(camera, pendingPreview.CameraNumber);
        string outcome = !renderTexture || !pendingPreviewPixelsApplied || !camera ? "pending" : "ok";
        string reason = !renderTexture ? "missing_texture" : camera ? "global" : "camera_missing";
        RecordPreviewApply(pendingPreview, outcome, reason);
        if (pendingPreviewPixelsApplied && camera)
        {
            pendingPreview = null;
            pendingPreviewPixelsApplied = false;
        }
    }

    private void RecordPreviewApply(MapRoomCameraPreview packet, string outcome, string reason)
    {
        if (lastPreviewApplyDiagnosticRevision == packet.Revision && lastPreviewApplyDiagnosticOutcome == $"{outcome}{reason}")
        {
            return;
        }
        lastPreviewApplyDiagnosticRevision = packet.Revision;
        lastPreviewApplyDiagnosticOutcome = $"{outcome}{reason}";
        MapRoomCameraPreviewImage.TryValidate(packet.JpegBytes, out int width, out int height);
        diagnostics.Record("preview_apply", outcome, packet.CameraId, revision: packet.Revision,
            slot: packet.CameraNumber, reason: $"{reason}_bytes_{packet.JpegBytes.Length}_{width}x{height}");
    }

    internal static bool TryAdvancePreviewRevision(ref long currentRevision, long incomingRevision)
    {
        if (incomingRevision <= 0 || incomingRevision <= currentRevision)
        {
            return false;
        }
        currentRevision = incomingRevision;
        return true;
    }

    public bool CanSelectForControl(MapRoomCamera camera)
    {
        if (!camera)
        {
            return true;
        }
        if (!HasReadyDockAssociation(camera))
        {
            return false;
        }
        if (!camera.TryGetNitroxId(out NitroxId cameraId))
        {
            return true;
        }
        return CanSelectForControl(cameraId, camera.IsControlled());
    }

    internal bool CanSelectForControl(NitroxId cameraId, bool activelyControlled) =>
        CanSelectForControl(pendingControl.Contains(cameraId), locallyControlled.Contains(cameraId), remotelyControlled.Contains(cameraId), activelyControlled);

    internal static bool CanSelectForControl(bool pending, bool locallyControlled, bool remotelyControlled, bool activelyControlled) =>
        locallyControlled || (!remotelyControlled && (!pending || activelyControlled));

    public bool HasPendingControl(NitroxId cameraId) => pendingControl.Contains(cameraId);

    public bool HasLocalControl(NitroxId cameraId) => locallyControlled.Contains(cameraId);

    internal void ClearControlState(NitroxId cameraId)
    {
        pendingControls.Remove(cameraId);
        bool hadControlState = pendingControl.Remove(cameraId);
        hadControlState |= locallyControlled.Remove(cameraId);
        hadControlState |= remotelyControlled.Remove(cameraId);
        lastBroadcastLightState.Remove(cameraId);
        stalkerCameraLockPurposeTracker.Forget(cameraId);
        MovementBroadcaster.UnregisterWatched(cameraId);
        if (NitroxEntity.TryGetObjectFrom(cameraId, out GameObject cameraObject) &&
            cameraObject.TryGetComponent<MapRoomCameraMovementReplicator>(out MapRoomCameraMovementReplicator replicator))
        {
            UnityEngine.Object.Destroy(replicator);
            MapRoomCameraDocks.SettleInDock(cameraObject);
        }
        if (hadControlState)
        {
            diagnostics.Record("ownership_clear", "ok", cameraId, reason: "canonical_drop");
        }
    }

    public void BroadcastLightIfChanged(MapRoomCamera camera)
    {
        if (camera && camera.lightsParent && camera.TryGetNitroxId(out NitroxId cameraId))
        {
            bool lightOn = camera.lightsParent.activeSelf;
            if (!lastBroadcastLightState.TryGetValue(cameraId, out bool lastLightOn) || lastLightOn != lightOn)
            {
                lastBroadcastLightState[cameraId] = lightOn;
                packetSender.Send(new MapRoomCameraLight(cameraId, lightOn));
            }
        }
    }

    public void ProcessLight(MapRoomCameraLight packet)
    {
        diagnostics.RecordIfIgnored("light_apply", packet.CameraId, packet.IsServerResponse, packet.Granted, packet.Revision, lightRevisions);
        if (!packet.IsServerResponse || !packet.Granted ||
            lightRevisions.TryGetValue(packet.CameraId, out long revision) && packet.Revision < revision)
        {
            return;
        }
        lightRevisions[packet.CameraId] = packet.Revision;
        if (!NitroxEntity.TryGetObjectFrom(packet.CameraId, out GameObject cameraObject) || !cameraObject.GetComponent<MapRoomCamera>())
        {
            pendingLights[packet.CameraId] = packet;
            return;
        }
        pendingLights.Remove(packet.CameraId);
        SetLight(cameraObject, packet.On);
        lastBroadcastLightState[packet.CameraId] = packet.On;
        diagnostics.Record("light_apply", "ok", packet.CameraId, revision: packet.Revision, reason: packet.On ? "on" : "off");
    }

    public void BroadcastComponentStateIfChanged(MapRoomCamera camera)
    {
        if (!camera || !camera.TryGetNitroxId(out NitroxId cameraId) || (!locallyControlled.Contains(cameraId) && !simulationOwnership.HasAnyLockType(cameraId)))
        {
            return;
        }
        // Until the server's battery and health have been applied, what the camera shows is what its prefab came
        // with, and while its battery is being put in, it shows none.
        if (!lastComponents.TryGetValue(cameraId, out (float Energy, float Health) last) || initializingCameraBatteries.ContainsKey(cameraId))
        {
            return;
        }
        float energy = camera.energyMixin.charge;
        float health = camera.liveMixin.health;
        if (Math.Abs(last.Energy - energy) >= ENERGY_BROADCAST_THRESHOLD || Math.Abs(last.Health - health) >= HEALTH_BROADCAST_THRESHOLD)
        {
            lastComponents[cameraId] = (energy, health);
            packetSender.Send(new MapRoomCameraComponentState(cameraId, energy, health));
        }
    }

    /// <summary>
    ///     A camera in a dock is recharged by the player who simulates it, like everything else about its battery.
    /// </summary>
    private bool CanSimulateDockedCamera(MapRoomCamera camera) =>
        camera && camera.dockingPoint && camera.TryGetNitroxId(out NitroxId cameraId) && simulationOwnership.HasAnyLockType(cameraId);

    public void ProcessComponentState(MapRoomCameraComponentState packet)
    {
        diagnostics.RecordIfIgnored("component_apply", packet.CameraId, packet.IsServerResponse, packet.Granted, packet.Revision, componentRevisions);
        if (!packet.IsServerResponse || !packet.Granted || componentRevisions.TryGetValue(packet.CameraId, out long revision) && packet.Revision < revision)
        {
            return;
        }
        componentRevisions[packet.CameraId] = packet.Revision;
        if (!NitroxEntity.TryGetComponentFrom(packet.CameraId, out MapRoomCamera camera))
        {
            pendingComponents[packet.CameraId] = packet;
            return;
        }
        pendingComponents.Remove(packet.CameraId);
        ApplyComponentState(camera, packet);
    }

    /// <summary>
    ///     Gives a camera the number the server assigned to it. Without one, the game numbers a camera by its place in
    ///     this client's own list of cameras.
    /// </summary>
    public void SetCameraNumber(MapRoomCamera camera, int cameraNumber)
    {
        if (camera.cameraNumber != cameraNumber)
        {
            camera.cameraNumber = cameraNumber;
            camera.UpdatePingLabel();
            TryApplyPendingPreview();
        }
    }

    /// <summary>
    ///     Called when a camera has spawned and the state it was sent with is applied. Whatever arrived for it before
    ///     it existed is newer than that state and is applied now.
    /// </summary>
    public void CameraSpawned(NitroxId cameraId)
    {
        if (pendingLights.TryGetValue(cameraId, out MapRoomCameraLight light))
        {
            ProcessLight(light);
        }
        if (pendingComponents.TryGetValue(cameraId, out MapRoomCameraComponentState components))
        {
            ProcessComponentState(components);
        }
        if (pendingControls.TryGetValue(cameraId, out MapRoomCameraControl control))
        {
            ProcessControl(control);
        }
        TryApplyPendingPreview();
    }

    private void ApplyComponentState(MapRoomCamera camera, MapRoomCameraComponentState packet)
    {
        if (camera.energyMixin.battery != null)
        {
            camera.energyMixin.battery.charge = packet.Energy;
            pendingCameraEnergy.Remove(packet.CameraId);
            // A replacement camera can become ready while an older instance still owns the wait.
            // Supersede that generation; its coroutine will observe that it is stale and exit.
            initializingCameraBatteries.Remove(packet.CameraId);
        }
        else
        {
            pendingCameraEnergy[packet.CameraId] = packet.Energy;
            if (!initializingCameraBatteries.TryGetValue(packet.CameraId, out CameraBatteryInitialization initialization) ||
                !ReferenceEquals(initialization.Camera, camera))
            {
                long generation = ++nextCameraBatteryInitializationGeneration;
                initializingCameraBatteries[packet.CameraId] = new CameraBatteryInitialization(camera, generation);
                UWE.CoroutineHost.StartCoroutine(InitializeCameraBattery(camera, packet.CameraId, generation));
            }
        }
        liveMixinManager.SyncRemoteHealth(camera.liveMixin, packet.Health);
        lastComponents[packet.CameraId] = (packet.Energy, packet.Health);
        diagnostics.RecordComponentApplied(packet.CameraId, packet.Energy, packet.Health, packet.Revision, true);
    }

    public void UpdateEnergyRecharge(MapRoomCamera camera)
    {
        bool charging = false;
        if (CanSimulateDockedCamera(camera) && camera.energyMixin.battery != null)
        {
            float current = camera.energyMixin.charge;
            float amount = CalculateDockCharge(current, camera.energyMixin.capacity);
            if (amount > 0f)
            {
                PowerRelay relay = camera.dockingPoint.GetComponentInParent<PowerRelay>();
                float consumed = 0f;
                if (relay)
                {
                    PowerSystem.ConsumeEnergy(relay, amount, out consumed);
                }
                // As in the game, whatever the base supplied is added, even when it could not supply all that was asked.
                // ConsumeEnergy returns false for a partial draw but has still taken that energy from the base.
                if (!GameModeUtils.RequiresPower())
                {
                    camera.energyMixin.AddEnergy(amount);
                    charging = true;
                }
                else if (consumed > 0f)
                {
                    camera.energyMixin.AddEnergy(consumed);
                    charging = true;
                }
            }
        }
        if (charging)
        {
            camera.chargingSound.Play();
        }
        else
        {
            camera.chargingSound.Stop(STOP_MODE.IMMEDIATE);
        }
        BroadcastComponentStateIfChanged(camera);
    }

    /// <summary>
    ///     Energy a docked camera takes in one update. This is the game's own rule (MapRoomCamera.UpdateEnergyRecharge):
    ///     a tenth of the battery's capacity per update until full, so a docked camera is recharged almost at once.
    /// </summary>
    internal static float CalculateDockCharge(float current, float capacity)
    {
        if (capacity <= 0f || current >= capacity)
        {
            return 0f;
        }
        return Math.Min(capacity - Math.Max(0f, current), capacity * DOCK_CHARGE_FRACTION_PER_UPDATE);
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
                Log.Error(populationException, $"[MapRoomCameras] Failed to request a restored battery for camera {cameraId}; authoritative component broadcasts remain suppressed");
                diagnostics.Record("component_apply", "pending", cameraId, reason: "battery_spawn_exception");
            }
            else if (camera && camera.energyMixin.battery == null &&
                IsCurrentCameraBatteryInitialization(cameraId, camera, generation))
            {
                Log.Warn($"[MapRoomCameras] Restored camera {cameraId} battery is still unavailable after {CAMERA_BATTERY_WAIT_SECONDS} seconds; authoritative component broadcasts remain suppressed");
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
                Log.Info($"[MapRoomCameras] Initialized restored camera {cameraId} battery with {energy:F2} energy");
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

    private static void SetLight(GameObject cameraObject, bool on)
    {
        if (!cameraObject.TryGetComponent(out MapRoomCamera camera))
        {
            return;
        }

        // Start restores lightsParent from this serialized field. Keep it canonical even when
        // authoritative state arrives between prefab creation and the component's Start callback.
        camera.lightState = on;
        if (camera.lightsParent && camera.lightsParent.activeSelf != on)
        {
            camera.lightsParent.SetActive(on);
        }
    }

    private readonly record struct CameraBatteryInitialization(MapRoomCamera Camera, long Generation);

    private static byte[] CapturePreviewJpeg(RenderTexture source)
    {
        const int SIZE = MapRoomCameraPreviewImage.MAX_DIMENSION;
        RenderTexture temporary = RenderTexture.GetTemporary(SIZE, SIZE, 0, RenderTextureFormat.ARGB32,
            RenderTextureReadWrite.Default);
        RenderTexture previous = RenderTexture.active;
        Texture2D readback = null;
        try
        {
            Graphics.Blit(source, temporary);
            RenderTexture.active = temporary;
            readback = new Texture2D(SIZE, SIZE, TextureFormat.RGB24, mipChain: false);
            readback.ReadPixels(new Rect(0f, 0f, SIZE, SIZE), 0, 0, recalculateMipMaps: false);
            readback.Apply(updateMipmaps: false, makeNoLongerReadable: false);
            return ImageConversion.EncodeToJPG(readback, PREVIEW_JPEG_QUALITY);
        }
        finally
        {
            RenderTexture.active = previous;
            if (readback)
            {
                UnityEngine.Object.Destroy(readback);
            }
            RenderTexture.ReleaseTemporary(temporary);
        }
    }

    private static MapRoomCamera FindCamera(NitroxId cameraId)
    {
        if (NitroxEntity.TryGetObjectFrom(cameraId, out GameObject cameraObject) && cameraObject &&
            cameraObject.TryGetComponent(out MapRoomCamera camera))
        {
            return camera;
        }
        foreach (MapRoomCamera candidate in MapRoomCamera.cameras)
        {
            if (candidate && candidate.TryGetNitroxId(out NitroxId candidateId) && candidateId == cameraId)
            {
                return candidate;
            }
        }
        return null;
    }

    private static RenderTexture FindSharedPreviewTexture(MapRoomCamera camera)
    {
        if (camera && camera.mapRoomRenderTexture)
        {
            return camera.mapRoomRenderTexture;
        }
        foreach (MapRoomCamera candidate in MapRoomCamera.cameras)
        {
            if (candidate && candidate.mapRoomRenderTexture)
            {
                return candidate.mapRoomRenderTexture;
            }
        }
        foreach (MapRoomFunctionality mapRoom in Resources.FindObjectsOfTypeAll<MapRoomFunctionality>())
        {
            if (mapRoom && mapRoom.gameObject.scene.IsValid() && mapRoom.mapRoomRenderTexture)
            {
                return mapRoom.mapRoomRenderTexture;
            }
        }
        return null;
    }

    private static bool TryApplyPreviewPixels(RenderTexture target, byte[] jpegBytes)
    {
        Texture2D decoded = null;
        try
        {
            decoded = new Texture2D(2, 2, TextureFormat.RGB24, mipChain: false);
            if (!ImageConversion.LoadImage(decoded, jpegBytes, markNonReadable: true) ||
                decoded.width > MapRoomCameraPreviewImage.MAX_DIMENSION ||
                decoded.height > MapRoomCameraPreviewImage.MAX_DIMENSION)
            {
                return false;
            }
            Graphics.Blit(decoded, target);
            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[MapRoomCameras] Failed to apply remote camera preview");
            return false;
        }
        finally
        {
            if (decoded)
            {
                UnityEngine.Object.Destroy(decoded);
            }
        }
    }

    private static void ApplyGlobalPreviewSelection(MapRoomCamera camera, int cameraNumber)
    {
        foreach (MapRoomScreen screen in Resources.FindObjectsOfTypeAll<MapRoomScreen>())
        {
            if (!screen || !screen.gameObject.scene.IsValid())
            {
                continue;
            }
            if (camera)
            {
                screen.OnMapRoomCameraChanged(camera);
            }
            else
            {
                if (screen.cameraText && Language.main != null)
                {
                    screen.cameraText.text = Language.main.GetFormat("MapRoomCameraInfoScreen", cameraNumber);
                }
                if (screen.cameraPreview)
                {
                    screen.cameraPreview.SetActive(true);
                }
            }
        }
    }
}
