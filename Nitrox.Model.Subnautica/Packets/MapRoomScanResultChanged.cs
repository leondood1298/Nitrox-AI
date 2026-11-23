using System;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Packets;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Bases;

namespace Nitrox.Model.Subnautica.Packets;

/// <summary>
///     Sent by the server to the subscribers of a Scanner Room when its scan found one resource, or lost one.
/// </summary>
[Serializable]
public class MapRoomScanResultChanged : Packet
{
    public NitroxId MapRoomId { get; }
    public long Generation { get; }
    public MapRoomScanResultRecord Result { get; }
    public bool Removed { get; }

    public MapRoomScanResultChanged(NitroxId mapRoomId, long generation, MapRoomScanResultRecord result, bool removed)
    {
        MapRoomId = mapRoomId;
        Generation = generation;
        Result = result;
        Removed = removed;
    }
}
