using System;
using System.Runtime.Serialization;
using BinaryPack.Attributes;

namespace Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;

/// <summary>
///     A Sea Emperor baby that appeared in the world after leaving the prison (SeaEmperorBabiesSpawner): its size and how many
///     cure balls it has dropped (DropEnzymes), so that it drops no more than the game allows whoever simulates it.
/// </summary>
[Serializable, DataContract]
public class SeaEmperorBabyMetadata : CreatureMetadata
{
    [DataMember(Order = 1)]
    public float Scale { get; }

    /// <summary>
    ///     DropEnzymes.spawnCount
    /// </summary>
    [DataMember(Order = 2)]
    public int DropCount { get; }

    /// <summary>
    ///     DropEnzymes.spawnLimit
    /// </summary>
    [DataMember(Order = 3)]
    public int DropLimit { get; }

    [IgnoreConstructor]
    protected SeaEmperorBabyMetadata()
    {
        // Constructor for serialization. Has to be "protected" for json serialization.
    }

    public SeaEmperorBabyMetadata(float scale, int dropCount, int dropLimit, float infectedAmount, float enzymeEndTime = NO_ENZYME, bool hasLeash = false,
                                  Nitrox.Model.DataStructures.Unity.NitroxVector3 leashPosition = default, bool hasSize = false, float size = 0f)
        : base(infectedAmount, enzymeEndTime, hasLeash, leashPosition, hasSize, size)
    {
        Scale = scale;
        DropCount = dropCount;
        DropLimit = dropLimit;
    }

    public override string ToString()
    {
        return $"[{nameof(SeaEmperorBabyMetadata)} Scale: {Scale}, DropCount: {DropCount}, DropLimit: {DropLimit}, {base.ToString()}]";
    }
}
