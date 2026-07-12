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

    [DataMember(Order = 3)]
    public bool LightOn { get; set; }

    [DataMember(Order = 4)]
    public long LightRevision { get; set; }

    [DataMember(Order = 5)]
    public float Energy { get; set; }

    [DataMember(Order = 6)]
    public float Health { get; set; }

    [DataMember(Order = 7)]
    public long ComponentRevision { get; set; }

    [IgnoreConstructor]
    protected MapRoomCameraRecord()
    {
    }

    public MapRoomCameraRecord(NitroxId cameraId, int cameraNumber, bool lightOn = false, long lightRevision = 0, float energy = 100f, float health = 100f, long componentRevision = 0)
    {
        CameraId = cameraId;
        CameraNumber = cameraNumber;
        LightOn = lightOn;
        LightRevision = lightRevision;
        Energy = energy;
        Health = health;
        ComponentRevision = componentRevision;
    }
}
