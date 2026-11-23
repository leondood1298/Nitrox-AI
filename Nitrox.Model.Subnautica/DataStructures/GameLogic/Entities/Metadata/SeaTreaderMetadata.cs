using System;
using System.Runtime.Serialization;
using BinaryPack.Attributes;
using Nitrox.Model.DataStructures.Unity;

namespace Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;

[Serializable]
[DataContract]
public class SeaTreaderMetadata : CreatureMetadata
{
    [DataMember(Order = 1)]
    public bool ReverseDirection { get; }

    [DataMember(Order = 2)]
    public float GrazingEndTime { get; }

    [IgnoreConstructor]
    protected SeaTreaderMetadata()
    {
        // Constructor for serialization. Has to be "protected" for json serialization.
    }

    /// <param name="leashPosition">The point of its path it walks to, kept on the base like every creature's leash.</param>
    public SeaTreaderMetadata(bool reverseDirection, float grazingEndTime, NitroxVector3 leashPosition, float infectedAmount, float enzymeEndTime = NO_ENZYME, bool hasLeash = true,
                              bool hasSize = false, float size = 0f) : base(infectedAmount, enzymeEndTime, hasLeash, leashPosition, hasSize, size)
    {
        ReverseDirection = reverseDirection;
        GrazingEndTime = grazingEndTime;
    }

    public override string ToString()
    {
        return $"[{nameof(SeaTreaderMetadata)} ReverseDirection: {ReverseDirection}, GrazingEndTime: {GrazingEndTime}, {base.ToString()}]";
    }
}
