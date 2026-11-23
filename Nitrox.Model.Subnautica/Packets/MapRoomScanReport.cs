using System;
using System.Collections.Generic;
using Nitrox.Model.DataStructures;
using Nitrox.Model.DataStructures.Unity;
using Nitrox.Model.Packets;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Bases;

namespace Nitrox.Model.Subnautica.Packets;

/// <summary>
///     Sent by the player who simulates a Scanner Room: everything its scan has found, as that player's game sees it.
///     The server adds what it knows of the world itself and passes the result on as a <see cref="MapRoomScanSnapshot" />.
/// </summary>
[Serializable]
public class MapRoomScanReport : Packet
{
    public NitroxId MapRoomId { get; }

    /// <summary>
    ///     The generation of the scan target these results belong to. A report for an older target is dropped.
    /// </summary>
    public long Generation { get; }

    public NitroxVector3 ScanOrigin { get; }
    public float ScanRange { get; }
    public List<MapRoomScanResultRecord> Results { get; }

    public MapRoomScanReport(NitroxId mapRoomId, long generation, NitroxVector3 scanOrigin, float scanRange, List<MapRoomScanResultRecord> results)
    {
        MapRoomId = mapRoomId;
        Generation = generation;
        ScanOrigin = scanOrigin;
        ScanRange = scanRange;
        Results = results;
    }
}
