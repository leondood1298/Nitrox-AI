using System.Threading.Tasks;
using System.Collections.Generic;
using Nitrox.Model.Packets.Core;
using Nitrox.Model.Subnautica.Packets;
using Nitrox.Server.Subnautica.Models.GameLogic;
using Nitrox.Server.Subnautica.Models.Packets.Core;

namespace Nitrox.Server.Subnautica.Models.Packets.Processors;

internal sealed class MapRoomCameraLightProcessor(SimulationOwnershipData simulationOwnershipData, ILogger<MapRoomCameraLightProcessor> logger) : IAuthPacketProcessor<MapRoomCameraLight>, IAuthPacketProcessor, IPacketProcessor, IPacketProcessor<AuthProcessorContext, MapRoomCameraLight>
{
	private readonly SimulationOwnershipData simulationOwnershipData = simulationOwnershipData;
	private readonly ILogger<MapRoomCameraLightProcessor> logger = logger;
	private readonly Dictionary<Nitrox.Model.DataStructures.NitroxId, (bool On, long Revision)> states = new();

	public async Task Process(AuthProcessorContext context, MapRoomCameraLight packet)
	{
		if (packet.IsServerResponse || simulationOwnershipData.GetPlayerForLock(packet.CameraId) != context.Sender)
		{
			logger.ZLogWarning($"Rejected camera light update from session {context.Sender.SessionId}: camera {packet.CameraId}, on {packet.On}");
			await context.ReplyAsync(new MapRoomCameraLight(packet.CameraId, packet.On, 0, true, false));
			return;
		}

		long revision;
		lock (states)
		{
			if (states.TryGetValue(packet.CameraId, out var state) && state.On == packet.On)
			{
				revision = state.Revision;
			}
			else
			{
				revision = states.TryGetValue(packet.CameraId, out state) ? state.Revision + 1 : 1;
				states[packet.CameraId] = (packet.On, revision);
			}
		}
		logger.ZLogInformation($"Accepted camera light update: camera {packet.CameraId}, on {packet.On}, revision {revision}, session {context.Sender.SessionId}");
		await context.SendToAllAsync(new MapRoomCameraLight(packet.CameraId, packet.On, revision, true, true));
	}
}


