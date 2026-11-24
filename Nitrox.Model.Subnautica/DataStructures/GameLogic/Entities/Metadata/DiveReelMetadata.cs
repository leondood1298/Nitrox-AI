using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using BinaryPack.Attributes;
using Nitrox.Model.DataStructures.Unity;

namespace Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;

/// <summary>
///     The trail a pathfinder tool (DiveReel) laid: the positions of its nodes, first one first (DiveReel.nodePositions).
///     The nodes are shown wherever the tool is, also to the other players, and come back after a rejoin or a restart.
/// </summary>
[Serializable]
[DataContract]
public class DiveReelMetadata : EntityMetadata
{
    [DataMember(Order = 1)]
    public List<NitroxVector3> NodePositions { get; }

    [IgnoreConstructor]
    protected DiveReelMetadata()
    {
        // Constructor for serialization. Has to be "protected" for json serialization.
    }

    public DiveReelMetadata(List<NitroxVector3> nodePositions)
    {
        NodePositions = nodePositions;
    }

    public override string ToString()
    {
        return $"[{nameof(DiveReelMetadata)} Nodes: {NodePositions?.Count ?? 0}]";
    }
}
