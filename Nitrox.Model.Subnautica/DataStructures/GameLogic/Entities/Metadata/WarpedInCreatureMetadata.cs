using System;
using System.Runtime.Serialization;
using BinaryPack.Attributes;
using Nitrox.Model.DataStructures.Unity;

namespace Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;

/// <summary>
///     A creature a Warper's ball brought in (WarpedInCreature), which warps out again when its time is up. The time travels
///     with the creature so that whoever simulates it ends it then, and a creature whose time passed while nobody had it
///     loaded is gone when it loads again.
/// </summary>
[Serializable, DataContract]
public class WarpedInCreatureMetadata : CreatureMetadata
{
    /// <summary>
    ///     When it warps out, in game time (DayNightCycle.timePassed).
    /// </summary>
    [DataMember(Order = 1)]
    public double WarpOutTime { get; }

    [IgnoreConstructor]
    protected WarpedInCreatureMetadata()
    {
        // Constructor for serialization. Has to be "protected" for json serialization.
    }

    public WarpedInCreatureMetadata(double warpOutTime, float infectedAmount, float enzymeEndTime = NO_ENZYME, bool hasLeash = false, NitroxVector3 leashPosition = default,
                                    bool hasSize = false, float size = 0f)
        : base(infectedAmount, enzymeEndTime, hasLeash, leashPosition, hasSize, size)
    {
        WarpOutTime = warpOutTime;
    }

    public override string ToString()
    {
        return $"[{nameof(WarpedInCreatureMetadata)} WarpOutTime: {WarpOutTime}, {base.ToString()}]";
    }
}
