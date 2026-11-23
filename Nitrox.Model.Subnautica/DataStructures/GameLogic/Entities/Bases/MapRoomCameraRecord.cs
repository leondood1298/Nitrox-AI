using System;
using System.Runtime.Serialization;
using BinaryPack.Attributes;
using Nitrox.Model.DataStructures;

namespace Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Bases;

[Serializable, DataContract]
public class MapRoomCameraRecord
{
    public const float MAX_ENERGY = 100f;
    public const float MAX_HEALTH = 400f;

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

    public MapRoomCameraRecord(NitroxId cameraId, int cameraNumber, bool lightOn = false, long lightRevision = 0,
        float energy = MAX_ENERGY, float health = MAX_HEALTH, long componentRevision = 0)
    {
        CameraId = cameraId;
        CameraNumber = cameraNumber;
        LightOn = lightOn;
        LightRevision = lightRevision;
        Energy = energy;
        Health = health;
        ComponentRevision = componentRevision;
    }

    /// <summary>
    ///     Coarse percentage band (0, 10, 25, 50, 75 or 100) of a camera's energy or health relative to its maximum.
    ///     Server and client report a component change only when it crosses into another band.
    /// </summary>
    public static int GetComponentBand(float value, float maximum) => value switch
    {
        <= 0f => 0,
        _ when value <= maximum * 0.1f => 10,
        _ when value <= maximum * 0.25f => 25,
        _ when value <= maximum * 0.5f => 50,
        _ when value <= maximum * 0.75f => 75,
        _ => 100
    };
}
