using System.Linq;
using System.Collections.Generic;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Bases;
using Nitrox.Model.Subnautica.Packets;
using Nitrox.Server.Subnautica.Models.GameLogic;
using Nitrox.Server.Subnautica.Models.GameLogic.Entities;
using Nitrox.Server.Subnautica.Models.Packets.Core;

namespace Nitrox.Server.Subnautica.Models.Packets.Processors;

internal sealed class MapRoomCameraComponentStateProcessor(SimulationOwnershipData simulationOwnershipData, EntityRegistry entityRegistry) : IAuthPacketProcessor<MapRoomCameraComponentState>
{
    public async Task Process(AuthProcessorContext context, MapRoomCameraComponentState packet)
    {
        List<MapRoomCameraRecord> records = entityRegistry.GetEntities<MapRoomEntity>().Select(room => room.GetCameraRecord(packet.CameraId)).Where(record => record != null).ToList()!;
        if (packet.IsServerResponse || records.Count != 1 || simulationOwnershipData.GetPlayerForLock(packet.CameraId) != context.Sender || packet.Energy < 0f || packet.Health < 0f)
        {
            await context.ReplyAsync(new MapRoomCameraComponentState(packet.CameraId, packet.Energy, packet.Health, 0, true, false));
            return;
        }
        MapRoomCameraRecord record = records[0];
        lock (record)
        {
            record.Energy = packet.Energy;
            record.Health = packet.Health;
            record.ComponentRevision++;
        }
        await context.SendToAllAsync(new MapRoomCameraComponentState(packet.CameraId, record.Energy, record.Health, record.ComponentRevision, true, true));
    }
}
