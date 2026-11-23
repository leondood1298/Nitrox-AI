using System;
using System.Collections;
using System.Collections.Generic;
using FMOD.Studio;
using Nitrox.Model.DataStructures;
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
    private const float DOCK_CHARGE_FRACTION_PER_UPDATE = 0.1f;
    private const int PREVIEW_JPEG_QUALITY = 55;

    private readonly IPacketSender packetSender;
    private readonly IMultiplayerSession multiplayerSession;
    private readonly SimulationOwnership simulationOwnership;
    private readonly MapRoomCameraStates cameraStates;
    private readonly ScannerRoomClientDiagnostics diagnostics;
    private readonly StalkerCameraLockPurposeTracker stalkerCameraLockPurposeTracker;

    private readonly HashSet<NitroxId> locallyControlled = new();
    private readonly HashSet<NitroxId> pendingControl = new();
    private readonly HashSet<NitroxId> remotelyControlled = new();

    /// <summary>
    ///     Who the server said controls cameras that had not spawned on this client yet. Applied when the camera spawns.
    /// </summary>
    private readonly Dictionary<NitroxId, MapRoomCameraControl> pendingControls = new();
    private long latestPreviewRevision;
    private MapRoomCameraPreview pendingPreview;
    private bool pendingPreviewPixelsApplied;
    private long lastPreviewApplyDiagnosticRevision;
    private string lastPreviewApplyDiagnosticOutcome;

    public MapRoomCameras(IPacketSender packetSender, IMultiplayerSession multiplayerSession, SimulationOwnership simulationOwnership,
        MapRoomCameraStates cameraStates, ScannerRoomClientDiagnostics diagnostics, StalkerCameraLockPurposeTracker stalkerCameraLockPurposeTracker)
    {
        this.packetSender = packetSender;
        this.multiplayerSession = multiplayerSession;
        this.simulationOwnership = simulationOwnership;
        this.cameraStates = cameraStates;
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
            pendingControl.Add(cameraId);
            packetSender.Send(new MapRoomCameraControl(cameraId, mapRoomId, cameraIndex, isControlling: true, lightOn));
            diagnostics.Record("control_publish", "ok", cameraId, mapRoomId.HasValue ? mapRoomId.Value : null, slot: cameraIndex, reason: "acquire_requested");
        }
        else if (camera.TryGetNitroxId(out NitroxId cameraId))
        {
            pendingControl.Remove(cameraId);
            locallyControlled.Remove(cameraId);
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
            MapRoomCameraStates.SetLight(cameraObject.GetComponent<MapRoomCamera>(), packet.LightOn);
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

    /// <summary>
    ///     A camera in a dock is recharged by the player who simulates it, like everything else about its battery.
    /// </summary>
    private bool CanSimulateDockedCamera(MapRoomCamera camera) =>
        camera && camera.dockingPoint && camera.TryGetNitroxId(out NitroxId cameraId) && simulationOwnership.HasAnyLockType(cameraId);

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
    ///     Called when a camera has spawned and the state it was sent with is applied. If the server said before that
    ///     somebody controls it, that is applied now.
    /// </summary>
    public void CameraSpawned(NitroxId cameraId)
    {
        if (pendingControls.TryGetValue(cameraId, out MapRoomCameraControl control))
        {
            ProcessControl(control);
        }
        TryApplyPendingPreview();
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
        cameraStates.ReportIfChanged(camera);
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
