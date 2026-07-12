using System;
using System.Collections;
using System.Collections.Generic;
using Nitrox.Model.Core;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Logger;
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

	private readonly Dictionary<NitroxId, bool> lastBroadcastLightState = new Dictionary<NitroxId, bool>();

	private readonly HashSet<NitroxId> locallyControlled = new HashSet<NitroxId>();
	private readonly HashSet<NitroxId> pendingControl = new HashSet<NitroxId>();
	private readonly HashSet<NitroxId> remotelyControlled = new HashSet<NitroxId>();
	private readonly Dictionary<NitroxId, long> dockingRevisions = new Dictionary<NitroxId, long>();
	private readonly Dictionary<NitroxId, long> lightRevisions = new Dictionary<NitroxId, long>();
	private readonly Dictionary<NitroxId, (float Energy, float Health)> lastComponents = new();
	private readonly Dictionary<NitroxId, long> componentRevisions = new();
	private readonly Dictionary<NitroxId, float> pendingCameraEnergy = new();
	private readonly HashSet<NitroxId> initializingCameraBatteries = new();

	public MapRoomCameras(IPacketSender packetSender, IMultiplayerSession multiplayerSession, LiveMixinManager liveMixinManager, SimulationOwnership simulationOwnership)
	{
		this.packetSender = packetSender;
		this.multiplayerSession = multiplayerSession;
		this.liveMixinManager = liveMixinManager;
		this.simulationOwnership = simulationOwnership;
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
			Optional<NitroxId> mapRoomId = Optional.Empty;
			int cameraIndex = -1;
			MapRoomFunctionality mapRoomForDock = GetMapRoomForDock(camera.dockingPoint);
			if ((bool)mapRoomForDock && mapRoomForDock.TryGetNitroxId(out NitroxId nitroxId))
			{
				mapRoomId = Optional.Of(nitroxId);
				cameraIndex = GetDockingIndex(mapRoomForDock, camera.dockingPoint);
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
			MovementBroadcaster.UnregisterWatched(nitroxId2);
			lastBroadcastLightState.Remove(nitroxId2);
			packetSender.Send(new MapRoomCameraControl(nitroxId2, Optional.Empty, -1, isControlling: false, lightOn: false));
		}
	}

	public void ProcessControl(MapRoomCameraControl packet)
	{
		if (!packet.IsServerResponse)
		{
			return;
		}
		bool isLocalController = packet.ControllerSessionId == multiplayerSession.Reservation.SessionId;
		if (!packet.Granted)
		{
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
			GameObject gameObject = ResolveCameraObject(packet.CameraId, packet.MapRoomId, packet.CameraIndex);
			if (!gameObject)
			{
				Log.Warn(string.Format("[{0}] Couldn't find a camera drone to replicate for {1}", "MapRoomCameras", packet));
				return;
			}
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
				return;
			}
			if (!gameObject.GetComponent<MapRoomCameraMovementReplicator>())
			{
				gameObject.AddComponent<MapRoomCameraMovementReplicator>();
			}
			remotelyControlled.Add(packet.CameraId);
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
			MovementBroadcaster.UnregisterWatched(packet.CameraId);
		}
	}

	public bool CanSelectForControl(MapRoomCamera camera)
	{
		if (!camera || !camera.TryGetNitroxId(out NitroxId cameraId))
		{
			return true;
		}
		return CanSelectForControl(pendingControl.Contains(cameraId), locallyControlled.Contains(cameraId), remotelyControlled.Contains(cameraId), camera.IsControlled());
	}

	public static bool CanSelectForControl(bool pending, bool locallyControlled, bool remotelyControlled, bool activelyControlled) =>
		locallyControlled || (!remotelyControlled && (!pending || activelyControlled));

	public void BroadcastLightIfChanged(MapRoomCamera camera)
	{
		if ((bool)camera && (bool)camera.lightsParent && camera.TryGetNitroxId(out NitroxId nitroxId))
		{
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
		if (packet.IsServerResponse && packet.Granted && (!lightRevisions.TryGetValue(packet.CameraId, out long revision) || packet.Revision >= revision) && NitroxEntity.TryGetObjectFrom(packet.CameraId, out GameObject gameObject) && (bool)gameObject)
		{
			lightRevisions[packet.CameraId] = packet.Revision;
			SetLight(gameObject, packet.On);
		}
	}

	public void BroadcastComponentStateIfChanged(MapRoomCamera camera)
	{
		if (!camera || !camera.TryGetNitroxId(out NitroxId id) || (!locallyControlled.Contains(id) && !simulationOwnership.HasAnyLockType(id) && !CanSimulateDockedCamera(camera))) return;
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
		if (NitroxEntity.TryGetObjectFrom(packet.CameraId, out GameObject gameObject) && gameObject.TryGetComponent(out MapRoomCamera camera))
		{
			if (camera.energyMixin.battery != null)
			{
				camera.energyMixin.battery.charge = packet.Energy;
			}
			else
			{
				pendingCameraEnergy[packet.CameraId] = packet.Energy;
				if (initializingCameraBatteries.Add(packet.CameraId))
				{
					UWE.CoroutineHost.StartCoroutine(InitializeCameraBattery(camera, packet.CameraId));
				}
			}
			liveMixinManager.SyncRemoteHealth(camera.liveMixin, packet.Health);
		}
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

	private IEnumerator InitializeCameraBattery(MapRoomCamera camera, NitroxId cameraId)
	{
		if ((bool)camera && camera.energyMixin.battery == null)
		{
			BatteryChildEntityHelper.PopulateInstalledBattery(camera.energyMixin, [], cameraId);
			float timeoutAt = Time.time + 10f;
			yield return new WaitUntil(() => !camera || camera.energyMixin.battery != null || Time.time >= timeoutAt);
		}

		if ((bool)camera && camera.energyMixin.battery != null && pendingCameraEnergy.TryGetValue(cameraId, out float energy))
		{
			camera.energyMixin.battery.charge = energy;
			Log.Info($"[MapRoomCameras] Initialized restored camera {cameraId} battery with {energy:F2} energy");
		}
		else
		{
			Log.Warn($"[MapRoomCameras] Could not initialize restored camera {cameraId} battery");
		}
		pendingCameraEnergy.Remove(cameraId);
		initializingCameraBatteries.Remove(cameraId);
	}

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
				packetSender.Send(new MapRoomCameraDock(cameraId, mapRoomId, GetDockingIndex(mapRoom, dockingPoint), isDocked: false));
			}
		}
	}

	public void ProcessDock(MapRoomCameraDock packet)
	{
		if (!packet.IsServerResponse || !packet.Granted || (dockingRevisions.TryGetValue(packet.MapRoomId, out long revision) && packet.Revision < revision))
		{
			return;
		}
		dockingRevisions[packet.MapRoomId] = packet.Revision;
		lightRevisions[packet.CameraId] = packet.LightRevision;
		if (packet.CameraNumber > 0 && NitroxEntity.TryGetObjectFrom(packet.CameraId, out GameObject numberedObject) && numberedObject.TryGetComponent(out MapRoomCamera numberedCamera))
		{
			numberedCamera.cameraNumber = packet.CameraNumber;
			numberedCamera.UpdatePingLabel();
		}
		if (NitroxEntity.TryGetObjectFrom(packet.CameraId, out GameObject lightObject))
		{
			SetLight(lightObject, packet.LightOn);
		}
		ProcessComponentState(new MapRoomCameraComponentState(packet.CameraId, packet.Energy, packet.Health, packet.ComponentRevision, true, true));
		if (packet.IsDocked)
		{
			pendingControl.Remove(packet.CameraId);
			locallyControlled.Remove(packet.CameraId);
			MovementBroadcaster.UnregisterWatched(packet.CameraId);
		}
		if (!NitroxEntity.TryGetObjectFrom(packet.CameraId, out GameObject gameObject) || !gameObject || !gameObject.TryGetComponent<MapRoomCamera>(out var component) || !NitroxEntity.TryGetObjectFrom(packet.MapRoomId, out GameObject gameObject2) || !gameObject2 || !gameObject2.TryGetComponent<MapRoomFunctionality>(out var component2))
		{
			return;
		}
		List<MapRoomCameraDocking> dockingPoints = GetDockingPoints(component2);
		if (packet.DockingIndex < 0 || packet.DockingIndex >= dockingPoints.Count)
		{
			return;
		}
		if (packet.IsDocked && gameObject.TryGetComponent<MapRoomCameraMovementReplicator>(out var component3))
		{
			UnityEngine.Object.Destroy(component3);
		}
		using (PacketSuppressor<MapRoomCameraDock>.Suppress())
		{
			if (packet.IsDocked)
			{
				dockingPoints[packet.DockingIndex].DockCamera(component);
			}
			else if (dockingPoints[packet.DockingIndex].camera == component)
			{
				dockingPoints[packet.DockingIndex].UndockCamera();
			}
		}
	}

	private static void SetLight(GameObject cameraObject, bool on)
	{
		if (cameraObject.TryGetComponent<MapRoomCamera>(out var component) && (bool)component.lightsParent && component.lightsParent.activeSelf != on)
		{
			component.lightsParent.SetActive(on);
		}
	}

	private static GameObject ResolveCameraObject(NitroxId cameraId, Optional<NitroxId> mapRoomId, int cameraIndex)
	{
		if (NitroxEntity.TryGetObjectFrom(cameraId, out GameObject gameObject) && (bool)gameObject)
		{
			return gameObject;
		}
		if (mapRoomId.HasValue && cameraIndex >= 0 && NitroxEntity.TryGetObjectFrom(mapRoomId.Value, out GameObject gameObject2) && (bool)gameObject2 && gameObject2.TryGetComponent<MapRoomFunctionality>(out var component))
		{
			List<MapRoomCameraDocking> dockingPoints = GetDockingPoints(component);
			if (cameraIndex < dockingPoints.Count)
			{
				MapRoomCamera camera = dockingPoints[cameraIndex].camera;
				if ((bool)camera)
				{
					NitroxEntity.SetNewId(camera.gameObject, cameraId);
					return camera.gameObject;
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

	public static void EnsureCameraIds(MapRoomFunctionality mapRoom)
	{
		if (!mapRoom || !mapRoom.TryGetNitroxId(out NitroxId nitroxId))
		{
			return;
		}
		List<MapRoomCameraDocking> dockingPoints = GetDockingPoints(mapRoom);
		int num = 0;
		int reconciled = 0;
		int broadcast = 0;
		MapRoomCameras cameraManager = NitroxServiceLocator.LocateService<MapRoomCameras>();
		foreach (MapRoomCameraDocking item in dockingPoints)
		{
			MapRoomCamera camera = item.camera;
			if ((bool)camera && !camera.TryGetNitroxId(out NitroxId _))
			{
				NitroxEntity.SetNewId(camera.gameObject, GetDeterministicCameraId(nitroxId, GetLocalDockPosition(mapRoom, item)));
				num++;
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
		Log.Info(string.Format("[{0}] EnsureCameraIds map room {1}: found {2} dock(s), assigned {3} camera id(s), reconciled {4} camera reference(s), broadcast {5} restored dock(s)", "MapRoomCameras", nitroxId, dockingPoints.Count, num, reconciled, broadcast));
		NormalizeCameraList();
	}

	public static IEnumerator EnsureCameraIdsDeferred(MapRoomFunctionality mapRoom)
	{
		float timeoutAt = Time.time + 15f;
		yield return new WaitUntil(() => !mapRoom || Time.time >= timeoutAt || MapRoomReadyForIds(mapRoom));
		EnsureCameraIds(mapRoom);
	}

	public static void EnsureCameraId(MapRoomCameraDocking dockingPoint, MapRoomCamera camera)
	{
		if ((bool)dockingPoint && (bool)camera && !camera.TryGetNitroxId(out NitroxId _))
		{
			MapRoomFunctionality mapRoomForDock = GetMapRoomForDock(dockingPoint);
			if ((bool)mapRoomForDock && mapRoomForDock.TryGetNitroxId(out NitroxId nitroxId2))
			{
				Vector3 localDockPosition = GetLocalDockPosition(mapRoomForDock, dockingPoint);
				NitroxId deterministicCameraId = GetDeterministicCameraId(nitroxId2, localDockPosition);
				NitroxEntity.SetNewId(camera.gameObject, deterministicCameraId);
				Log.Info(string.Format("[{0}] assigned camera id {1} (map room {2}, localPos {3:F2},{4:F2},{5:F2})", "MapRoomCameras", deterministicCameraId, nitroxId2, localDockPosition.x, localDockPosition.y, localDockPosition.z));
			}
		}
		if ((bool)camera)
		{
			RegisterRestoredDockedCamera(camera);
		}
		NormalizeCameraList();
	}

	private static bool RegisterRestoredDockedCamera(MapRoomCamera camera)
	{
		if (!camera)
		{
			return false;
		}

		bool changed = false;
		if (camera.TryGetNitroxId(out NitroxId cameraId))
		{
			for (int i = MapRoomCamera.cameras.Count - 1; i >= 0; i--)
			{
				MapRoomCamera existing = MapRoomCamera.cameras[i];
				if (existing == camera || ((bool)existing && existing.TryGetNitroxId(out NitroxId existingId) && existingId == cameraId))
				{
					MapRoomCamera.cameras.RemoveAt(i);
					changed = true;
				}
			}
		}
		else
		{
			while (MapRoomCamera.cameras.Remove(camera))
			{
				changed = true;
			}
		}

		MapRoomCamera.cameras.Add(camera);
		return changed;
	}

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
		if (NitroxEntity.TryGetObjectFrom(cameraId, out GameObject gameObject) && (bool)gameObject && gameObject.TryGetComponent<MapRoomCamera>(out var _))
		{
			UnityEngine.Object.Destroy(gameObject);
		}
	}

	private static bool MapRoomReadyForIds(MapRoomFunctionality mapRoom)
	{
		if (!mapRoom || !mapRoom.TryGetNitroxId(out NitroxId _))
		{
			return false;
		}
		foreach (MapRoomCameraDocking dockingPoint in GetDockingPoints(mapRoom))
		{
			if ((bool)dockingPoint.camera)
			{
				return true;
			}
		}
		return false;
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


