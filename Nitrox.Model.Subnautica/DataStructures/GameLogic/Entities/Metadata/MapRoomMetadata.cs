using System;
using System.Runtime.Serialization;
using BinaryPack.Attributes;

namespace Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;

/// <summary>
///     What a Scanner Room scans for and how far it has got. Any player who sees the room may choose the target; the
///     progress comes from the room's simulating player.
/// </summary>
[Serializable]
[DataContract]
public class MapRoomMetadata : EntityMetadata
{
    [DataMember(Order = 1)]
    public NitroxTechType TypeToScan { get; }

    [DataMember(Order = 2)]
    public int NumNodesScanned { get; }

    /// <summary>
    ///     Counts the scan targets the room has had. The server sets it. A request names the generation it was made
    ///     against, so that one made against an older target can be told apart, and scan results name the generation
    ///     they were found for.
    /// </summary>
    [DataMember(Order = 3)]
    public long Generation { get; }

    [IgnoreConstructor]
    protected MapRoomMetadata()
    {
    }

    public MapRoomMetadata(NitroxTechType typeToScan, int numNodesScanned, long generation = 0)
    {
        TypeToScan = typeToScan;
        NumNodesScanned = numNodesScanned;
        Generation = generation;
    }

    public override string ToString()
    {
        return $"[MapRoomMetadata TypeToScan: {TypeToScan} NumNodesScanned: {NumNodesScanned} Generation: {Generation}]";
    }
}
