using System;
using Nitrox.Model.DataStructures;
using Nitrox.Model.DataStructures.Unity;
using Nitrox.Model.Packets;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;

namespace Nitrox.Model.Subnautica.Packets;

[Serializable]
public class MapRoomScanResultChanged : Packet
{
    public NitroxId MapRoomId { get; }
    public long Generation { get; }
    public string ResourceId { get; }
    public NitroxTechType TechType { get; }
    public NitroxVector3 Position { get; }
    public bool Removed { get; }
    public bool IsRangeExit { get; }
    public NitroxVector3 ScanOrigin { get; }
    public float ScanRange { get; }
    public long Revision { get; }
    public bool IsServerResponse { get; }
    public bool Granted { get; }

    public MapRoomScanResultChanged(NitroxId mapRoomId, long generation, string resourceId, NitroxTechType techType, NitroxVector3 position,
        bool removed = false, bool isRangeExit = false, NitroxVector3 scanOrigin = default, float scanRange = 0f,
        long revision = 0, bool isServerResponse = false, bool granted = false)
    {
        MapRoomId = mapRoomId;
        Generation = generation;
        ResourceId = resourceId;
        TechType = techType;
        Position = position;
        Removed = removed;
        IsRangeExit = isRangeExit;
        ScanOrigin = scanOrigin;
        ScanRange = scanRange;
        Revision = revision;
        IsServerResponse = isServerResponse;
        Granted = granted;
    }
}
