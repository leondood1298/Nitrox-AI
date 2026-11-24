using System;
using System.Runtime.Serialization;
using BinaryPack.Attributes;
using Nitrox.Model.DataStructures;

namespace Nitrox.Model.Subnautica.DataStructures.GameLogic;

/// <summary>
///     Where a new item came from when a game made it from something in the world: a medical cabinet's medkit, a fruit of
///     a plant, a fabricator's result, a time capsule's items, the resources of a broken outcrop. Every game that takes
///     from the same source at the same moment makes its own item with its own id; the server gives the item to the
///     first of them and takes it back from the others.
/// </summary>
[Serializable]
[DataContract]
public class ItemSource
{
    /// <summary>
    ///     The entity the item came from.
    /// </summary>
    [DataMember(Order = 1)]
    public NitroxId SourceId { get; set; }

    /// <summary>
    ///     What was taken from the source, the same in every game that takes the same thing (for example the index of a
    ///     fruit on its plant). Two games taking different things from one source do not compete.
    /// </summary>
    [DataMember(Order = 2)]
    public string Token { get; set; }

    [IgnoreConstructor]
    protected ItemSource()
    {
        // Constructor for serialization. Has to be "protected" for json serialization.
    }

    public ItemSource(NitroxId sourceId, string token)
    {
        SourceId = sourceId;
        Token = token;
    }

    public override string ToString()
    {
        return $"[{nameof(ItemSource)} {SourceId} {Token}]";
    }
}
