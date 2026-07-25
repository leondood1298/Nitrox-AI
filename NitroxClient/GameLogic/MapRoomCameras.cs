using System;
using System.Collections;
using System.Collections.Generic;
using Nitrox.Model.Core;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Logger;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Bases;
using Nitrox.Model.Subnautica.Extensions;
using Nitrox.Model.Subnautica.Helper;
using Nitrox.Model.Subnautica.Packets;
using NitroxClient.Communication;
using NitroxClient.Communication.Abstract;
using NitroxClient.Extensions;
using NitroxClient.GameLogic.Helper;
using NitroxClient.MonoBehaviours;
using UnityEngine;

namespace NitroxClient.GameLogic;

public class MapRoomCameras
{
	private readonly IPacketSender packetSender;
	private readonly IMultiplayerSession multiplayerSession;
	private readonly LiveMixinManager liveMixinManager;
	private readonly SimulationOwnership simulationOwnership;
	private readonly ScannerRoomClientDiagnostics diagnostics;
	private readonly StalkerCameraLockPurposeTracker stalkerCameraLockPurposeTracker;

	private readonly Dictionary<NitroxId, bool> lastBroadcastLightState = new Dictionary<NitroxId, bool>();

	private readonly HashSet<NitroxId> locallyControlled = new HashSet<NitroxId>();
	private readonly HashSet<NitroxId> pendingControl = new HashSet<NitroxId>();
	private readonly HashSet<NitroxId> remotelyControlled = new HashSet<NitroxId>();
	private readonly Dictionary<NitroxId, long> dockingRevisions = new Dictionary<NitroxId, long>();
	private readonly Dictionary<NitroxId, long> lightRevisions = new Dictionary<NitroxId, long>();
	private readonly Dictionary<NitroxId, (float Energy, float Health)> lastComponents = new();
	private readonly Dictionary<NitroxId, long> componentRevisions = new();
	private readonly Dictionary<NitroxId, float> pendingCameraEnergy = new();
	private readonly Dictionary<NitroxId, CameraBatteryInitialization> initializingCameraBatteries = new();
	private readonly MapRoomCameraRestoreStateCache restoreStateCache = new();
	private readonly MapRoomCameraBootstrapStateCache bootstrapStateCache = new();
	private readonly HashSet<NitroxId> bootstrapReplayRooms = new();
	private readonly HashSet<NitroxId> deferredCameraState = new();
	private readonly HashSet<NitroxId> cameraRestoreBarriers = new();
	private static readonly NitroxTechType mapRoomCameraTechType = new("MapRoomCamera");
	private static readonly HashSet<int> camerasAwaitingVanillaRegistration = [];
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

	public bool CanBeginControl(MapRoomCamera camera)
	{
		if (!camera)
		{
			return false;
		}
		if (!HasReadyDockAssociation(camera))
		{
			return false;
		}
		if (!TryGetStaleSlotAssociation(camera, out MapRoomCameraDock authoritativeDock,
			out NitroxId mapRoomId, out int cameraIndex))
		{
			return true;
		}
		diagnostics.Record("control_publish", "reject", authoritativeDock.CameraId, mapRoomId,
			authoritativeDock.Revision, cameraIndex, "stale_slot_camera");
		return false;
	}

	private bool TryGetStaleSlotAssociation(MapRoomCamera camera, out MapRoomCameraDock authoritativeDock,
		out NitroxId mapRoomId, out int cameraIndex)
	{
		authoritativeDock = null;
		mapRoomId = null;
		cameraIndex = -1;
		MapRoomFunctionality mapRoom = camera ? GetMapRoomForDock(camera.dockingPoint) : null;
		if (!mapRoom || !mapRoom.TryGetNitroxId(out mapRoomId))
		{
			return false;
		}
		cameraIndex = GetDockingIndex(mapRoom, camera.dockingPoint);
		return bootstrapStateCache.TryGetDock(mapRoomId, cameraIndex, out authoritativeDock) &&
			NitroxEntity.TryGetObjectFrom(authoritativeDock.CameraId, out GameObject canonicalObject) &&
			ShouldRejectStaleSlotCamera(canonicalObject, camera.gameObject);
	}

	internal static bool ShouldRejectStaleSlotCamera(GameObject canonicalObject, GameObject selectedObject) =>
		canonicalObject && canonicalObject != selectedObject;

	private static bool HasReadyDockAssociation(MapRoomCamera camera)
	{
		if (!camera || !camera.dockingPoint)
		{
			return true;
		}
		MapRoomFunctionality mapRoom = GetMapRoomForDock(camera.dockingPoint);
		return mapRoom && mapRoom.TryGetNitroxId(out NitroxId _) &&
			GetDockingIndex(mapRoom, camera.dockingPoint) >= 0;
	}

