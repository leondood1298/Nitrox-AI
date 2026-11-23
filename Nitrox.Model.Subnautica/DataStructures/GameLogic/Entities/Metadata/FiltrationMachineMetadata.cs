using System;
using System.Runtime.Serialization;
using BinaryPack.Attributes;

namespace Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;

/// <summary>
///     The two timers of a base's water filtration machine (FiltrationMachine.timeRemainingWater and timeRemainingSalt):
///     the seconds left until the next bottle of water and the next lump of salt. A timer that does not run is
///     <see cref="IDLE" />. The bottles and salt it made are inventory items of the machine's entity, not part of this.
/// </summary>
[Serializable]
[DataContract]
public class FiltrationMachineMetadata : EntityMetadata
{
    /// <summary>
    ///     What the game sets a timer to when it stops it (FiltrationMachine's constructor and UpdateFiltering).
    /// </summary>
    public const float IDLE = -1f;

    /// <summary>
    ///     What FiltrationMachine.TryFilterWater starts the water timer at.
    /// </summary>
    public const float WATER_SECONDS = 840f;

    /// <summary>
    ///     What FiltrationMachine.TryFilterSalt starts the salt timer at.
    /// </summary>
    public const float SALT_SECONDS = 420f;

    [DataMember(Order = 1)]
    public float TimeRemainingWater { get; }

    [DataMember(Order = 2)]
    public float TimeRemainingSalt { get; }

    [IgnoreConstructor]
    protected FiltrationMachineMetadata()
    {
        //Constructor for serialization. Has to be "protected" for json serialization.
    }

    public FiltrationMachineMetadata(float timeRemainingWater, float timeRemainingSalt)
    {
        TimeRemainingWater = timeRemainingWater;
        TimeRemainingSalt = timeRemainingSalt;
    }

    public override string ToString()
    {
        return $"[{nameof(FiltrationMachineMetadata)} TimeRemainingWater: {TimeRemainingWater}, TimeRemainingSalt: {TimeRemainingSalt}]";
    }
}
