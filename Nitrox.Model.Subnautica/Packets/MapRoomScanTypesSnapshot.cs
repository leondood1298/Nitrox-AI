using System;
using System.Collections.Generic;
using Nitrox.Model.DataStructures;
using Nitrox.Model.DataStructures.Unity;
using Nitrox.Model.Packets;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;

namespace Nitrox.Model.Subnautica.Packets;

[Serializable]
public class MapRoomScanTypesSnapshot : Packet
{
    public NitroxId MapRoomId { get; }
    public List<NitroxTechType> TechTypes { get; }
    public List<NitroxTechType> DetectableTechTypes { get; }
    public NitroxVector3 ScanOrigin { get; }
    public float ScanRange { get; }
    public long Revision { get; }
    public bool IsServerResponse { get; }
    public bool Granted { get; }

    public MapRoomScanTypesSnapshot(NitroxId mapRoomId, List<NitroxTechType> techTypes, List<NitroxTechType> detectableTechTypes, NitroxVector3 scanOrigin, float scanRange, long revision = 0, bool isServerResponse = false, bool granted = false)
    {
        MapRoomId = mapRoomId;
        TechTypes = techTypes;
        DetectableTechTypes = detectableTechTypes;
        ScanOrigin = scanOrigin;
        ScanRange = scanRange;
        Revision = revision;
        IsServerResponse = isServerResponse;
        Granted = granted;
    }
}
