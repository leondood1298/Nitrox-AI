using System;
using System.Collections.Generic;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Packets;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;

namespace Nitrox.Model.Subnautica.Packets;

[Serializable]
public class MapRoomScanTypesSnapshot : Packet
{
    public NitroxId MapRoomId { get; }
    public List<NitroxTechType> TechTypes { get; }
    public long Revision { get; }
    public bool IsServerResponse { get; }
    public bool Granted { get; }

    public MapRoomScanTypesSnapshot(NitroxId mapRoomId, List<NitroxTechType> techTypes, long revision = 0, bool isServerResponse = false, bool granted = false)
    {
        MapRoomId = mapRoomId;
        TechTypes = techTypes;
        Revision = revision;
        IsServerResponse = isServerResponse;
        Granted = granted;
    }
}
