using System;
using System.Runtime.Serialization;
using BinaryPack.Attributes;
using Nitrox.Model.DataStructures.Unity;

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
[ProtoInclude(54, typeof(SeaEmperorBabyMetadata))]
[ProtoInclude(55, typeof(SandSharkMetadata))]
[ProtoInclude(56, typeof(StalkerMetadata))]
[ProtoInclude(57, typeof(CuteFishMetadata))]
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

    /// <summary>
    ///     The creature carries no enzyme, as far as anybody has decided.
    /// </summary>
    public const float NO_ENZYME = 0f;

    /// <summary>
    ///     Enzyme that does not run out: a hero peeper in the prison's aquarium keeps its enzyme (Peeper.Update).
    /// </summary>
    public const float LASTING_ENZYME = float.MaxValue;

    /// <summary>
    ///     The game time (DayNightCycle.timePassed) at which a hero peeper's enzyme (Peeper.enzymeAmount) runs out, as decided
    ///     by its simulating player, <see cref="LASTING_ENZYME" /> or <see cref="NO_ENZYME" />. A later time always wins, so
    ///     an update that does not know about the enzyme cannot take it away.
    /// </summary>
    [DataMember(Order = 2)]
    public float EnzymeEndTime { get; set; }

    /// <summary>
    ///     Whether <see cref="LeashPosition" /> is known. The server seeds it where it spawns a creature; an update that does
    ///     not carry it leaves the stored one.
    /// </summary>
    [DataMember(Order = 3)]
    public bool HasLeash { get; set; }

    /// <summary>
    ///     Creature.leashPosition: the place the creature stays around and returns to. The game saves it; without it every
    ///     game that loads the creature took wherever it was then as its home.
    /// </summary>
    [DataMember(Order = 4)]
    public NitroxVector3 LeashPosition { get; set; }

    /// <summary>
    ///     Whether <see cref="Size" /> is known.
    /// </summary>
    [DataMember(Order = 5)]
    public bool HasSize { get; set; }

    /// <summary>
    ///     Creature.Size (0 to 1), which Creature.Start rolls for many creatures. Rolled on each game, a creature was a
    ///     different size for every player and after every load.
    /// </summary>
    [DataMember(Order = 6)]
    public float Size { get; set; }

    [IgnoreConstructor]
    protected CreatureMetadata()
    {
        // Constructor for serialization. Has to be "protected" for json serialization.
    }

    /// <remarks>
    ///     BinaryPack needs a constructor with a parameter for every member, hence the leash and size parameters of every
    ///     creature metadata's constructor.
    /// </remarks>
    protected CreatureMetadata(float infectedAmount, float enzymeEndTime = NO_ENZYME, bool hasLeash = false, NitroxVector3 leashPosition = default, bool hasSize = false,
                               float size = 0f)
    {
        InfectedAmount = infectedAmount;
        EnzymeEndTime = enzymeEndTime;
        HasLeash = hasLeash;
        LeashPosition = leashPosition;
        HasSize = hasSize;
        Size = size;
    }

    public static bool IsDecided(float infectedAmount)
    {
        return infectedAmount >= 0f;
    }

    public CreatureMetadata WithLeash(NitroxVector3 leashPosition)
    {
        HasLeash = true;
        LeashPosition = leashPosition;
        return this;
    }

    public CreatureMetadata WithSize(float size)
    {
        HasSize = true;
        Size = size;
        return this;
    }

    /// <summary>
    ///     Keeps what <paramref name="stored" /> knows of the leash and size where this update knows nothing.
    /// </summary>
    public void KeepKnownBodyFrom(CreatureMetadata stored)
    {
        if (!HasLeash && stored.HasLeash)
        {
            WithLeash(stored.LeashPosition);
        }
        if (!HasSize && stored.HasSize)
        {
            WithSize(stored.Size);
        }
    }

    public override string ToString()
    {
        return $"InfectedAmount: {InfectedAmount}, EnzymeEndTime: {EnzymeEndTime}, Leash: {(HasLeash ? LeashPosition.ToString() : "unknown")}, Size: {(HasSize ? Size.ToString() : "unknown")}";
    }
}
