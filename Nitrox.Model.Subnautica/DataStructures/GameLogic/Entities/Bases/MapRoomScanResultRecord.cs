using System;
using System.Runtime.Serialization;
using BinaryPack.Attributes;
using Nitrox.Model.DataStructures.Unity;

namespace Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Bases;

[Serializable, DataContract]
public class MapRoomScanResultRecord
{
    [DataMember(Order = 1)]
    public string ResourceId { get; set; }

    [DataMember(Order = 2)]
    public NitroxTechType TechType { get; set; }

    [DataMember(Order = 3)]
    public NitroxVector3 Position { get; set; }

    [IgnoreConstructor]
    protected MapRoomScanResultRecord()
    {
    }

    public MapRoomScanResultRecord(string resourceId, NitroxTechType techType, NitroxVector3 position)
    {
        ResourceId = resourceId;
        TechType = techType;
        Position = position;
    }
}
