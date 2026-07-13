using System.Threading.Tasks;
using Nitrox.Model.Packets.Core;
using Nitrox.Model.Subnautica.Packets;
using Nitrox.Model.Core;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Bases;
using Nitrox.Server.Subnautica.Models.GameLogic;
using Nitrox.Server.Subnautica.Models.GameLogic.Entities;
using Nitrox.Server.Subnautica.Models.Packets.Core;

namespace Nitrox.Server.Subnautica.Models.Packets.Processors;

internal sealed class MapRoomCameraControlProcessor(SimulationOwnershipData simulationOwnershipData, EntityRegistry entityRegistry, ILogger<MapRoomCameraControlProcessor> logger) : IAuthPacketProcessor<MapRoomCameraControl>, IAuthPacketProcessor, IPacketProcessor, IPacketProcessor<AuthProcessorContext, MapRoomCameraControl>
{
	private readonly SimulationOwnershipData simulationOwnershipData = simulationOwnershipData;
	private readonly EntityRegistry entityRegistry = entityRegistry;
	private readonly ILogger<MapRoomCameraControlProcessor> logger = logger;

	public async Task Process(AuthProcessorContext context, MapRoomCameraControl packet)
	{
		if (packet.IsServerResponse || !IsValidAssociation(packet))
		{
			logger.ZLogWarning($"Rejected camera control association from session {context.Sender.SessionId}: camera {packet.CameraId}, room {packet.MapRoomId}, slot {packet.CameraIndex}, controlling {packet.IsControlling}");
			await context.ReplyAsync(CreateResponse(packet, false, context.Sender.SessionId));
			return;
		}

		if (packet.IsControlling)
		{
			bool granted = simulationOwnershipData.TryToAcquire(packet.CameraId, context.Sender, SimulationLockType.EXCLUSIVE);
			SessionId controller = granted ? context.Sender.SessionId : simulationOwnershipData.GetPlayerForLock(packet.CameraId)?.SessionId ?? context.Sender.SessionId;
			MapRoomCameraControl response = CreateResponse(packet, granted, controller);
			if (granted)
			{
				logger.ZLogInformation($"Granted camera control to session {context.Sender.SessionId}: camera {packet.CameraId}, room {packet.MapRoomId}, slot {packet.CameraIndex}");
				await context.SendToAllAsync(response);
			}
			else
			{
				logger.ZLogWarning($"Rejected locked camera control from session {context.Sender.SessionId}: camera {packet.CameraId}, controller {controller}");
				await context.ReplyAsync(response);
			}
			return;
		}

		Player? currentOwner = simulationOwnershipData.GetPlayerForLock(packet.CameraId);
		if (CanAcknowledgeRelease(currentOwner != null, currentOwner == context.Sender))
		{
			if (currentOwner != null)
			{
				simulationOwnershipData.RevokeIfOwner(packet.CameraId, context.Sender);
			}
			logger.ZLogInformation($"Released camera control for session {context.Sender.SessionId}: camera {packet.CameraId}");
			await context.SendToAllAsync(CreateResponse(packet, true, context.Sender.SessionId));
		}
		else
		{
			await context.ReplyAsync(CreateResponse(packet, false, simulationOwnershipData.GetPlayerForLock(packet.CameraId)?.SessionId ?? context.Sender.SessionId));
		}
	}

	internal static bool CanAcknowledgeRelease(bool hasOwner, bool senderOwnsLock) => !hasOwner || senderOwnsLock;

	private bool IsValidAssociation(MapRoomCameraControl packet)
	{
		if (!packet.IsControlling)
		{
			return true;
		}
		if (packet.MapRoomId.HasValue)
		{
			if (packet.CameraIndex is < 0 or > 1 || !entityRegistry.TryGetEntityById(packet.MapRoomId.Value, out MapRoomEntity mapRoom))
			{
				return false;
			}
			lock (mapRoom)
			{
				return mapRoom.GetCameraRecord(packet.CameraId) != null && mapRoom.GetDockedCamera(packet.CameraIndex) == packet.CameraId;
			}
		}

		int registrations = 0;
		bool isDocked = false;
		foreach (MapRoomEntity room in entityRegistry.GetEntities<MapRoomEntity>())
		{
			lock (room)
			{
				if (room.GetCameraRecord(packet.CameraId) != null)
				{
					registrations++;
					isDocked |= room.IsCameraDocked(packet.CameraId);
				}
			}
		}
		return registrations == 0 || (registrations == 1 && !isDocked);
	}

	private static MapRoomCameraControl CreateResponse(MapRoomCameraControl packet, bool granted, SessionId controller) =>
		new(packet.CameraId, packet.MapRoomId, packet.CameraIndex, packet.IsControlling, packet.LightOn, true, granted, controller);
}


