using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.Packets;
using NitroxClient.Communication.Abstract;
using NitroxClient.Extensions;

namespace NitroxClient.GameLogic;

public sealed class MapRoomScanResultSubscriber(IPacketSender packetSender)
{
    public void Set(uGUI_MapRoomScanner scanner, bool subscribed)
    {
        MapRoomFunctionality mapRoom = scanner ? scanner.mapRoom : null;
        Set(mapRoom, subscribed);
    }

    public void Set(MapRoomFunctionality mapRoom, bool subscribed)
    {
        if (mapRoom && mapRoom.TryGetNitroxId(out NitroxId roomId))
        {
            packetSender.Send(new MapRoomScanResultSubscription(roomId, subscribed));
        }
    }
}
