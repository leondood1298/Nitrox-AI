using System;
using Nitrox.Model.DataStructures;
using Nitrox.Model.DataStructures.Unity;
using Nitrox.Model.Packets;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Bases;

namespace Nitrox.Model.Subnautica.Packets;

/// <summary>
///     Sent by the player who simulates a Scanner Room when its scan finds one resource, or loses one.
/// </summary>
[Serializable]
public class MapRoomScanResultReport : Packet
{
    public NitroxId MapRoomId { get; }
    public long Generation { get; }
    public MapRoomScanResultRecord Result { get; }
    public bool Removed { get; }

    /// <summary>
    ///     True when the resource is gone from the results because it moved out of scan range, false when the sender's
    ///     game merely unloaded it. Only the server knows whether an unloaded resource still exists.
    /// </summary>
    public bool IsRangeExit { get; }

    public NitroxVector3 ScanOrigin { get; }
    public float ScanRange { get; }

    public MapRoomScanResultReport(NitroxId mapRoomId, long generation, MapRoomScanResultRecord result, bool removed, bool isRangeExit, NitroxVector3 scanOrigin,
        float scanRange)
    {
        MapRoomId = mapRoomId;
        Generation = generation;
        Result = result;
        Removed = removed;
        IsRangeExit = isRangeExit;
        ScanOrigin = scanOrigin;
        ScanRange = scanRange;
    }
}
