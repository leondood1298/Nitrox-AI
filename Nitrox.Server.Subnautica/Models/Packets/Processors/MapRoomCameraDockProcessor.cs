using System.Threading.Tasks;
using Nitrox.Model.Packets.Core;
using Nitrox.Model.Subnautica.Packets;
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

	public async Task Process(AuthProcessorContext context, MapRoomCameraDock packet)
	{
		if (packet.IsServerResponse || packet.DockingIndex is < 0 or > 1 || !entityRegistry.TryGetEntityById(packet.MapRoomId, out MapRoomEntity mapRoom))
		{
			logger.ZLogWarning($"Rejected camera dock from session {context.Sender.SessionId}: room {packet.MapRoomId}, camera {packet.CameraId}, slot {packet.DockingIndex}");
			await context.ReplyAsync(new MapRoomCameraDock(packet.CameraId, packet.MapRoomId, packet.DockingIndex, 0, true, false));
			return;
		}

		MapRoomCameraDock response;
		lock (mapRoom)
		{
			Nitrox.Model.DataStructures.NitroxId? occupyingCamera = mapRoom.GetDockedCamera(packet.DockingIndex);
			bool granted = (occupyingCamera == null || occupyingCamera == packet.CameraId) && !mapRoom.IsCameraDocked(packet.CameraId);
			if (occupyingCamera == packet.CameraId)
			{
				granted = true;
			}
			if (granted && occupyingCamera == null)
			{
				mapRoom.SetDockedCamera(packet.DockingIndex, packet.CameraId);
			}
			response = new MapRoomCameraDock(packet.CameraId, packet.MapRoomId, packet.DockingIndex, mapRoom.DockingRevision, true, granted);
		}

		if (response.Granted)
		{
			simulationOwnershipData.RevokeOwnerOfId(packet.CameraId);
			logger.ZLogInformation($"Accepted camera dock: room {packet.MapRoomId}, camera {packet.CameraId}, slot {packet.DockingIndex}, revision {response.Revision}, session {context.Sender.SessionId}");
			await context.SendToAllAsync(response);
		}
		else
		{
			logger.ZLogWarning($"Rejected conflicting camera dock: room {packet.MapRoomId}, camera {packet.CameraId}, slot {packet.DockingIndex}, revision {response.Revision}, session {context.Sender.SessionId}");
			await context.ReplyAsync(response);
		}
	}
}


