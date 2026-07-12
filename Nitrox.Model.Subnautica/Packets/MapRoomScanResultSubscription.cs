using System;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Packets;

namespace Nitrox.Model.Subnautica.Packets;

[Serializable]
public class MapRoomScanResultSubscription(NitroxId mapRoomId, bool subscribed) : Packet
{
    public NitroxId MapRoomId { get; } = mapRoomId;
    public bool Subscribed { get; } = subscribed;
}
