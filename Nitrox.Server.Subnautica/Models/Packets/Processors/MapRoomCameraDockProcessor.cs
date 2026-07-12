using System.Threading.Tasks;
using System.Linq;
using Nitrox.Model.Packets.Core;
using Nitrox.Model.Subnautica.Packets;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Bases;
using Nitrox.Server.Subnautica.Models.GameLogic;
using Nitrox.Server.Subnautica.Models.GameLogic.Entities;
using Nitrox.Server.Subnautica.Models.Packets.Core;

namespace Nitrox.Server.Subnautica.Models.Packets.Processors;

internal sealed class MapRoomCameraDockProcessor(EntityRegistry entityRegistry, SimulationOwnershipData simulationOwnershipData, ILogger<MapRoomCameraDockProcessor> logger) : IAuthPacketProcessor<MapRoomCameraDock>, IAuthPacketProcessor, IPacketProcessor, IPacketProcessor<AuthProcessorContext, MapRoomCameraDock>
{
	private readonly EntityRegistry entityRegistry = entityRegistry;
	private readonly SimulationOwnershipData simulationOwnershipData = simulationOwnershipData;
	private readonly ILogger<MapRoomCameraDockProcessor> logger = logger;
	private readonly object dockingLock = new();

	public async Task Process(AuthProcessorContext context, MapRoomCameraDock packet)
	{
		if (packet.IsServerResponse || packet.DockingIndex is < 0 or > 1 || !entityRegistry.TryGetEntityById(packet.MapRoomId, out MapRoomEntity mapRoom))
		{
			logger.ZLogWarning($"Rejected camera dock from session {context.Sender.SessionId}: room {packet.MapRoomId}, camera {packet.CameraId}, slot {packet.DockingIndex}");
			await context.ReplyAsync(new MapRoomCameraDock(packet.CameraId, packet.MapRoomId, packet.DockingIndex, 0, true, false, packet.IsDocked));
			return;
		}

		bool validWorldCamera = entityRegistry.TryGetEntityById(packet.CameraId, out WorldEntity camera) && camera.TechType.Equals(new Nitrox.Model.Subnautica.DataStructures.GameLogic.NitroxTechType("MapRoomCamera"));
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
			logger.ZLogWarning($"Rejected unknown camera dock from session {context.Sender.SessionId}: room {packet.MapRoomId}, camera {packet.CameraId}, slot {packet.DockingIndex}");
			await context.ReplyAsync(new MapRoomCameraDock(packet.CameraId, packet.MapRoomId, packet.DockingIndex, 0, true, false, packet.IsDocked));
			return;
		}

		MapRoomCameraDock response;
		lock (dockingLock)
		{
			lock (mapRoom)
			{
				bool granted;
				if (!packet.IsDocked)
				{
					granted = mapRoom.TryClearDockedCamera(packet.DockingIndex, packet.CameraId);
				}
				else
				{
					Nitrox.Model.DataStructures.NitroxId? occupyingCamera = mapRoom.GetDockedCamera(packet.DockingIndex);
					bool localSlotAvailable = (occupyingCamera == null || occupyingCamera == packet.CameraId) && (!mapRoom.IsCameraDocked(packet.CameraId) || occupyingCamera == packet.CameraId);
					granted = localSlotAvailable && TryTransferRegistration(mapRoom, packet.CameraId);
					if (granted && occupyingCamera == packet.CameraId)
					{
						granted = true;
					}
					if (granted && occupyingCamera == null)
					{
						mapRoom.SetDockedCamera(packet.DockingIndex, packet.CameraId);
					}
				}
				int cameraNumber = granted ? mapRoom.GetOrAssignCameraNumber(packet.CameraId, packet.DockingIndex + 1) : 0;
				MapRoomCameraRecord? record = granted ? mapRoom.GetCameraRecord(packet.CameraId) : null;
				response = new MapRoomCameraDock(packet.CameraId, packet.MapRoomId, packet.DockingIndex, mapRoom.DockingRevision, true, granted, packet.IsDocked, cameraNumber, record?.LightOn ?? false, record?.LightRevision ?? 0, record?.Energy ?? 100f, record?.Health ?? 100f, record?.ComponentRevision ?? 0);
			}
		}

		if (response.Granted)
		{
			if (packet.IsDocked)
			{
				simulationOwnershipData.RevokeOwnerOfId(packet.CameraId);
			}
			logger.ZLogInformation($"Accepted camera {(packet.IsDocked ? "dock" : "undock")}: room {packet.MapRoomId}, camera {packet.CameraId}, number {response.CameraNumber}, slot {packet.DockingIndex}, revision {response.Revision}, session {context.Sender.SessionId}");
			await context.SendToAllAsync(response);
		}
		else
		{
			logger.ZLogWarning($"Rejected conflicting camera dock: room {packet.MapRoomId}, camera {packet.CameraId}, slot {packet.DockingIndex}, revision {response.Revision}, session {context.Sender.SessionId}");
			await context.ReplyAsync(response);
		}
	}

	internal static bool IsKnownCamera(bool validWorldCamera, bool registeredCamera) => validWorldCamera || registeredCamera;
	internal static bool CanBootstrapRestoredCamera(bool isDocked, bool senderOwnsRoom, bool slotAvailable, int registeredCameraCount) =>
		isDocked && senderOwnsRoom && slotAvailable && registeredCameraCount is >= 0 and < 2;

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


