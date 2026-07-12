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

internal sealed class MapRoomCameraControlProcessor(SimulationOwnershipData simulationOwnershipData, EntityRegistry entityRegistry) : IAuthPacketProcessor<MapRoomCameraControl>, IAuthPacketProcessor, IPacketProcessor, IPacketProcessor<AuthProcessorContext, MapRoomCameraControl>
{
	private readonly SimulationOwnershipData simulationOwnershipData = simulationOwnershipData;
	private readonly EntityRegistry entityRegistry = entityRegistry;

	public async Task Process(AuthProcessorContext context, MapRoomCameraControl packet)
	{
		if (packet.IsServerResponse || !IsValidAssociation(packet))
		{
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
				await context.SendToAllAsync(response);
			}
			else
			{
				await context.ReplyAsync(response);
			}
			return;
		}

		if (simulationOwnershipData.RevokeIfOwner(packet.CameraId, context.Sender))
		{
			await context.SendToAllAsync(CreateResponse(packet, true, context.Sender.SessionId));
		}
		else
		{
			await context.ReplyAsync(CreateResponse(packet, false, simulationOwnershipData.GetPlayerForLock(packet.CameraId)?.SessionId ?? context.Sender.SessionId));
		}
	}

	private bool IsValidAssociation(MapRoomCameraControl packet) => !packet.MapRoomId.HasValue ||
		(packet.CameraIndex is >= 0 and < 2 && entityRegistry.TryGetEntityById<MapRoomEntity>(packet.MapRoomId.Value, out _));

	private static MapRoomCameraControl CreateResponse(MapRoomCameraControl packet, bool granted, SessionId controller) =>
		new(packet.CameraId, packet.MapRoomId, packet.CameraIndex, packet.IsControlling, packet.LightOn, true, granted, controller);
}


