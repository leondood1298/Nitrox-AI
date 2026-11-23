using System;
using System.Runtime.Serialization;
using BinaryPack.Attributes;

namespace Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;

/// <summary>
///     The infection and enzyme of a creature that has no other lasting state, and what a client sends when a creature's
///     infection or enzyme changes (the server sets them on the creature metadata it already stores).
/// </summary>
[Serializable, DataContract]
public class CreatureInfectionMetadata : CreatureMetadata
{
    [IgnoreConstructor]
    protected CreatureInfectionMetadata()
    {
        // Constructor for serialization. Has to be "protected" for json serialization.
    }

    public CreatureInfectionMetadata(float infectedAmount, float enzymeEndTime = NO_ENZYME) : base(infectedAmount, enzymeEndTime)
    {
    }

    public override string ToString()
    {
        return $"[{nameof(CreatureInfectionMetadata)} {base.ToString()}]";
    }
}
