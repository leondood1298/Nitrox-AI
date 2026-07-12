using System.Collections.Generic;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Bases;
using Nitrox.Model.Subnautica.Packets;
using Nitrox.Server.Subnautica.Models.GameLogic;
using Nitrox.Server.Subnautica.Models.GameLogic.Entities;
using Nitrox.Server.Subnautica.Models.Packets.Core;

namespace Nitrox.Server.Subnautica.Models.Packets.Processors;

internal sealed class MapRoomScanTypesSnapshotProcessor(EntityRegistry entityRegistry, SimulationOwnershipData simulationOwnershipData) : IAuthPacketProcessor<MapRoomScanTypesSnapshot>
{
    public async Task Process(AuthProcessorContext context, MapRoomScanTypesSnapshot packet)
    {
        if (!entityRegistry.TryGetEntityById(packet.MapRoomId, out MapRoomEntity room) || simulationOwnershipData.GetPlayerForLock(packet.MapRoomId) != context.Sender || !MapRoomScanTypesAuthority.TryApply(room, packet, out List<NitroxTechType> accepted, out long revision))
        {
            await context.ReplyAsync(new MapRoomScanTypesSnapshot(packet.MapRoomId, [], 0, true, false));
            return;
        }
        await context.SendToAllAsync(new MapRoomScanTypesSnapshot(packet.MapRoomId, accepted, revision, true, true));
    }
}
