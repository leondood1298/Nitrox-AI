using System;
using System.Runtime.Serialization;
using BinaryPack.Attributes;

namespace Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;

/// <summary>
///     The infection of a creature that has no other lasting state, and what a client sends when a creature's infection
///     changes (the server sets it on the creature metadata it already stores).
/// </summary>
[Serializable, DataContract]
public class CreatureInfectionMetadata : CreatureMetadata
{
    [IgnoreConstructor]
    protected CreatureInfectionMetadata()
    {
        // Constructor for serialization. Has to be "protected" for json serialization.
    }

    public CreatureInfectionMetadata(float infectedAmount) : base(infectedAmount)
    {
    }

    public override string ToString()
    {
        return $"[{nameof(CreatureInfectionMetadata)} {base.ToString()}]";
    }
}
