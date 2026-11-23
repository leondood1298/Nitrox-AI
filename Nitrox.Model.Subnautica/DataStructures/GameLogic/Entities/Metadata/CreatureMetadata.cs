using System;
using System.Runtime.Serialization;
using BinaryPack.Attributes;

namespace Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;

/// <summary>
///     Lasting state of a living creature. A creature has one metadata slot, so its infection (InfectedMixin) is carried by
///     whichever creature metadata it has: the server merges an infection update into the stored one.
/// </summary>
[Serializable, DataContract]
[ProtoInclude(50, typeof(WaterParkCreatureMetadata))]
[ProtoInclude(51, typeof(SeaTreaderMetadata))]
[ProtoInclude(52, typeof(StayAtLeashPositionMetadata))]
[ProtoInclude(53, typeof(CreatureInfectionMetadata))]
[ProtoInclude(55, typeof(SandSharkMetadata))]
public abstract class CreatureMetadata : EntityMetadata
{
    /// <summary>
    ///     Nobody has decided this creature's infection yet: the player who simulates it rolls it from the creature's id.
    /// </summary>
    public const float UNDECIDED = -1f;

    /// <summary>
    ///     InfectedMixin.infectedAmount (0 to 1) as decided by the creature's simulating player, or <see cref="UNDECIDED" />.
    ///     Settable because the server merges an infection update into the creature metadata it stores.
    /// </summary>
    [DataMember(Order = 1)]
    public float InfectedAmount { get; set; }

    [IgnoreConstructor]
    protected CreatureMetadata()
    {
        // Constructor for serialization. Has to be "protected" for json serialization.
    }

    protected CreatureMetadata(float infectedAmount)
    {
        InfectedAmount = infectedAmount;
    }

    public static bool IsDecided(float infectedAmount)
    {
        return infectedAmount >= 0f;
    }

    public override string ToString()
    {
        return $"InfectedAmount: {InfectedAmount}";
    }
}
