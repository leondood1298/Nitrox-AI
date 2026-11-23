using System;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Packets;

namespace Nitrox.Model.Subnautica.Packets;

/// <summary>
///     Sent by a client to start or stop receiving what a Scanner Room's scan finds. A client that starts is answered
///     with a <see cref="MapRoomScanSnapshot" />.
/// </summary>
[Serializable]
public class MapRoomScanSubscription(NitroxId mapRoomId, bool subscribed) : Packet
{
    public NitroxId MapRoomId { get; } = mapRoomId;
    public bool Subscribed { get; } = subscribed;
}
