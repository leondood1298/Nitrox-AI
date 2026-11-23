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
    [DataMember(Order = 2)]
    public float MaxPower { get; }
    [DataMember(Order = 3)]
    public BasePowerSourceType SourceType { get; }
    [DataMember(Order = 4)]
    public long Revision { get; }
    [DataMember(Order = 5)]
    public float FuelConsumed { get; }

    /// <summary>
    ///     Highest water temperature a thermal plant has measured (the game only ever raises it). Kept here because an
    ///     entity has a single metadata slot, which this power state already occupies. Zero for other source types.
    /// </summary>
    [DataMember(Order = 6)]
    public float Temperature { get; }

    [IgnoreConstructor]
    protected PowerSourceMetadata()
    {
    }

    // Keep a single public constructor so Newtonsoft.Json can reliably select it.
    // The optional values preserve compatibility with saves that only contain Power.
    public PowerSourceMetadata(float power, float maxPower = 0f, BasePowerSourceType sourceType = BasePowerSourceType.UNKNOWN, long revision = 0, float fuelConsumed = 0f, float temperature = 0f)
    {
        Power = power;
        MaxPower = maxPower;
        SourceType = sourceType;
        Revision = revision;
        FuelConsumed = fuelConsumed;
        Temperature = temperature;
    }

    public PowerSourceMetadata WithTemperature(float temperature) => new(Power, MaxPower, SourceType, Revision, FuelConsumed, temperature);

    public override string ToString()
    {
        return $"[PowerSourceMetadata Type: {SourceType}, Power: {Power}/{MaxPower}, FuelConsumed: {FuelConsumed}, Revision: {Revision}]";
    }
}

public enum BasePowerSourceType
{
    UNKNOWN,
    SOLAR,
    THERMAL,
    BIOREACTOR,
    NUCLEAR
}

public static class BasePowerSourceTypes
{
    private const float SOLAR_MAX_POWER = 75f;
    private const float THERMAL_MAX_POWER = 250f;
    private const float BIOREACTOR_MAX_POWER = 500f;
    public const float NUCLEAR_MAX_POWER = 2500f;
    public const float BIOREACTOR_MAX_FUEL_PROGRESS = 840f;
    public const float NUCLEAR_MAX_FUEL_PROGRESS = 20000f;

    public static bool TryGetMaxPower(BasePowerSourceType sourceType, out float maxPower)
    {
        maxPower = sourceType switch
        {
            BasePowerSourceType.SOLAR => SOLAR_MAX_POWER,
            BasePowerSourceType.THERMAL => THERMAL_MAX_POWER,
            BasePowerSourceType.BIOREACTOR => BIOREACTOR_MAX_POWER,
            BasePowerSourceType.NUCLEAR => NUCLEAR_MAX_POWER,
            _ => 0f
        };
        return maxPower > 0f;
    }

    public static bool TryGetMaxFuelProgress(BasePowerSourceType sourceType, out float maxFuelProgress)
    {
        maxFuelProgress = sourceType switch
        {
            BasePowerSourceType.BIOREACTOR => BIOREACTOR_MAX_FUEL_PROGRESS,
            BasePowerSourceType.NUCLEAR => NUCLEAR_MAX_FUEL_PROGRESS,
            _ => 0f
        };
        return maxFuelProgress > 0f;
    }
}
