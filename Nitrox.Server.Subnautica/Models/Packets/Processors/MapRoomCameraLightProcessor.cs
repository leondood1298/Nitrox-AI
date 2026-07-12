using System.Threading.Tasks;
using System.Collections.Generic;
using Nitrox.Model.Packets.Core;
using Nitrox.Model.Subnautica.Packets;
using Nitrox.Server.Subnautica.Models.GameLogic;
using Nitrox.Server.Subnautica.Models.GameLogic.Entities;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Bases;
using System.Linq;
using Nitrox.Server.Subnautica.Models.Packets.Core;

namespace Nitrox.Server.Subnautica.Models.Packets.Processors;

internal sealed class MapRoomCameraLightProcessor(SimulationOwnershipData simulationOwnershipData, EntityRegistry entityRegistry, ILogger<MapRoomCameraLightProcessor> logger) : IAuthPacketProcessor<MapRoomCameraLight>, IAuthPacketProcessor, IPacketProcessor, IPacketProcessor<AuthProcessorContext, MapRoomCameraLight>
{
	private readonly SimulationOwnershipData simulationOwnershipData = simulationOwnershipData;
	private readonly ILogger<MapRoomCameraLightProcessor> logger = logger;
	private readonly EntityRegistry entityRegistry = entityRegistry;

	public async Task Process(AuthProcessorContext context, MapRoomCameraLight packet)
	{
		List<MapRoomCameraRecord> records = entityRegistry.GetEntities<MapRoomEntity>().Select(room => room.GetCameraRecord(packet.CameraId)).Where(record => record != null).ToList()!;
		if (packet.IsServerResponse || simulationOwnershipData.GetPlayerForLock(packet.CameraId) != context.Sender || records.Count != 1)
		{
			logger.ZLogWarning($"Rejected camera light update from session {context.Sender.SessionId}: camera {packet.CameraId}, on {packet.On}");
			await context.ReplyAsync(new MapRoomCameraLight(packet.CameraId, packet.On, 0, true, false));
			return;
		}

		long revision;
		MapRoomCameraRecord record = records[0];
		lock (record)
		{
			if (record.LightOn == packet.On)
			{
				revision = record.LightRevision;
			}
			else
			{
				revision = record.LightRevision + 1;
				record.LightOn = packet.On;
				record.LightRevision = revision;
			}
		}
		logger.ZLogInformation($"Accepted camera light update: camera {packet.CameraId}, on {packet.On}, revision {revision}, session {context.Sender.SessionId}");
		await context.SendToAllAsync(new MapRoomCameraLight(packet.CameraId, packet.On, revision, true, true));
	}
}


