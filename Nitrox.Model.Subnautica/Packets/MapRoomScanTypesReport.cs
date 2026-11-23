using System;
using System.Collections.Generic;
using Nitrox.Model.DataStructures;
using Nitrox.Model.DataStructures.Unity;
using Nitrox.Model.Packets;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;

namespace Nitrox.Model.Subnautica.Packets;

/// <summary>
///     Sent by the player who simulates a Scanner Room: the types of resources its screen offers to scan for, as that
///     player's game sees them.
/// </summary>
[Serializable]
public class MapRoomScanTypesReport : Packet
{
    public NitroxId MapRoomId { get; }
    public List<NitroxTechType> TechTypes { get; }

    /// <summary>
    ///     Every type the sender's game can scan for at all. The server uses it to add the types of resources in range
    ///     that the sender has not loaded.
    /// </summary>
    public List<NitroxTechType> DetectableTechTypes { get; }

    public NitroxVector3 ScanOrigin { get; }
    public float ScanRange { get; }

    public MapRoomScanTypesReport(NitroxId mapRoomId, List<NitroxTechType> techTypes, List<NitroxTechType> detectableTechTypes, NitroxVector3 scanOrigin, float scanRange)
    {
        MapRoomId = mapRoomId;
        TechTypes = techTypes;
        DetectableTechTypes = detectableTechTypes;
        ScanOrigin = scanOrigin;
        ScanRange = scanRange;
    }
}
