using System;
using System.Runtime.Serialization;
using BinaryPack.Attributes;

namespace Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;

/// <summary>
///     A medical cabinet's door, its medkit and when the next medkit appears, in the game time of
///     <c>DayNightCycle.timePassed</c>.
/// </summary>
[Serializable]
[DataContract]
public class MedicalCabinetMetadata : EntityMetadata
{
    [DataMember(Order = 1)]
    public bool DoorOpen { get; }

    [DataMember(Order = 2)]
    public bool HasMedKit { get; }

    [DataMember(Order = 3)]
    public float TimeSpawnMedKit { get; }

    [IgnoreConstructor]
    protected MedicalCabinetMetadata()
    {
        //Constructor for serialization. Has to be "protected" for json serialization.
    }

    public MedicalCabinetMetadata(bool doorOpen, bool hasMedKit, float timeSpawnMedKit)
    {
        DoorOpen = doorOpen;
        HasMedKit = hasMedKit;
        TimeSpawnMedKit = timeSpawnMedKit;
    }

    public override string ToString()
    {
        return $"[{nameof(MedicalCabinetMetadata)} DoorOpen: {DoorOpen}, HasMedKit: {HasMedKit}, TimeSpawnMedKit: {TimeSpawnMedKit}]";
    }
}
