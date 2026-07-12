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
        List<MapRoomEntity> rooms = entityRegistry.GetEntities<MapRoomEntity>().Where(room => room.GetCameraRecord(packet.CameraId) != null).ToList();
        MapRoomEntity? room = rooms.Count == 1 ? rooms[0] : null;
        bool senderIsCameraOwner = simulationOwnershipData.GetPlayerForLock(packet.CameraId) == context.Sender;
        bool senderIsDockedRoomOwner = room != null && room.IsCameraDocked(packet.CameraId) && simulationOwnershipData.GetPlayerForLock(room.Id) == context.Sender;
        if (packet.IsServerResponse || room == null || (!senderIsCameraOwner && !senderIsDockedRoomOwner) || packet.Energy < 0f || packet.Health < 0f)
        {
            await context.ReplyAsync(new MapRoomCameraComponentState(packet.CameraId, packet.Energy, packet.Health, 0, true, false));
            return;
        }
        MapRoomCameraRecord record = room.GetCameraRecord(packet.CameraId)!;
        lock (record)
        {
            record.Energy = packet.Energy;
            record.Health = packet.Health;
            record.ComponentRevision++;
        }
        await context.SendToAllAsync(new MapRoomCameraComponentState(packet.CameraId, record.Energy, record.Health, record.ComponentRevision, true, true));
    }
}
