using System.Threading.Tasks;
using System.Linq;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Packets.Core;
using Nitrox.Model.Subnautica.Packets;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Bases;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Server.Subnautica.Models.GameLogic;
using Nitrox.Server.Subnautica.Models.GameLogic.Entities;
using Nitrox.Server.Subnautica.Models.Packets.Core;

namespace Nitrox.Server.Subnautica.Models.Packets.Processors;

internal sealed class MapRoomCameraDockProcessor(EntityRegistry entityRegistry, WorldEntityManager worldEntityManager,
	SimulationOwnershipData simulationOwnershipData, MapRoomCameraControlReleaseFactory cameraControlReleaseFactory,
	MapRoomLifecycle roomLifecycle, MapRoomCameraControlLifecycle controlLifecycle, ScannerRoomDiagnostics diagnostics,
	ILogger<MapRoomCameraDockProcessor> logger) : IAuthPacketProcessor<MapRoomCameraDock>, IAuthPacketProcessor, IPacketProcessor, IPacketProcessor<AuthProcessorContext, MapRoomCameraDock>
{
	private const string MAP_ROOM_CAMERA_CLASS_ID = "733fd479-0760-4bc2-a03e-281cbf02bfa4";
	private readonly EntityRegistry entityRegistry = entityRegistry;
	private readonly WorldEntityManager worldEntityManager = worldEntityManager;
	private readonly SimulationOwnershipData simulationOwnershipData = simulationOwnershipData;
	private readonly MapRoomCameraControlReleaseFactory cameraControlReleaseFactory = cameraControlReleaseFactory;
	private readonly MapRoomLifecycle roomLifecycle = roomLifecycle;
	private readonly MapRoomCameraControlLifecycle controlLifecycle = controlLifecycle;
	private readonly ScannerRoomDiagnostics diagnostics = diagnostics;
	private readonly ILogger<MapRoomCameraDockProcessor> logger = logger;
	private readonly object dockingLock = new();

	public async Task Process(AuthProcessorContext context, MapRoomCameraDock packet)
	{
		if (packet.IsServerResponse || packet.DockingIndex is < 0 or > 1 ||
			!entityRegistry.TryGetEntityById(packet.MapRoomId, out MapRoomEntity mapRoom))
		{
			diagnostics.RecordRejected(packet.IsDocked ? "dock" : "undock",
				entityRegistry.TryGetEntityById(packet.MapRoomId, out MapRoomEntity diagnosticRoom) ? diagnosticRoom : null,
				packet.CameraId, context.Sender.SessionId, packet.DockingIndex,
				packet.IsServerResponse ? "server_response" : packet.DockingIndex is < 0 or > 1 ? "invalid_slot" : "unknown_room");
			await context.ReplyAsync(new MapRoomCameraDock(packet.CameraId, packet.MapRoomId, packet.DockingIndex, 0, true, false, packet.IsDocked));
			return;
		}
		using IDisposable roomGate = await roomLifecycle.EnterAsync(packet.MapRoomId);
		if (!entityRegistry.TryGetEntityById(packet.MapRoomId, out MapRoomEntity registeredMapRoom) ||
			!ReferenceEquals(mapRoom, registeredMapRoom))
		{
			diagnostics.RecordRejected(packet.IsDocked ? "dock" : "undock", null, packet.CameraId,
				context.Sender.SessionId, packet.DockingIndex, "room_removed");
			await context.ReplyAsync(new MapRoomCameraDock(packet.CameraId, packet.MapRoomId, packet.DockingIndex,
				0, true, false, packet.IsDocked));
			return;
		}

		bool entityExists = entityRegistry.TryGetEntityById(packet.CameraId, out Entity existingEntity);
		bool validWorldCamera = existingEntity is WorldEntity camera && new NitroxTechType("MapRoomCamera").Equals(camera.TechType);
		bool registeredCamera;
		bool canBootstrapRestoredCamera;
		bool senderOwnsRoom = simulationOwnershipData.GetPlayerForLock(mapRoom.Id) == context.Sender;
		lock (mapRoom)
		{
			registeredCamera = mapRoom.GetCameraRecord(packet.CameraId) != null;
			canBootstrapRestoredCamera = CanBootstrapRestoredCamera(packet.IsDocked,
				senderOwnsRoom,
				mapRoom.GetDockedCamera(packet.DockingIndex) == null,
				mapRoom.CameraRegistry.Count);
		}
		if (!IsKnownCamera(validWorldCamera, registeredCamera) && !canBootstrapRestoredCamera)
		{
			diagnostics.RecordRejected(packet.IsDocked ? "dock" : "undock", mapRoom, packet.CameraId, context.Sender.SessionId, packet.DockingIndex, "unknown_camera");
			await context.ReplyAsync(new MapRoomCameraDock(packet.CameraId, packet.MapRoomId, packet.DockingIndex, 0, true, false, packet.IsDocked));
			return;
		}
		using IDisposable lifecycleGate = await controlLifecycle.EnterAsync(packet.CameraId);
		entityExists = entityRegistry.TryGetEntityById(packet.CameraId, out existingEntity);
		validWorldCamera = existingEntity is WorldEntity gatedCamera && new NitroxTechType("MapRoomCamera").Equals(gatedCamera.TechType);
		senderOwnsRoom = simulationOwnershipData.GetPlayerForLock(mapRoom.Id) == context.Sender;
		lock (mapRoom)
		{
			registeredCamera = mapRoom.GetCameraRecord(packet.CameraId) != null;
			canBootstrapRestoredCamera = CanBootstrapRestoredCamera(packet.IsDocked, senderOwnsRoom,
				mapRoom.GetDockedCamera(packet.DockingIndex) == null, mapRoom.CameraRegistry.Count);
		}
		if (!IsKnownCamera(validWorldCamera, registeredCamera) && !canBootstrapRestoredCamera)
		{
			diagnostics.RecordRejected(packet.IsDocked ? "dock" : "undock", mapRoom, packet.CameraId,
				context.Sender.SessionId, packet.DockingIndex, "association_changed");
			await context.ReplyAsync(new MapRoomCameraDock(packet.CameraId, packet.MapRoomId, packet.DockingIndex,
				mapRoom.DockingRevision, true, false, packet.IsDocked));
			return;
		}

		MapRoomCameraDock response;
		bool stateChanged = false;
		bool senderAuthorized = false;
		string diagnosticReason = "slot_conflict";
		lock (dockingLock)
		{
			lock (mapRoom)
			{
				NitroxId? occupyingCamera = mapRoom.GetDockedCamera(packet.DockingIndex);
				bool alreadyCanonical = IsRequestedStateCanonical(packet.IsDocked, packet.CameraId, occupyingCamera,
					mapRoom.IsCameraDocked(packet.CameraId));
				bool senderOwnsCamera = simulationOwnershipData.GetPlayerForLock(packet.CameraId) == context.Sender;
				senderAuthorized = senderOwnsCamera || simulationOwnershipData.GetPlayerForLock(mapRoom.Id) == context.Sender;
				bool granted = alreadyCanonical;
				if (!alreadyCanonical && !senderAuthorized)
				{
					diagnosticReason = "non_owner";
				}
				else if (!alreadyCanonical && !packet.IsDocked)
				{
					granted = mapRoom.TryClearDockedCamera(packet.DockingIndex, packet.CameraId);
					stateChanged = granted;
				}
				else if (!alreadyCanonical)
				{
					bool localSlotAvailable = (occupyingCamera == null || occupyingCamera == packet.CameraId) && (!mapRoom.IsCameraDocked(packet.CameraId) || occupyingCamera == packet.CameraId);
					granted = localSlotAvailable && TryTransferRegistration(mapRoom, packet.CameraId);
					if (granted && occupyingCamera == null)
					{
						mapRoom.SetDockedCamera(packet.DockingIndex, packet.CameraId);
						stateChanged = true;
					}
				}
				MapRoomCameraRecord? existingRecord = granted ? mapRoom.GetCameraRecord(packet.CameraId) : null;
				int cameraNumber = granted
					? existingRecord?.CameraNumber ?? (stateChanged ? mapRoom.GetOrAssignCameraNumber(packet.CameraId, packet.DockingIndex + 1) : 0)
					: 0;
				MapRoomCameraRecord? record = granted ? mapRoom.GetCameraRecord(packet.CameraId) : null;
				response = new MapRoomCameraDock(packet.CameraId, packet.MapRoomId, packet.DockingIndex, mapRoom.DockingRevision, true, granted, packet.IsDocked, cameraNumber, record?.LightOn ?? false, record?.LightRevision ?? 0, record?.Energy ?? MapRoomCameraRecord.MAX_ENERGY, record?.Health ?? MapRoomCameraRecord.MAX_HEALTH, record?.ComponentRevision ?? 0);
				diagnosticReason = granted ? alreadyCanonical ? "idempotent" : response.Revision == 0 ? "bootstrap" : "changed" : diagnosticReason;
				if (granted)
				{
					diagnostics.RecordAccepted(packet.IsDocked ? "dock" : "undock", mapRoom, packet.CameraId,
						context.Sender.SessionId, packet.DockingIndex, diagnosticReason);
				}
				else
				{
					diagnostics.RecordRejected(packet.IsDocked ? "dock" : "undock", mapRoom, packet.CameraId,
						context.Sender.SessionId, packet.DockingIndex, diagnosticReason);
				}
			}
		}

		if (response.Granted)
		{
			if (senderAuthorized && ShouldPersistLooseCamera(packet.IsDocked, entityExists, packet.CameraTransform != null))
			{
				GlobalRootEntity looseCamera = new(packet.CameraTransform!, GlobalRootEntity.GLOBAL_ROOT_LEVEL, MAP_ROOM_CAMERA_CLASS_ID, true,
					packet.CameraId, new NitroxTechType("MapRoomCamera"), null, null, []);
				entityRegistry.AddOrUpdate(looseCamera);
				worldEntityManager.TrackEntityInTheWorld(looseCamera);
				logger.ZLogInformation($"Persisted restored camera {packet.CameraId} as a loose world entity after its first undock");
			}
			MapRoomCameraControl? controlRelease = null;
			bool preserveControlLock = false;
			if (stateChanged && packet.IsDocked && simulationOwnershipData.TryGetLock(packet.CameraId, out SimulationOwnershipData.PlayerLock playerLock))
			{
				preserveControlLock = ShouldPreserveControlLock(playerLock.Player == context.Sender, playerLock.LockType);
			}
			if (stateChanged && packet.IsDocked && !preserveControlLock &&
				simulationOwnershipData.RevokeOwnerOfId(packet.CameraId, out SimulationOwnershipData.PlayerLock revokedLock) &&
				cameraControlReleaseFactory.TryCreate(packet.CameraId, revokedLock, "dock", out MapRoomCameraControl release))
			{
				controlRelease = release;
			}
			else if (stateChanged && packet.IsDocked && !preserveControlLock)
			{
				simulationOwnershipData.RevokeOwnerOfId(packet.CameraId);
			}
			if (controlRelease != null)
			{
				await context.SendToAllAsync(controlRelease);
			}
			await context.SendToAllAsync(response);
		}
		else
		{
			await context.ReplyAsync(response);
		}
	}

	internal static bool IsKnownCamera(bool validWorldCamera, bool registeredCamera) => validWorldCamera || registeredCamera;
	internal static bool CanBootstrapRestoredCamera(bool isDocked, bool senderOwnsRoom, bool slotAvailable, int registeredCameraCount) =>
		isDocked && senderOwnsRoom && slotAvailable && registeredCameraCount is >= 0 and < 2;
	internal static bool ShouldPreserveControlLock(bool senderOwnsLock, SimulationLockType lockType) =>
		senderOwnsLock && lockType == SimulationLockType.EXCLUSIVE;
	internal static bool ShouldPersistLooseCamera(bool isDocked, bool entityExists, bool hasTransform) =>
		!isDocked && !entityExists && hasTransform;
	internal static bool IsRequestedStateCanonical(bool isDocked, NitroxId cameraId, NitroxId? slotCameraId, bool cameraDocked) =>
		isDocked ? slotCameraId == cameraId : slotCameraId == null && !cameraDocked;

	private bool TryTransferRegistration(MapRoomEntity targetRoom, Nitrox.Model.DataStructures.NitroxId cameraId)
	{
		MapRoomEntity? sourceRoom = null;
		MapRoomCameraRecord? sourceRecord = null;
		foreach (MapRoomEntity room in entityRegistry.GetEntities<MapRoomEntity>())
		{
			if (room == targetRoom)
			{
				continue;
			}
			lock (room)
			{
				MapRoomCameraRecord? record = room.GetCameraRecord(cameraId);
				if (room.IsCameraDocked(cameraId) || (record != null && sourceRecord != null))
				{
					return false;
				}
				if (record != null)
				{
					sourceRoom = room;
					sourceRecord = record;
				}
			}
		}
		if (sourceRoom == null || sourceRecord == null)
		{
			return true;
		}
		lock (sourceRoom)
		{
			if (sourceRoom.IsCameraDocked(cameraId) || !sourceRoom.CameraRegistry.Remove(sourceRecord))
			{
				return false;
			}
		}
		if (targetRoom.CameraRegistry.Exists(record => record.CameraNumber == sourceRecord.CameraNumber))
		{
			sourceRecord.CameraNumber = targetRoom.CameraRegistry.Count == 0 ? 1 : targetRoom.CameraRegistry.Max(record => record.CameraNumber) + 1;
		}
		targetRoom.CameraRegistry.Add(sourceRecord);
		logger.ZLogInformation($"Transferred camera {cameraId} registration from Scanner Room {sourceRoom.Id} to {targetRoom.Id} with number {sourceRecord.CameraNumber}");
		return true;
	}
}


