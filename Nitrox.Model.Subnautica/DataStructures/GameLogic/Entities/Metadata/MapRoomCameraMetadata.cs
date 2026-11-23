using System;
using System.Runtime.Serialization;
using BinaryPack.Attributes;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Bases;

namespace Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;

/// <summary>
///     Light, battery and health of a Scanner Room camera that is not registered with any Scanner Room: one that was
///     fabricated and never docked, was picked up and dropped again, or outlived the room it belonged to. A registered
///     camera keeps this state in its room's <see cref="MapRoomCameraRecord" /> instead, and the server moves it between
///     the two when a camera is first docked or its room is deconstructed.
/// </summary>
[Serializable]
[DataContract]
public class MapRoomCameraMetadata : EntityMetadata
{
    [DataMember(Order = 1)]
    public bool LightOn { get; }

    [DataMember(Order = 2)]
    public long LightRevision { get; }

    [DataMember(Order = 3)]
    public float Energy { get; }

    [DataMember(Order = 4)]
    public float Health { get; }

    [DataMember(Order = 5)]
    public long ComponentRevision { get; }

    [IgnoreConstructor]
    protected MapRoomCameraMetadata()
    {
        // Constructor for serialization. Has to be "protected" for json serialization.
    }

    public MapRoomCameraMetadata(bool lightOn = false, long lightRevision = 0, float energy = MapRoomCameraRecord.MAX_ENERGY,
        float health = MapRoomCameraRecord.MAX_HEALTH, long componentRevision = 0)
    {
        LightOn = lightOn;
        LightRevision = lightRevision;
        Energy = energy;
        Health = health;
        ComponentRevision = componentRevision;
    }

    public override string ToString()
    {
        return $"[{nameof(MapRoomCameraMetadata)} LightOn: {LightOn} (rev {LightRevision}), Energy: {Energy}, Health: {Health} (rev {ComponentRevision})]";
    }
}
