using System;
using System.Runtime.Serialization;
using BinaryPack.Attributes;

namespace Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;

[Serializable, DataContract]
public class WaterParkCreatureMetadata : CreatureMetadata
{
    [DataMember(Order = 1)]
    public float Age { get; init; }

    [DataMember(Order = 2)]
    public double MatureTime { get; init; }

    [DataMember(Order = 3)]
    public float TimeNextBreed { get; init; }
    
    [DataMember(Order = 4)]
    public bool BornInside { get; init; }

    [IgnoreConstructor]
    protected WaterParkCreatureMetadata()
    {
        // Constructor for serialization. Has to be "protected" for json serialization.
    }

    public WaterParkCreatureMetadata(float age, double matureTime, float timeNextBreed, bool bornInside, float infectedAmount, float enzymeEndTime = NO_ENZYME, bool hasLeash = false,
                                     Nitrox.Model.DataStructures.Unity.NitroxVector3 leashPosition = default, bool hasSize = false, float size = 0f)
        : base(infectedAmount, enzymeEndTime, hasLeash, leashPosition, hasSize, size)
    {
        Age = age;
        MatureTime = matureTime;
        TimeNextBreed = timeNextBreed;
        BornInside = bornInside;
    }

    public override string ToString()
    {
        return $"[WaterParkCreatureMetadata Age: {Age}, MatureTime: {MatureTime}, TimeNextBreed: {TimeNextBreed}, BornInside: {BornInside}, {base.ToString()}]";
    }
}
