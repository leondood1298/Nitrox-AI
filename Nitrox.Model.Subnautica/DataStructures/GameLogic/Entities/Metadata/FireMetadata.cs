using System;
using System.Runtime.Serialization;
using BinaryPack.Attributes;

namespace Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;

/// <summary>
///     The strength a fire has left after it was sprayed without being put out. A fire that is put out is removed
///     instead.
/// </summary>
[Serializable]
[DataContract]
public class FireMetadata : EntityMetadata
{
    [DataMember(Order = 1)]
    public float Health { get; }

    [IgnoreConstructor]
    protected FireMetadata()
    {
        // Constructor for serialization. Has to be "protected" for json serialization.
    }

    public FireMetadata(float health)
    {
        Health = health;
    }

    public override string ToString()
    {
        return $"[{nameof(FireMetadata)} Health: {Health}]";
    }
}
