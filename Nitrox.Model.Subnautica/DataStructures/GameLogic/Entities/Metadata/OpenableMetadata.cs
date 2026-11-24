using System;
using System.Runtime.Serialization;
using BinaryPack.Attributes;

namespace Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;

/// <summary>
///     Whether a door that swings open (the Cyclops' doors and hatches) is open. The server keeps the last state any player
///     sent, so that a player who joins sees the doors as the others do.
/// </summary>
[Serializable]
[DataContract]
public class OpenableMetadata : EntityMetadata
{
    [DataMember(Order = 1)]
    public bool IsOpen { get; }

    [IgnoreConstructor]
    protected OpenableMetadata()
    {
        // Constructor for serialization. Has to be "protected" for json serialization.
    }

    public OpenableMetadata(bool isOpen)
    {
        IsOpen = isOpen;
    }

    public override string ToString()
    {
        return $"[OpenableMetadata IsOpen: {IsOpen}]";
    }
}
