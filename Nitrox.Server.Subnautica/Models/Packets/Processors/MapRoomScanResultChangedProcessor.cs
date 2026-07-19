using System.Linq;
using Nitrox.Model.DataStructures.Unity;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Bases;
using Nitrox.Model.Subnautica.Packets;
using Nitrox.Server.Subnautica.Models.GameLogic;
using Nitrox.Server.Subnautica.Models.GameLogic.Entities;
using Nitrox.Server.Subnautica.Models.Packets.Core;

namespace Nitrox.Server.Subnautica.Models.Packets.Processors;

internal sealed class MapRoomScanResultChangedProcessor(EntityRegistry entityRegistry, SimulationOwnershipData simulationOwnershipData, PlayerManager playerManager, MapRoomScanResultSubscriptions subscriptions) : IAuthPacketProcessor<MapRoomScanResultChanged>
{
    public async Task Process(AuthProcessorContext context, MapRoomScanResultChanged packet)
    {
        if (!entityRegistry.TryGetEntityById(packet.MapRoomId, out MapRoomEntity room) || simulationOwnershipData.GetPlayerForLock(packet.MapRoomId) != context.Sender)
        {
            await context.ReplyAsync(CreateResponse(packet, room: null, granted: false));
            return;
        }
        NitroxVector3? scanAnchor = null;
        if (packet.IsRangeExit || !packet.Removed)
        {
            if (!MapRoomWorldResourceIndex.TryResolveScanAnchor(entityRegistry, room, out NitroxVector3 resolvedAnchor))
            {
                await context.ReplyAsync(CreateResponse(packet, room: null, granted: false));
                return;
            }
            scanAnchor = resolvedAnchor;
        }
        else if (MapRoomWorldResourceIndex.TryResolveScanAnchor(entityRegistry, room, out NitroxVector3 removalAnchor))
        {
            // Owner-only removals do not require an anchor, but exact server-backed removals use it to distinguish
            // an ordinary cell unload from a resource that is genuinely outside the current scanner query.
            scanAnchor = removalAnchor;
        }
        if (!MapRoomScanResultAuthority.TryApply(room, packet, entityRegistry, scanAnchor, out bool canonicalCorrectionRequired))
        {
            if (canonicalCorrectionRequired)
            {
                await context.ReplyAsync(CreateCorrectionSnapshot(room, packet));
            }
            else
            {
                await context.ReplyAsync(CreateResponse(packet, room: null, granted: false));
            }
            return;
        }
        MapRoomScanResultChanged response = CreateResponse(packet, room, granted: true);
        await context.ReplyAsync(response);
        foreach (Player player in playerManager.GetConnectedPlayersExcept(context.Sender).Where(player => subscriptions.Contains(room.Id, player.SessionId)))
        {
            await context.SendAsync(response, player.SessionId);
        }
    }

    private static MapRoomScanResultChanged CreateResponse(MapRoomScanResultChanged packet, MapRoomEntity? room, bool granted)
    {
        NitroxVector3 position = packet.Position;
        long revision = 0;
        if (room != null)
        {
            lock (room)
            {
                revision = room.ScanResultRevision;
                if (granted && !packet.Removed)
                {
                    MapRoomScanResultRecord? canonical = room.ScanResults.Find(result => result.ResourceId == packet.ResourceId);
                    position = canonical?.Position ?? position;
                }
            }
        }
        return new MapRoomScanResultChanged(packet.MapRoomId, packet.Generation, packet.ResourceId, packet.TechType, position,
            packet.Removed, packet.IsRangeExit, packet.ScanOrigin, packet.ScanRange, revision, true, granted);
    }

    internal static MapRoomScanResultSnapshot CreateCorrectionSnapshot(MapRoomEntity room, MapRoomScanResultChanged request)
    {
        lock (room)
        {
            return new MapRoomScanResultSnapshot(room.Id, room.ScanResultGeneration,
                room.ScanResults.Select(result => new MapRoomScanResultRecord(result.ResourceId, result.TechType, result.Position)).ToList(),
                request.ScanOrigin, request.ScanRange, room.ScanResultRevision, true, true);
        }
    }
}
