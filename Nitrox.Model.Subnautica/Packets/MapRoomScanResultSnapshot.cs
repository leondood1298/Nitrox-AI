using System;
using System.Collections.Generic;
using Nitrox.Model.DataStructures;
using Nitrox.Model.DataStructures.Unity;
using Nitrox.Model.Packets;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Bases;

namespace Nitrox.Model.Subnautica.Packets;

[Serializable]
public class MapRoomScanResultSnapshot : Packet
{
    public NitroxId MapRoomId { get; }
    public long Generation { get; }
    public List<MapRoomScanResultRecord> Results { get; }
    public NitroxVector3 ScanOrigin { get; }
    public float ScanRange { get; }
    public long Revision { get; }
    public bool IsServerResponse { get; }
    public bool Granted { get; }

    public MapRoomScanResultSnapshot(NitroxId mapRoomId, long generation, List<MapRoomScanResultRecord> results, NitroxVector3 scanOrigin, float scanRange, long revision = 0, bool isServerResponse = false, bool granted = false)
    {
        MapRoomId = mapRoomId;
        Generation = generation;
        Results = results;
        ScanOrigin = scanOrigin;
        ScanRange = scanRange;
        Revision = revision;
        IsServerResponse = isServerResponse;
        Granted = granted;
    }
}
