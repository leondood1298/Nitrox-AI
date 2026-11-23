using System;
using System.Runtime.Serialization;
using BinaryPack.Attributes;

namespace Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;

/// <summary>
///     Whether a supply crate is open. The item inside is an entity of its own, a child of the crate's entity.
/// </summary>
/// <remarks>
///     A crate that has to be laser cut open carries its <c>Sealed</c> on the same object, so the cut state shares this
///     metadata: an entity has one metadata slot, and two kinds sent for one crate would keep replacing each other.
/// </remarks>
[Serializable]
[DataContract]
public class SupplyCrateMetadata : EntityMetadata
{
    [DataMember(Order = 1)]
    public bool Open { get; }

    /// <summary>
    ///     Whether the crate still has to be cut open. Only used by crates that can be cut.
    /// </summary>
    [DataMember(Order = 2)]
    public bool Sealed { get; }

    /// <summary>
    ///     How far the cutting has gone. Only used by crates that can be cut.
    /// </summary>
    [DataMember(Order = 3)]
    public float OpenedAmount { get; }

    [IgnoreConstructor]
    protected SupplyCrateMetadata()
    {
        //Constructor for serialization. Has to be "protected" for json serialization.
    }

    public SupplyCrateMetadata(bool open, bool @sealed, float openedAmount)
    {
        Open = open;
        Sealed = @sealed;
        OpenedAmount = openedAmount;
    }

    public override string ToString()
    {
        return $"[{nameof(SupplyCrateMetadata)} Open: {Open}, Sealed: {Sealed}, OpenedAmount: {OpenedAmount}]";
    }
}
