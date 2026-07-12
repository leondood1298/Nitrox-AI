using System;
using System.Runtime.Serialization;
using BinaryPack.Attributes;
using Nitrox.Model.DataStructures;

namespace Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Bases;

[Serializable, DataContract]
public class MapRoomCameraRecord
{
    [DataMember(Order = 1)]
    public NitroxId CameraId { get; set; }

    [DataMember(Order = 2)]
    public int CameraNumber { get; set; }

    [IgnoreConstructor]
    protected MapRoomCameraRecord()
    {
    }

    public MapRoomCameraRecord(NitroxId cameraId, int cameraNumber)
    {
        CameraId = cameraId;
        CameraNumber = cameraNumber;
    }
}