	public void BroadcastControl(MapRoomCamera camera, bool isControlling)
	{
		if (!camera)
		{
			return;
		}
		NitroxId nitroxId2;
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
				MapRoomFunctionality mapRoomForDock = GetMapRoomForDock(camera.dockingPoint);
				if (!mapRoomForDock || !mapRoomForDock.TryGetNitroxId(out NitroxId nitroxId))
				{
					camera.TryGetNitroxId(out NitroxId unresolvedCameraId);
					diagnostics.Record("control_publish", "pending", unresolvedCameraId,
						reason: "fresh_room_identity_wait");
					AbortLocalControl(camera);
					return;
				}
				mapRoomId = Optional.Of(nitroxId);
				cameraIndex = GetDockingIndex(mapRoomForDock, camera.dockingPoint);
				if (cameraIndex < 0)
				{
					camera.TryGetNitroxId(out NitroxId unresolvedCameraId);
					diagnostics.Record("control_publish", "pending", unresolvedCameraId, nitroxId,
						slot: cameraIndex, reason: "fresh_room_slot_wait");
					AbortLocalControl(camera);
					return;
				}
				if (bootstrapStateCache.TryGetDock(nitroxId, cameraIndex, out MapRoomCameraDock authoritativeDock))
				{
					if (NitroxEntity.TryGetObjectFrom(authoritativeDock.CameraId, out GameObject canonicalObject) &&
						canonicalObject && canonicalObject != camera.gameObject)
					{
						AbortLocalControl(camera);
						return;
					}
					if (!TryAssignCameraId(camera.gameObject, authoritativeDock.CameraId))
					{
						diagnostics.Record("control_publish", "reject", authoritativeDock.CameraId, nitroxId,
							authoritativeDock.Revision, cameraIndex, "canonical_id_collision");
						AbortLocalControl(camera);
						return;
					}
					if (!authoritativeDock.IsDocked)
					{
						mapRoomId = Optional.Empty;
						cameraIndex = -1;
					}
				}
				else
				{
					if (!EnsureCameraId(camera.dockingPoint, camera))
					{
						AbortLocalControl(camera);
						return;
					}
				}
			}
			NitroxId idOrGenerateNew = NitroxEntity.GetIdOrGenerateNew(camera.gameObject);
			bool flag = (bool)camera.lightsParent && camera.lightsParent.activeSelf;
			lastBroadcastLightState[idOrGenerateNew] = flag;
			pendingControl.Add(idOrGenerateNew);
			packetSender.Send(new MapRoomCameraControl(idOrGenerateNew, mapRoomId, cameraIndex, isControlling: true, flag));
		}
		else if (camera.TryGetNitroxId(out nitroxId2))
		{
			pendingControl.Remove(nitroxId2);
			locallyControlled.Remove(nitroxId2);
			lastBroadcastLightState.Remove(nitroxId2);
			// Release the camera-specific lock before generic movement ownership is relinquished.
			// Otherwise the server can see an already-unlocked camera and fail to notify remote clients.
			packetSender.Send(new MapRoomCameraControl(nitroxId2, Optional.Empty, -1, isControlling: false, lightOn: false));
			MovementBroadcaster.UnregisterWatched(nitroxId2);
		}
	}

	private static void AbortLocalControl(MapRoomCamera camera)
	{
		if (!camera)
		{
			return;
		}
		camera.enabled = true;
		using (PacketSuppressor<MapRoomCameraControl>.Suppress())
		{
			camera.ExitLockedMode(resetPlayerPosition: false);
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
			bootstrapStateCache.RetainControl(packet);
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
				slot: packet.CameraIndex, reason: isLocalController ? "denied" : "locked_remote");
			if (!isLocalController)
			{
				remotelyControlled.Add(packet.CameraId);
			}
			if (pendingControl.Remove(packet.CameraId) && NitroxEntity.TryGetObjectFrom(packet.CameraId, out GameObject deniedObject) && deniedObject.TryGetComponent(out MapRoomCamera deniedCamera))
			{
				deniedCamera.enabled = true;
				using (PacketSuppressor<MapRoomCameraControl>.Suppress())
				{
					deniedCamera.ExitLockedMode(resetPlayerPosition: false);
				}
			}
			return;
		}
		GameObject gameObject2;
		MapRoomCameraMovementReplicator component;
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
			GameObject gameObject = ResolveCameraObject(packet.CameraId, packet.MapRoomId, packet.CameraIndex);
			if (!gameObject)
			{
				bootstrapStateCache.RetainControl(packet);
				diagnostics.Record("control_apply", "pending", packet.CameraId, packet.MapRoomId.HasValue ? packet.MapRoomId.Value : null,
					slot: packet.CameraIndex, reason: "waiting_object");
				Log.Warn(string.Format("[{0}] Deferring camera control until its fresh-room object exists for {1}", "MapRoomCameras", packet));
				return;
			}
			bootstrapStateCache.RemoveControl(packet.CameraId);
			SetLight(gameObject, packet.LightOn);
			if (isLocalController)
			{
				remotelyControlled.Remove(packet.CameraId);
				pendingControl.Remove(packet.CameraId);
				locallyControlled.Add(packet.CameraId);
				if (gameObject.TryGetComponent(out MapRoomCamera localCamera))
				{
					localCamera.enabled = true;
					if (uGUI_CameraDrone.main && uGUI_CameraDrone.main.GetCamera() == localCamera)
					{
						uGUI_CameraDrone.main.noSignal.SetActive(false);
					}
				}
				MovementBroadcaster.RegisterWatched(gameObject, packet.CameraId);
				diagnostics.Record("control_apply", "ok", packet.CameraId, packet.MapRoomId.HasValue ? packet.MapRoomId.Value : null,
					slot: packet.CameraIndex, reason: "local");
				return;
			}
			if (!gameObject.GetComponent<MapRoomCameraMovementReplicator>())
			{
				gameObject.AddComponent<MapRoomCameraMovementReplicator>();
			}
			remotelyControlled.Add(packet.CameraId);
			diagnostics.Record("control_apply", "ok", packet.CameraId, packet.MapRoomId.HasValue ? packet.MapRoomId.Value : null,
				slot: packet.CameraIndex, reason: "remote");
		}
		else if (NitroxEntity.TryGetObjectFrom(packet.CameraId, out gameObject2) && gameObject2.TryGetComponent<MapRoomCameraMovementReplicator>(out component))
		{
			UnityEngine.Object.Destroy(component);
		}
		if (!packet.IsControlling)
		{
			remotelyControlled.Remove(packet.CameraId);
		}
		if (!packet.IsControlling && isLocalController)
		{
			pendingControl.Remove(packet.CameraId);
			locallyControlled.Remove(packet.CameraId);
			stalkerCameraLockPurposeTracker.Forget(packet.CameraId);
			MovementBroadcaster.UnregisterWatched(packet.CameraId);
		}
		if (!packet.IsControlling)
		{
			diagnostics.Record("control_release_apply", "ok", packet.CameraId, packet.MapRoomId.HasValue ? packet.MapRoomId.Value : null,
				slot: packet.CameraIndex, reason: isLocalController ? "local" : "remote");
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

	public void TryApplyPendingPreview()
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
		if (lastPreviewApplyDiagnosticRevision == packet.Revision && lastPreviewApplyDiagnosticOutcome == outcome + reason)
		{
			return;
		}
		lastPreviewApplyDiagnosticRevision = packet.Revision;
		lastPreviewApplyDiagnosticOutcome = outcome + reason;
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
		// MapRoomScreen performs input-stack and current-camera mutations around ControlCamera.
		// Exclude a stale duplicate here, before FindCamera can choose it.
		if (TryGetStaleSlotAssociation(camera, out _, out _, out _))
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

	public static bool CanSelectForControl(bool pending, bool locallyControlled, bool remotelyControlled, bool activelyControlled) =>
		locallyControlled || (!remotelyControlled && (!pending || activelyControlled));

	public bool HasPendingControl(NitroxId cameraId) => pendingControl.Contains(cameraId);

	public bool HasLocalControl(NitroxId cameraId) => locallyControlled.Contains(cameraId);

	internal void ClearControlState(NitroxId cameraId)
	{
		bootstrapStateCache.RemoveControl(cameraId);
		bool hadControlState = pendingControl.Remove(cameraId);
		hadControlState |= locallyControlled.Remove(cameraId);
		hadControlState |= remotelyControlled.Remove(cameraId);
		lastBroadcastLightState.Remove(cameraId);
		stalkerCameraLockPurposeTracker.Forget(cameraId);
		MovementBroadcaster.UnregisterWatched(cameraId);
		if (NitroxEntity.TryGetObjectFrom(cameraId, out GameObject gameObject) &&
			gameObject.TryGetComponent<MapRoomCameraMovementReplicator>(out MapRoomCameraMovementReplicator replicator))
		{
			UnityEngine.Object.Destroy(replicator);
		}
		if (hadControlState)
		{
			diagnostics.Record("ownership_clear", "ok", cameraId, reason: "canonical_drop");
		}
	}

	public void BroadcastLightIfChanged(MapRoomCamera camera)
	{
		if ((bool)camera && (bool)camera.lightsParent && camera.TryGetNitroxId(out NitroxId nitroxId))
		{
			if (restoreStateCache.HasPending(nitroxId) || HasActiveCameraRestoreBarrier(nitroxId))
			{
				return;
			}
			bool activeSelf = camera.lightsParent.activeSelf;
			if (!lastBroadcastLightState.TryGetValue(nitroxId, out var value) || value != activeSelf)
			{
				lastBroadcastLightState[nitroxId] = activeSelf;
				packetSender.Send(new MapRoomCameraLight(nitroxId, activeSelf));
			}
		}
	}

	public void ProcessLight(MapRoomCameraLight packet)
	{
		if (!packet.IsServerResponse || !packet.Granted ||
			lightRevisions.TryGetValue(packet.CameraId, out long revision) && packet.Revision < revision)
		{
			return;
		}
		lightRevisions[packet.CameraId] = packet.Revision;
		restoreStateCache.Retain(packet);
		if (!TryApplyPendingCameraState(packet.CameraId))
		{
			if (deferredCameraState.Add(packet.CameraId))
			{
				diagnostics.Record("light_apply", "pending", packet.CameraId, revision: packet.Revision, reason: "waiting_object");
			}
		}
	}

	public void BroadcastComponentStateIfChanged(MapRoomCamera camera)
	{
		if (!camera || !camera.TryGetNitroxId(out NitroxId id) || (!locallyControlled.Contains(id) && !simulationOwnership.HasAnyLockType(id) && !CanSimulateDockedCamera(camera))) return;
		if (ShouldSuppressComponentBroadcast(restoreStateCache.HasPending(id),
			initializingCameraBatteries.ContainsKey(id), HasActiveCameraRestoreBarrier(id))) return;
		float energy = camera.energyMixin.charge;
		float health = camera.liveMixin.health;
		if (!lastComponents.TryGetValue(id, out var state) || Math.Abs(state.Energy - energy) >= 0.5f || Math.Abs(state.Health - health) >= 0.05f)
		{
			lastComponents[id] = (energy, health);
			packetSender.Send(new MapRoomCameraComponentState(id, energy, health));
		}
	}

	public bool CanSimulateDockedCamera(MapRoomCamera camera)
	{
		MapRoomFunctionality mapRoom = camera ? GetMapRoomForDock(camera.dockingPoint) : null;
		return mapRoom && mapRoom.TryGetNitroxId(out NitroxId mapRoomId) && simulationOwnership.HasAnyLockType(mapRoomId);
	}

	public void ProcessComponentState(MapRoomCameraComponentState packet)
	{
		if (!packet.IsServerResponse || !packet.Granted || componentRevisions.TryGetValue(packet.CameraId, out long revision) && packet.Revision < revision) return;
		componentRevisions[packet.CameraId] = packet.Revision;
		restoreStateCache.Retain(packet);
		if (!TryApplyPendingCameraState(packet.CameraId))
		{
			if (deferredCameraState.Add(packet.CameraId))
			{
				diagnostics.Record("component_apply", "pending", packet.CameraId, revision: packet.Revision, reason: "waiting_object");
			}
		}
	}

	internal static bool ShouldSuppressComponentBroadcast(bool authoritativeStatePending, bool batteryInitializing,
		bool restoreBarrier) => authoritativeStatePending || batteryInitializing || restoreBarrier;

	public void CameraObjectSpawned(NitroxId cameraId, GameObject cameraObject)
	{
		if (restoreStateCache.HasPending(cameraId))
		{
			TryApplyPendingCameraState(cameraId, cameraObject);
		}
		foreach (MapRoomCameraDock dock in bootstrapStateCache.GetDocksForCamera(cameraId))
		{
			if (NitroxEntity.TryGetObjectFrom(dock.MapRoomId, out GameObject mapRoomObject) && mapRoomObject &&
				mapRoomObject.TryGetComponent(out MapRoomFunctionality mapRoom))
			{
				ApplyCachedDocks(mapRoom, dock.MapRoomId, dock);
			}
		}
		ApplyPendingControls();
	}

	public void CameraObjectWillSpawn(NitroxId cameraId, NitroxTechType techType, bool initialSyncCompleted)
	{
		bool isMapRoomCamera = mapRoomCameraTechType.Equals(techType);
		bool hasKnownState = isMapRoomCamera && restoreStateCache.MarkPendingForSpawn(cameraId);
		if (ShouldCreateCameraRestoreBarrier(isMapRoomCamera, initialSyncCompleted, hasKnownState))
		{
			cameraRestoreBarriers.Add(cameraId);
		}
	}

	internal static bool ShouldCreateCameraRestoreBarrier(bool isMapRoomCamera, bool initialSyncCompleted,
		bool hasKnownState) => isMapRoomCamera && (!initialSyncCompleted || hasKnownState);

	private bool HasActiveCameraRestoreBarrier(NitroxId cameraId)
	{
		if (!cameraRestoreBarriers.Contains(cameraId))
		{
			return false;
		}

		bool initialSyncCompleted = Multiplayer.Main && Multiplayer.Main.InitialSyncCompleted;
		if (ShouldReleaseUnknownCameraRestoreBarrier(initialSyncCompleted, restoreStateCache.HasKnownState(cameraId)))
		{
			cameraRestoreBarriers.Remove(cameraId);
			diagnostics.Record("restore_apply", "ok", cameraId, reason: "initial_sync_no_canonical_state");
			return false;
		}
		return true;
	}

	internal static bool ShouldReleaseUnknownCameraRestoreBarrier(bool initialSyncCompleted, bool hasKnownState) =>
		initialSyncCompleted && !hasKnownState;

	private bool TryApplyPendingCameraState(NitroxId cameraId, GameObject knownCameraObject = null)
	{
		GameObject cameraObject = knownCameraObject;
		MapRoomCamera camera = null;
		bool objectFound = cameraObject && cameraObject.TryGetComponent(out camera);
		if (!objectFound)
		{
			objectFound = NitroxEntity.TryGetObjectFrom(cameraId, out cameraObject) && cameraObject &&
				cameraObject.TryGetComponent(out camera);
		}
		if (!restoreStateCache.TryTake(cameraId, objectFound, out MapRoomCameraRestoreStateCache.PendingCameraState state))
		{
			return objectFound;
		}

		int? cameraNumber = null;
		long? componentRevision = null;
		if (state.Record is { } record)
		{
			camera.cameraNumber = record.CameraNumber;
			camera.UpdatePingLabel();
			cameraNumber = record.CameraNumber;
		}
		if (state.Light is { } light)
		{
			SetLight(cameraObject, light.On);
			lastBroadcastLightState[cameraId] = light.On;
			diagnostics.Record("light_apply", "ok", cameraId, revision: light.Revision, reason: light.On ? "on" : "off");
		}
		if (state.Component is { } component)
		{
			ApplyComponentState(camera, component);
			componentRevision = component.Revision;
		}
		if (deferredCameraState.Remove(cameraId))
		{
			diagnostics.Record("restore_apply", "ok", cameraId, revision: componentRevision, slot: cameraNumber,
				reason: "delayed_object");
		}
		TryApplyPendingPreview();
		return true;
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
		cameraRestoreBarriers.Remove(packet.CameraId);
		diagnostics.RecordComponentApplied(packet.CameraId, packet.Energy, packet.Health, packet.Revision, true);
	}

	public void UpdateEnergyRecharge(MapRoomCamera camera)
	{
		bool charging = false;
		if (CanSimulateDockedCamera(camera) && camera.energyMixin.battery != null)
		{
			float current = camera.energyMixin.charge;
			float amount = CalculateDockCharge(current, camera.energyMixin.capacity, Time.deltaTime);
			if (amount > 0f)
			{
				PowerRelay relay = camera.dockingPoint.GetComponentInParent<PowerRelay>();
				float consumed = 0f;
				if (!GameModeUtils.RequiresPower() || ((bool)relay && PowerSystem.ConsumeEnergy(relay, amount, out consumed) && consumed > 0f))
				{
					camera.energyMixin.AddEnergy(GameModeUtils.RequiresPower() ? consumed : amount);
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
			camera.chargingSound.Stop(global::FMOD.Studio.STOP_MODE.IMMEDIATE);
		}
		BroadcastComponentStateIfChanged(camera);
	}

	public static float CalculateDockCharge(float current, float capacity, float deltaTime)
	{
		if (capacity <= 0f || deltaTime <= 0f || current >= capacity)
		{
			return 0f;
		}
		return Math.Min(capacity - Math.Max(0f, current), capacity * 0.01f * deltaTime);
	}

	private IEnumerator InitializeCameraBattery(MapRoomCamera camera, NitroxId cameraId, long generation)
	{
		bool canonicalEnergyApplied = false;
		bool populationFailed = false;
		Exception populationException = null;
		try
		{
			if ((bool)camera && camera.energyMixin.battery == null)
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
					float timeoutAt = Time.time + 10f;
					yield return new WaitUntil(() => !camera || camera.energyMixin.battery != null ||
						!IsCurrentCameraBatteryInitialization(cameraId, camera, generation) || Time.time >= timeoutAt);
				}
			}

			if (populationFailed && IsCurrentCameraBatteryInitialization(cameraId, camera, generation))
			{
				Log.Error(populationException, $"[MapRoomCameras] Failed to request a restored battery for camera {cameraId}; authoritative component broadcasts remain suppressed");
				diagnostics.Record("component_apply", "pending", cameraId, reason: "battery_spawn_exception");
			}
			else if ((bool)camera && camera.energyMixin.battery == null &&
				IsCurrentCameraBatteryInitialization(cameraId, camera, generation))
			{
				Log.Warn($"[MapRoomCameras] Restored camera {cameraId} battery is still unavailable after 10 seconds; authoritative component broadcasts remain suppressed");
				diagnostics.Record("component_apply", "pending", cameraId, reason: "battery_wait_timeout");
			}

			if ((bool)camera && camera.energyMixin.battery == null &&
				IsCurrentCameraBatteryInitialization(cameraId, camera, generation))
			{
				yield return new WaitUntil(() => !camera || camera.energyMixin.battery != null ||
					!IsCurrentCameraBatteryInitialization(cameraId, camera, generation));
			}

			if (IsCurrentCameraBatteryInitialization(cameraId, camera, generation) && (bool)camera &&
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

	public void BroadcastDock(MapRoomCameraDocking dockingPoint, MapRoomCamera camera)
	{
		if (!PacketSuppressor<MapRoomCameraDock>.IsSuppressed && (bool)dockingPoint && (bool)camera && camera.TryGetNitroxId(out NitroxId nitroxId))
		{
			MapRoomFunctionality mapRoomForDock = GetMapRoomForDock(dockingPoint);
			if ((bool)mapRoomForDock && mapRoomForDock.TryGetNitroxId(out NitroxId nitroxId2))
			{
				packetSender.Send(new MapRoomCameraDock(nitroxId, nitroxId2, GetDockingIndex(mapRoomForDock, dockingPoint)));
			}
		}
	}

	public void BroadcastUndock(MapRoomCameraDocking dockingPoint, MapRoomCamera camera)
	{
		if (!PacketSuppressor<MapRoomCameraDock>.IsSuppressed && (bool)dockingPoint && (bool)camera && camera.TryGetNitroxId(out NitroxId cameraId))
		{
			MapRoomFunctionality mapRoom = GetMapRoomForDock(dockingPoint);
			if ((bool)mapRoom && mapRoom.TryGetNitroxId(out NitroxId mapRoomId))
			{
				packetSender.Send(new MapRoomCameraDock(cameraId, mapRoomId, GetDockingIndex(mapRoom, dockingPoint), isDocked: false, cameraTransform: camera.transform.ToWorldDto()));
			}
		}
	}

	public void ProcessDock(MapRoomCameraDock packet)
	{
		if (!packet.IsServerResponse)
		{
			return;
		}
		if (!packet.Granted)
		{
			diagnostics.Record(packet.IsDocked ? "dock_apply" : "undock_apply", "reject", packet.CameraId, packet.MapRoomId,
				packet.Revision, packet.DockingIndex, "server_reject");
			return;
		}
		bootstrapStateCache.RetainDock(packet);
		if (!bootstrapStateCache.TryGetDock(packet.MapRoomId, packet.DockingIndex, out MapRoomCameraDock retainedPacket))
		{
			return;
		}
		if (packet.Revision < retainedPacket.Revision ||
			packet.Revision == retainedPacket.Revision &&
			(packet.CameraId != retainedPacket.CameraId || packet.IsDocked != retainedPacket.IsDocked))
		{
			diagnostics.Record(packet.IsDocked ? "dock_apply" : "undock_apply", "stale", packet.CameraId, packet.MapRoomId,
				packet.Revision, packet.DockingIndex, $"slot_revision_{retainedPacket.Revision}");
			return;
		}
		packet = retainedPacket;
		if (!dockingRevisions.TryGetValue(packet.MapRoomId, out long revision) || packet.Revision > revision)
		{
			dockingRevisions[packet.MapRoomId] = packet.Revision;
		}
		diagnostics.Record(packet.IsDocked ? "dock_apply" : "undock_apply", "ok", packet.CameraId, packet.MapRoomId,
			packet.Revision, packet.DockingIndex);
		if (packet.CameraNumber > 0)
		{
			RetainAndApplyCameraRecord(new MapRoomCameraRecord(packet.CameraId, packet.CameraNumber,
				packet.LightOn, packet.LightRevision, packet.Energy, packet.Health, packet.ComponentRevision),
				preferCameraNumber: true);
		}
		else
		{
			ProcessLight(new MapRoomCameraLight(packet.CameraId, packet.LightOn, packet.LightRevision, true, true));
			ProcessComponentState(new MapRoomCameraComponentState(packet.CameraId, packet.Energy, packet.Health,
				packet.ComponentRevision, true, true));
		}
		if (packet.IsDocked)
		{
			pendingControl.Remove(packet.CameraId);
			if (!locallyControlled.Contains(packet.CameraId))
			{
				MovementBroadcaster.UnregisterWatched(packet.CameraId);
			}
		}
		if (!NitroxEntity.TryGetObjectFrom(packet.MapRoomId, out GameObject mapRoomObject) || !mapRoomObject ||
			!mapRoomObject.TryGetComponent(out MapRoomFunctionality mapRoom) ||
			!ApplyCachedDocks(mapRoom, packet.MapRoomId, packet))
		{
			diagnostics.Record(packet.IsDocked ? "dock_object" : "undock_object", "diverge", packet.CameraId, packet.MapRoomId,
				packet.Revision, packet.DockingIndex, "waiting_object");
			return;
		}
		ApplyPendingControls();
	}

	private bool TryApplyDockObject(MapRoomCameraDock packet)
	{
		if (!NitroxEntity.TryGetObjectFrom(packet.MapRoomId, out GameObject mapRoomObject) || !mapRoomObject ||
			!mapRoomObject.TryGetComponent(out MapRoomFunctionality mapRoom))
		{
			return false;
		}

		List<MapRoomCameraDocking> dockingPoints = GetDockingPoints(mapRoom);
		if (packet.DockingIndex < 0 || packet.DockingIndex >= dockingPoints.Count)
		{
			return false;
		}

		MapRoomCameraDocking dockingPoint = dockingPoints[packet.DockingIndex];
		MapRoomCamera slotCamera = dockingPoint.camera;
		MapRoomCamera camera;
		GameObject cameraObject;
		if (NitroxEntity.TryGetObjectFrom(packet.CameraId, out cameraObject) && cameraObject)
		{
			if (!cameraObject.TryGetComponent(out camera))
			{
				diagnostics.Record(packet.IsDocked ? "dock_object" : "undock_object", "diverge",
					packet.CameraId, packet.MapRoomId, packet.Revision, packet.DockingIndex,
					"canonical_id_collision");
				return false;
			}
			if (!TryAssignCameraId(cameraObject, packet.CameraId))
			{
				diagnostics.Record(packet.IsDocked ? "dock_object" : "undock_object", "diverge",
					packet.CameraId, packet.MapRoomId, packet.Revision, packet.DockingIndex,
					"canonical_id_collision");
				return false;
			}
			if (slotCamera && slotCamera != camera)
			{
				RemoveUnexpectedRestoredCamera(dockingPoint, slotCamera);
			}
		}
		else if (slotCamera)
		{
			camera = slotCamera;
			cameraObject = camera.gameObject;
			if (!TryAssignCameraId(cameraObject, packet.CameraId))
			{
				diagnostics.Record(packet.IsDocked ? "dock_object" : "undock_object", "diverge",
					packet.CameraId, packet.MapRoomId, packet.Revision, packet.DockingIndex,
					"canonical_id_collision");
				return false;
			}
		}
		else if (!packet.IsDocked)
		{
			// The authoritative empty slot is already represented locally. Durable camera state
			// remains cached and will apply if/when the loose world object is streamed in.
			return true;
		}
		else
		{
			return false;
		}

		if (packet.IsDocked && cameraObject.TryGetComponent(out MapRoomCameraMovementReplicator replicator))
		{
			UnityEngine.Object.Destroy(replicator);
		}
		using (PacketSuppressor<MapRoomCameraDock>.Suppress())
		{
			if (packet.IsDocked)
			{
				if (ShouldApplyPhysicalDockTransition(isDocked: true,
					dockingPoint.camera == camera, dockingPoint.cameraDocked))
				{
					dockingPoint.DockCamera(camera);
				}
			}
			else if (ShouldApplyPhysicalDockTransition(isDocked: false,
				dockingPoint.camera == camera, dockingPoint.cameraDocked))
			{
				dockingPoint.UndockCamera();
			}
		}
		RegisterRestoredDockedCamera(camera);
		TryApplyPendingCameraState(packet.CameraId, cameraObject);
		NormalizeCameraList();
		if (!packet.IsDocked && remotelyControlled.Contains(packet.CameraId))
		{
			UWE.CoroutineHost.StartCoroutine(RestoreRemoteMovementAfterUndock(packet.CameraId, cameraObject));
		}
		return true;
	}

	internal static bool ShouldApplyPhysicalDockTransition(bool isDocked, bool slotMatchesCamera,
		bool slotReportsDocked) =>
		isDocked ? !slotMatchesCamera || !slotReportsDocked : slotMatchesCamera;

	private void ApplyPendingControls()
	{
		foreach (MapRoomCameraControl pending in bootstrapStateCache.GetPendingControls())
		{
			ProcessControl(pending);
		}
	}

	private IEnumerator RestoreRemoteMovementAfterUndock(NitroxId cameraId, GameObject cameraObject)
	{
		// Destroy is deferred until the end of the frame. Waiting prevents a rapid
		// dock/undock pair from seeing the old component just before it disappears.
		yield return null;
		if ((bool)cameraObject && remotelyControlled.Contains(cameraId) && !cameraObject.GetComponent<MapRoomCameraMovementReplicator>())
		{
			cameraObject.AddComponent<MapRoomCameraMovementReplicator>();
		}
	}

	private static void SetLight(GameObject cameraObject, bool on)
	{
		if (!cameraObject.TryGetComponent<MapRoomCamera>(out var component))
		{
			return;
		}

		// Start restores lightsParent from this serialized field. Keep it canonical even when
		// authoritative state arrives between prefab creation and the component's Start callback.
		component.lightState = on;
		if ((bool)component.lightsParent && component.lightsParent.activeSelf != on)
		{
			component.lightsParent.SetActive(on);
		}
	}

	private static GameObject ResolveCameraObject(NitroxId cameraId, Optional<NitroxId> mapRoomId, int cameraIndex)
	{
		if (NitroxEntity.TryGetObjectFrom(cameraId, out GameObject gameObject) && (bool)gameObject)
		{
			return gameObject.TryGetComponent<MapRoomCamera>(out _) &&
				TryAssignCameraId(gameObject, cameraId)
					? gameObject
					: null;
		}
		if (mapRoomId.HasValue && cameraIndex >= 0 && NitroxEntity.TryGetObjectFrom(mapRoomId.Value, out GameObject gameObject2) && (bool)gameObject2 && gameObject2.TryGetComponent<MapRoomFunctionality>(out var component))
		{
			List<MapRoomCameraDocking> dockingPoints = GetDockingPoints(component);
			if (cameraIndex < dockingPoints.Count)
			{
				MapRoomCamera camera = dockingPoints[cameraIndex].camera;
				if ((bool)camera)
				{
					return TryAssignCameraId(camera.gameObject, cameraId) ? camera.gameObject : null;
				}
			}
		}
		return null;
	}

	private static int GetDockingIndex(MapRoomFunctionality mapRoom, MapRoomCameraDocking dockingPoint)
	{
		List<MapRoomCameraDocking> dockingPoints = GetDockingPoints(mapRoom);
		for (int i = 0; i < dockingPoints.Count; i++)
		{
			if (dockingPoints[i] == dockingPoint)
			{
				return i;
			}
		}
		return -1;
	}

	public static NitroxId GetDeterministicCameraId(NitroxId mapRoomId, Vector3 localDockPosition)
	{
		int value = Mathf.RoundToInt(localDockPosition.x * 10f);
		int value2 = Mathf.RoundToInt(localDockPosition.y * 10f);
		int value3 = Mathf.RoundToInt(localDockPosition.z * 10f);
		byte[] array = new Guid(mapRoomId.ToString()).ToByteArray();
		byte[] bytes = BitConverter.GetBytes(value);
		byte[] bytes2 = BitConverter.GetBytes(value2);
		byte[] bytes3 = BitConverter.GetBytes(value3);
		for (int i = 0; i < 4; i++)
		{
			array[i] ^= bytes[i];
			array[i + 4] ^= bytes2[i];
			array[i + 8] ^= bytes3[i];
		}
		return new NitroxId(array);
	}

	private static Vector3 GetLocalDockPosition(MapRoomFunctionality mapRoom, MapRoomCameraDocking dockingPoint)
	{
		return mapRoom.transform.InverseTransformPoint(dockingPoint.transform.position);
	}

	private bool ApplyCachedDocks(MapRoomFunctionality mapRoom, NitroxId mapRoomId, MapRoomCameraDock focus = null,
		bool recordDiagnostics = true, bool pendingOnly = false)
	{
		bool allApplied = true;
		bool focusApplied = focus == null;
		IReadOnlyList<MapRoomCameraDock> docks = pendingOnly
			? bootstrapStateCache.GetPendingDocks(mapRoomId)
			: bootstrapStateCache.GetDocks(mapRoomId);
		foreach (MapRoomCameraDock pending in docks)
		{
			bool applied = TryApplyDockObject(pending);
			allApplied &= applied;
			if (applied)
			{
				bootstrapStateCache.MarkDockApplied(pending);
			}
			else
			{
				// An earlier marker may belong to an object that was replaced by a base resync.
				// Keep the canonical state pending until it applies to the current room generation.
				bootstrapStateCache.MarkDockPending(pending);
			}
			if (focus != null && pending.DockingIndex == focus.DockingIndex &&
				pending.Revision == focus.Revision && pending.CameraId == focus.CameraId &&
				pending.IsDocked == focus.IsDocked)
			{
				focusApplied = applied;
			}
			if (recordDiagnostics)
			{
				diagnostics.Record(pending.IsDocked ? "dock_replay" : "undock_replay", applied ? "ok" : "pending",
					pending.CameraId, mapRoomId, pending.Revision, pending.DockingIndex,
					applied ? "fresh_room_ready" : "waiting_object");
			}
		}
		return focus == null ? allApplied : focusApplied;
	}

	private IEnumerator ReplayCachedDocksDeferred(MapRoomFunctionality mapRoom, NitroxId mapRoomId)
	{
		try
		{
			while (mapRoom && mapRoom.TryGetNitroxId(out NitroxId currentId) && currentId == mapRoomId)
			{
				if (ApplyCachedDocks(mapRoom, mapRoomId, recordDiagnostics: false, pendingOnly: true))
				{
					diagnostics.Record("dock_replay", "ok", roomId: mapRoomId, reason: "late_camera_ready");
					ApplyPendingControls();
					yield break;
				}
				yield return new WaitForSeconds(0.25f);
			}
		}
		finally
		{
			bootstrapReplayRooms.Remove(mapRoomId);
		}
	}

	public static void EnsureCameraIds(MapRoomFunctionality mapRoom) => EnsureCameraIds(mapRoom, null);

	private static void EnsureCameraIds(MapRoomFunctionality mapRoom, IReadOnlyList<NitroxId?> authoritativeDockIds, IReadOnlyList<MapRoomCameraRecord> cameraRecords = null)
	{
		if (!mapRoom || !mapRoom.TryGetNitroxId(out NitroxId nitroxId))
		{
			return;
		}
		MapRoomCameras cameraManager = NitroxServiceLocator.LocateService<MapRoomCameras>();
		bool cachedDocksApplied = cameraManager.ApplyCachedDocks(mapRoom, nitroxId);
		if (!cachedDocksApplied && cameraManager.bootstrapReplayRooms.Add(nitroxId))
		{
			UWE.CoroutineHost.StartCoroutine(cameraManager.ReplayCachedDocksDeferred(mapRoom, nitroxId));
		}
		List<MapRoomCameraDocking> dockingPoints = GetDockingPoints(mapRoom);
		int num = 0;
		int reconciled = 0;
		int broadcast = 0;
		int removed = 0;
		for (int index = 0; index < dockingPoints.Count; index++)
		{
			MapRoomCameraDocking item = dockingPoints[index];
			MapRoomCamera camera = item.camera;
			bool hasCachedDock = cameraManager.bootstrapStateCache.TryGetDock(nitroxId, index, out MapRoomCameraDock cachedDock);
			bool hasAuthoritativeSlot = hasCachedDock || authoritativeDockIds != null;
			NitroxId? authoritativeCameraId = hasCachedDock
				? cachedDock.IsDocked ? cachedDock.CameraId : null
				: authoritativeDockIds != null && index < authoritativeDockIds.Count ? authoritativeDockIds[index] : null;
			if (hasAuthoritativeSlot && !ShouldRestoreDefaultCamera(authoritativeCameraId))
			{
				if (camera)
				{
					RemoveUnexpectedRestoredCamera(item, camera);
					removed++;
				}
				continue;
			}
			if ((bool)camera && hasAuthoritativeSlot && authoritativeCameraId != null)
			{
				bool identityChanged = !camera.TryGetNitroxId(out NitroxId existingId) ||
					existingId != authoritativeCameraId;
				if (!TryAssignCameraId(camera.gameObject, authoritativeCameraId))
				{
					cameraManager.diagnostics.Record("dock_reconcile", "diverge", authoritativeCameraId,
						nitroxId, slot: index, reason: "canonical_id_collision");
					continue;
				}
				if (identityChanged)
				{
					num++;
				}
			}
			else if ((bool)camera && !hasAuthoritativeSlot)
			{
				NitroxId deterministicCameraId = GetDeterministicCameraId(nitroxId, GetLocalDockPosition(mapRoom, item));
				bool identityChanged = !camera.TryGetNitroxId(out NitroxId provisionalId) ||
					provisionalId != deterministicCameraId;
				if (!TryAssignCameraId(camera.gameObject, deterministicCameraId))
				{
					cameraManager.diagnostics.Record("dock_reconcile", "diverge",
						deterministicCameraId, nitroxId, slot: index, reason: "deterministic_id_collision");
					continue;
				}
				if (identityChanged)
				{
					num++;
				}
			}
			if ((bool)camera && RegisterRestoredDockedCamera(camera))
			{
				reconciled++;
			}
			if ((bool)camera)
			{
				cameraManager.BroadcastDock(item, camera);
				broadcast++;
			}
		}
		if (cameraRecords != null)
		{
			cameraManager.ApplyRestoredCameraRecords(cameraRecords);
		}
		Log.Info(string.Format("[{0}] EnsureCameraIds map room {1}: found {2} dock(s), assigned {3} camera id(s), reconciled {4} camera reference(s), broadcast {5} restored dock(s), removed {6} authoritative empty-slot camera(s)", "MapRoomCameras", nitroxId, dockingPoints.Count, num, reconciled, broadcast, removed));
		NormalizeCameraList();
		cameraManager.TryApplyPendingPreview();
		cameraManager.ApplyPendingControls();
	}

	public static IEnumerator EnsureCameraIdsDeferred(MapRoomFunctionality mapRoom)
	{
		float timeoutAt = Time.time + 15f;
		yield return new WaitUntil(() => !mapRoom || Time.time >= timeoutAt || FreshMapRoomReadyForIds(mapRoom));
		EnsureCameraIds(mapRoom);
	}

	public static IEnumerator EnsureCameraIdsDeferred(MapRoomFunctionality mapRoom, NitroxId? leftDockCameraId, NitroxId? rightDockCameraId, IReadOnlyList<MapRoomCameraRecord> cameraRecords)
	{
		float timeoutAt = Time.time + 15f;
		NitroxId?[] authoritativeDockIds = [leftDockCameraId, rightDockCameraId];
		yield return new WaitUntil(() => !mapRoom || Time.time >= timeoutAt ||
			AuthoritativeMapRoomReadyForIds(mapRoom, authoritativeDockIds));
		EnsureCameraIds(mapRoom, authoritativeDockIds, cameraRecords);
	}

	private void ApplyRestoredCameraRecords(IReadOnlyList<MapRoomCameraRecord> cameraRecords)
	{
		foreach (MapRoomCameraRecord record in cameraRecords)
		{
			RetainAndApplyCameraRecord(record);
		}
	}

	private void RetainAndApplyCameraRecord(MapRoomCameraRecord record, bool preferCameraNumber = false)
	{
		restoreStateCache.Retain(record, preferCameraNumber);
		if (!lightRevisions.TryGetValue(record.CameraId, out long lightRevision) || record.LightRevision >= lightRevision)
		{
			lightRevisions[record.CameraId] = record.LightRevision;
		}
		if (!componentRevisions.TryGetValue(record.CameraId, out long componentRevision) || record.ComponentRevision >= componentRevision)
		{
			componentRevisions[record.CameraId] = record.ComponentRevision;
		}
		if (!TryApplyPendingCameraState(record.CameraId))
		{
			if (deferredCameraState.Add(record.CameraId))
			{
				diagnostics.Record("restore_apply", "pending", record.CameraId, revision: record.ComponentRevision,
					slot: record.CameraNumber, reason: "waiting_object");
			}
		}
	}

	private readonly record struct CameraBatteryInitialization(MapRoomCamera Camera, long Generation);

	internal static bool ShouldRestoreDefaultCamera(NitroxId? authoritativeCameraId) => authoritativeCameraId != null;

	private static void RemoveUnexpectedRestoredCamera(MapRoomCameraDocking dockingPoint, MapRoomCamera camera)
	{
		while (MapRoomCamera.cameras.Remove(camera))
		{
		}
		dockingPoint.camera = null;
		dockingPoint.cameraDocked = false;
		if (camera.TryGetComponent(out NitroxEntity nitroxEntity))
		{
			nitroxEntity.Remove();
		}
		UnityEngine.Object.Destroy(camera.gameObject);
	}

	public static bool EnsureCameraId(MapRoomCameraDocking dockingPoint, MapRoomCamera camera)
	{
		if (!dockingPoint || !camera)
		{
			return false;
		}
		if (camera.TryGetNitroxId(out NitroxId existingId))
		{
			if (!TryAssignCameraId(camera.gameObject, existingId))
			{
				Log.Error($"[MapRoomCameras] Refused to broadcast camera {existingId} because another object owns its canonical registration");
				return false;
			}
		}
		else
		{
			MapRoomFunctionality mapRoomForDock = GetMapRoomForDock(dockingPoint);
			if (!mapRoomForDock || !mapRoomForDock.TryGetNitroxId(out NitroxId mapRoomId))
			{
				return false;
			}
			Vector3 localDockPosition = GetLocalDockPosition(mapRoomForDock, dockingPoint);
			NitroxId deterministicCameraId = GetDeterministicCameraId(mapRoomId, localDockPosition);
			if (!TryAssignCameraId(camera.gameObject, deterministicCameraId))
			{
				Log.Error($"[MapRoomCameras] Refused to overwrite canonical camera id {deterministicCameraId} while reconciling map room {mapRoomId}");
				return false;
			}
			Log.Info(string.Format("[{0}] assigned camera id {1} (map room {2}, localPos {3:F2},{4:F2},{5:F2})", "MapRoomCameras", deterministicCameraId, mapRoomId, localDockPosition.x, localDockPosition.y, localDockPosition.z));
		}
		RegisterRestoredDockedCamera(camera);
		NormalizeCameraList();
		return true;
	}

	private static bool RegisterRestoredDockedCamera(MapRoomCamera camera)
	{
		if (!camera)
		{
			return false;
		}
		if (MapRoomCamera.cameras.Contains(camera))
		{
			NormalizeCameraList();
			return true;
		}

		int instanceId = camera.GetInstanceID();
		if (camerasAwaitingVanillaRegistration.Add(instanceId))
		{
			// MapRoomCamera.Start adds itself only after its asynchronous default-battery work.
			// Adding it here would let vanilla add the same instance a second time later.
			UWE.CoroutineHost.StartCoroutine(NormalizeAfterVanillaCameraRegistration(camera, instanceId));
		}
		return false;
	}

	private static IEnumerator NormalizeAfterVanillaCameraRegistration(MapRoomCamera camera, int instanceId)
	{
		try
		{
			yield return new WaitUntil(() => !camera || MapRoomCamera.cameras.Contains(camera));
			if (camera)
			{
				NormalizeCameraList();
			}
		}
		finally
		{
			camerasAwaitingVanillaRegistration.Remove(instanceId);
		}
	}

	private static bool TryAssignCameraId(GameObject cameraObject, NitroxId cameraId)
	{
		bool idRegistered = NitroxEntity.TryGetObjectFrom(cameraId, out GameObject registeredObject);
		bool uniqueIdRegistered = UniqueIdentifier.identifiers.TryGetValue(cameraId.ToString(),
			out UniqueIdentifier registeredIdentifier);
		if (!CanAssignCameraIdentity(idRegistered, registeredObject == cameraObject,
			uniqueIdRegistered, registeredIdentifier && registeredIdentifier.gameObject == cameraObject))
		{
			return false;
		}
		NitroxEntity.SetNewId(cameraObject, cameraId);
		return true;
	}

	internal static bool CanAssignCameraIdentity(bool idRegistered, bool registeredObjectIsCandidate,
		bool uniqueIdRegistered, bool registeredIdentifierIsCandidate) =>
		(!idRegistered || registeredObjectIsCandidate) &&
		(!uniqueIdRegistered || registeredIdentifierIsCandidate);

	public static void NormalizeCameraList()
	{
		HashSet<MapRoomCamera> seenInstances = new HashSet<MapRoomCamera>();
		HashSet<NitroxId> seenIds = new HashSet<NitroxId>();
		int removed = 0;
		for (int i = 0; i < MapRoomCamera.cameras.Count; i++)
		{
			MapRoomCamera camera = MapRoomCamera.cameras[i];
			if (!camera || !seenInstances.Add(camera))
			{
				MapRoomCamera.cameras.RemoveAt(i);
				removed++;
				i--;
				continue;
			}
			if (camera.TryGetNitroxId(out NitroxId cameraId) && !seenIds.Add(cameraId))
			{
				MapRoomCamera.cameras.RemoveAt(i);
				removed++;
				i--;
			}
		}
		if (removed > 0)
		{
			Log.Warn($"[MapRoomCameras] Removed {removed} stale or duplicate camera reference(s); {MapRoomCamera.cameras.Count} remain");
		}
	}

	public static void DestroyStaleLocalCamera(NitroxId cameraId)
	{
		if (NitroxEntity.TryGetObjectFrom(cameraId, out GameObject gameObject) && (bool)gameObject &&
			gameObject.TryGetComponent<MapRoomCamera>(out var _))
		{
			// Unity destroys the old object at the end of the frame. Detach its identity now so
			// authoritative state received while the replacement prefab yields cannot drain into it.
			if (gameObject.TryGetComponent(out NitroxEntity nitroxEntity))
			{
				nitroxEntity.Remove();
			}
			UnityEngine.Object.Destroy(gameObject);
		}
	}

	private static bool FreshMapRoomReadyForIds(MapRoomFunctionality mapRoom)
	{
		if (!mapRoom || !mapRoom.TryGetNitroxId(out NitroxId _))
		{
			return false;
		}
		List<MapRoomCameraDocking> dockingPoints = GetDockingPoints(mapRoom);
		List<bool> cameraPresence = new(dockingPoints.Count);
		foreach (MapRoomCameraDocking dockingPoint in dockingPoints)
		{
			cameraPresence.Add(dockingPoint.camera);
		}
		return ExpectedDockCamerasReady(cameraPresence);
	}

	internal static bool ExpectedDockCamerasReady(IReadOnlyList<bool> cameraPresence)
	{
		if (cameraPresence.Count < 2)
		{
			return false;
		}
		foreach (bool cameraPresent in cameraPresence)
		{
			if (!cameraPresent)
			{
				return false;
			}
		}
		return true;
	}

	private static bool AuthoritativeMapRoomReadyForIds(MapRoomFunctionality mapRoom,
		IReadOnlyList<NitroxId?> authoritativeDockIds)
	{
		if (!mapRoom || !mapRoom.TryGetNitroxId(out NitroxId _))
		{
			return false;
		}
		List<MapRoomCameraDocking> dockingPoints = GetDockingPoints(mapRoom);
		List<bool> cameraPresence = new(dockingPoints.Count);
		List<bool> dockingPointsDeserialized = new(dockingPoints.Count);
		foreach (MapRoomCameraDocking dockingPoint in dockingPoints)
		{
			cameraPresence.Add(dockingPoint.camera);
			dockingPointsDeserialized.Add(dockingPoint.deserialized);
		}
		return ExpectedAuthoritativeDockCamerasReady(cameraPresence, dockingPointsDeserialized,
			authoritativeDockIds);
	}

	internal static bool ExpectedAuthoritativeDockCamerasReady(IReadOnlyList<bool> cameraPresence,
		IReadOnlyList<bool> dockingPointsDeserialized, IReadOnlyList<NitroxId?> authoritativeDockIds)
	{
		if (cameraPresence.Count < authoritativeDockIds.Count ||
			dockingPointsDeserialized.Count < authoritativeDockIds.Count)
		{
			return false;
		}
		for (int index = 0; index < authoritativeDockIds.Count; index++)
		{
			if (!cameraPresence[index] &&
				(authoritativeDockIds[index] != null || !dockingPointsDeserialized[index]))
			{
				return false;
			}
		}
		return true;
	}

	private static List<MapRoomCameraDocking> GetDockingPoints(MapRoomFunctionality mapRoom)
	{
		List<MapRoomCameraDocking> list = new List<MapRoomCameraDocking>();
		if (!mapRoom)
		{
			return list;
		}
		Base componentInParent = mapRoom.GetComponentInParent<Base>();
		if ((bool)componentInParent)
		{
			MapRoomCameraDocking[] componentsInChildren = componentInParent.GetComponentsInChildren<MapRoomCameraDocking>(includeInactive: true);
			foreach (MapRoomCameraDocking mapRoomCameraDocking in componentsInChildren)
			{
				if ((bool)mapRoomCameraDocking && GetMapRoomForDock(mapRoomCameraDocking) == mapRoom)
				{
					list.Add(mapRoomCameraDocking);
				}
			}
		}
		if (list.Count == 0)
		{
			list.AddRange(mapRoom.GetComponentsInChildren<MapRoomCameraDocking>(includeInactive: true));
		}
		list.Sort((MapRoomCameraDocking a, MapRoomCameraDocking b) => CompareWorldPosition(a.transform.position, b.transform.position));
		return list;
	}

	private static MapRoomFunctionality GetMapRoomForDock(MapRoomCameraDocking dockingPoint)
	{
		if (!dockingPoint)
		{
			return null;
		}
		Base componentInParent = dockingPoint.GetComponentInParent<Base>();
		if ((bool)componentInParent)
		{
			MapRoomFunctionality mapRoomFunctionality = null;
			float num = float.MaxValue;
			Vector3 position = dockingPoint.transform.position;
			MapRoomFunctionality[] componentsInChildren = componentInParent.GetComponentsInChildren<MapRoomFunctionality>(includeInactive: true);
			foreach (MapRoomFunctionality mapRoomFunctionality2 in componentsInChildren)
			{
				float sqrMagnitude = (mapRoomFunctionality2.transform.position - position).sqrMagnitude;
				if (sqrMagnitude < num)
				{
					num = sqrMagnitude;
					mapRoomFunctionality = mapRoomFunctionality2;
				}
			}
			if ((bool)mapRoomFunctionality)
			{
				return mapRoomFunctionality;
			}
		}
		return dockingPoint.GetComponentInParent<MapRoomFunctionality>();
	}

	private static byte[] CapturePreviewJpeg(RenderTexture source)
	{
		const int size = MapRoomCameraPreviewImage.MAX_DIMENSION;
		RenderTexture temporary = RenderTexture.GetTemporary(size, size, 0, RenderTextureFormat.ARGB32,
			RenderTextureReadWrite.Default);
		RenderTexture previous = RenderTexture.active;
		Texture2D readback = null;
		try
		{
			Graphics.Blit(source, temporary);
			RenderTexture.active = temporary;
			readback = new Texture2D(size, size, TextureFormat.RGB24, mipChain: false);
			readback.ReadPixels(new Rect(0f, 0f, size, size), 0, 0, recalculateMipMaps: false);
			readback.Apply(updateMipmaps: false, makeNoLongerReadable: false);
			return ImageConversion.EncodeToJPG(readback, 55);
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
		if (NitroxEntity.TryGetObjectFrom(cameraId, out GameObject gameObject) && gameObject &&
			gameObject.TryGetComponent(out MapRoomCamera camera))
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

	private static int CompareWorldPosition(Vector3 a, Vector3 b)
	{
		if (Mathf.Abs(a.x - b.x) > 0.01f)
		{
			return a.x.CompareTo(b.x);
		}
		if (Mathf.Abs(a.y - b.y) > 0.01f)
		{
			return a.y.CompareTo(b.y);
		}
		return a.z.CompareTo(b.z);
	}
}

