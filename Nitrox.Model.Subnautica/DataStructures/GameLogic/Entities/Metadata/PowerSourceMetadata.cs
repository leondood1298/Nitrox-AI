using System;
using System.Runtime.Serialization;
using BinaryPack.Attributes;

namespace Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;

[Serializable]
[DataContract]
public class PowerSourceMetadata : EntityMetadata
{
    [DataMember(Order = 1)]
    public float Power { get; }

    /// <summary>
    ///     How much of the fuel item in use a reactor has burned (the game's _toConsume). Zero for other source types.
    /// </summary>
    [DataMember(Order = 2)]
    public float FuelConsumed { get; }

    /// <summary>
    ///     Says which capacity and fuel limits apply (see <see cref="BasePowerSourceTypes" />).
    /// </summary>
    [DataMember(Order = 3)]
    public BasePowerSourceType SourceType { get; }

    /// <summary>
    ///     Highest water temperature a thermal plant has measured (the game only ever raises it). Kept here because an
    ///     entity has a single metadata slot, which this power state already occupies. Zero for other source types.
    /// </summary>
    [DataMember(Order = 4)]
    public float Temperature { get; }

    [IgnoreConstructor]
    protected PowerSourceMetadata()
    {
    }

    // Keep a single public constructor so Newtonsoft.Json can reliably select it.
    public PowerSourceMetadata(float power, float fuelConsumed, BasePowerSourceType sourceType, float temperature = 0f)
    {
        Power = power;
        FuelConsumed = fuelConsumed;
        SourceType = sourceType;
        Temperature = temperature;
    }

    public PowerSourceMetadata WithPower(float power) => new(power, FuelConsumed, SourceType, Temperature);

    public PowerSourceMetadata WithTemperature(float temperature) => new(Power, FuelConsumed, SourceType, temperature);

    public override string ToString()
    {
        return $"[PowerSourceMetadata Type: {SourceType}, Power: {Power}, FuelConsumed: {FuelConsumed}, Temperature: {Temperature}]";
    }
}
