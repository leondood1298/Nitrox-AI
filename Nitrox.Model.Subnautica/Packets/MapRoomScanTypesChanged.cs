using System;
using System.Collections.Generic;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Packets;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;

namespace Nitrox.Model.Subnautica.Packets;

/// <summary>
///     Sent by the server to the subscribers of a Scanner Room when the types it offers to scan for have changed.
/// </summary>
[Serializable]
public class MapRoomScanTypesChanged : Packet
{
    public NitroxId MapRoomId { get; }
    public List<NitroxTechType> TechTypes { get; }

    public MapRoomScanTypesChanged(NitroxId mapRoomId, List<NitroxTechType> techTypes)
    {
        MapRoomId = mapRoomId;
        TechTypes = techTypes;
    }
}
