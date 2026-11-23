using System;
using System.Collections.Generic;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Packets;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Bases;

namespace Nitrox.Model.Subnautica.Packets;

/// <summary>
///     Sent by the server: everything a Scanner Room's scan has found and the types it offers to scan for. A client gets
///     it when it subscribes, when the room's simulating player has reported a full list, and as the correction when
///     the server refused a change that client reported.
/// </summary>
[Serializable]
public class MapRoomScanSnapshot : Packet
{
    public NitroxId MapRoomId { get; }
    public long Generation { get; }
    public List<MapRoomScanResultRecord> Results { get; }

    /// <summary>
    ///     Empty as long as nobody has reported the types to the server, which is the case after a server start.
    /// </summary>
    public List<NitroxTechType> AvailableTypes { get; }

    public MapRoomScanSnapshot(NitroxId mapRoomId, long generation, List<MapRoomScanResultRecord> results, List<NitroxTechType> availableTypes)
    {
        MapRoomId = mapRoomId;
        Generation = generation;
        Results = results;
        AvailableTypes = availableTypes;
    }
}
