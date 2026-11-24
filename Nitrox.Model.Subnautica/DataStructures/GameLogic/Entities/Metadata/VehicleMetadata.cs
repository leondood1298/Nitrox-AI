using System;
using System.Runtime.Serialization;
using BinaryPack.Attributes;
using Nitrox.Model.DataStructures.Unity;

namespace Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;

[Serializable, DataContract]
[ProtoInclude(50, typeof(SeamothMetadata))]
[ProtoInclude(60, typeof(ExosuitMetadata))]
public abstract class VehicleMetadata : NamedColoredMetadata
{
    [DataMember(Order = 1)]
    public float Health { get; init; }

    [DataMember(Order = 2)]
    public bool InPrecursor { get; init; }

    /// <summary>
    ///     Counts the renames and recolours. A player who renames sends one more than the last one it knows of, so the
    ///     server can tell a rename from a report of the simulating player that still carries the former name.
    /// </summary>
    [DataMember(Order = 3)]
    public int NameVersion { get; init; }

    [IgnoreConstructor]
    protected VehicleMetadata()
    {
        // Constructor for serialization. Has to be "protected" for json serialization.
    }

    public VehicleMetadata(float health, bool inPrecusor, string name, NitroxVector3[] colors, int nameVersion) : base(name, colors)
    {
        Health = health;
        InPrecursor = inPrecusor;
        NameVersion = nameVersion;
    }

    public override string ToString()
    {
        return $"[{nameof(VehicleMetadata)} Health: {Health}, InPrecursor: {InPrecursor}, NameVersion: {NameVersion}, {base.ToString()}]";
    }
}
