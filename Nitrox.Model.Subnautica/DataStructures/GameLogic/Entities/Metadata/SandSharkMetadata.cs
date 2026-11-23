using System;
using System.Runtime.Serialization;
using BinaryPack.Attributes;
using Nitrox.Model.DataStructures.Unity;

namespace Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;

/// <summary>
///     Whether a Sand Shark lies buried in the sand (BurryInSand), and where, so that a player who loads it later sees it
///     there.
/// </summary>
[Serializable, DataContract]
public class SandSharkMetadata : CreatureMetadata
{
    [DataMember(Order = 1)]
    public bool Buried { get; }

    /// <summary>
    ///     BurryInSand.burryPosition
    /// </summary>
    [DataMember(Order = 2)]
    public NitroxVector3 BurryPosition { get; }

    /// <summary>
    ///     BurryInSand.floorAlign
    /// </summary>
    [DataMember(Order = 3)]
    public NitroxQuaternion FloorAlign { get; }

    [IgnoreConstructor]
    protected SandSharkMetadata()
    {
        // Constructor for serialization. Has to be "protected" for json serialization.
    }

    public SandSharkMetadata(bool buried, NitroxVector3 burryPosition, NitroxQuaternion floorAlign, float infectedAmount, float enzymeEndTime = NO_ENZYME, bool hasLeash = false,
                             NitroxVector3 leashPosition = default, bool hasSize = false, float size = 0f) : base(infectedAmount, enzymeEndTime, hasLeash, leashPosition, hasSize, size)
    {
        Buried = buried;
        BurryPosition = burryPosition;
        FloorAlign = floorAlign;
    }

    public override string ToString()
    {
        return $"[{nameof(SandSharkMetadata)} Buried: {Buried}, BurryPosition: {BurryPosition}, FloorAlign: {FloorAlign}, {base.ToString()}]";
    }
}
