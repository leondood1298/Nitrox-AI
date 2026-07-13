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

	[IgnoreConstructor]
	protected PowerSourceMetadata()
	{
	}

	// Keep a single public constructor so Newtonsoft.Json can reliably select it.
	// The optional values preserve compatibility with saves that only contain Power.
	public PowerSourceMetadata(float power, float maxPower = 0f, BasePowerSourceType sourceType = BasePowerSourceType.UNKNOWN, long revision = 0, float fuelConsumed = 0f)
	{
		Power = power;
		MaxPower = maxPower;
		SourceType = sourceType;
		Revision = revision;
		FuelConsumed = fuelConsumed;
	}

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
	public const float BIOREACTOR_MAX_FUEL_PROGRESS = 840f;
	public const float NUCLEAR_MAX_FUEL_PROGRESS = 20000f;

	public static bool TryGetMaxPower(BasePowerSourceType sourceType, out float maxPower)
	{
		maxPower = sourceType switch
		{
			BasePowerSourceType.SOLAR => 75f,
			BasePowerSourceType.THERMAL => 250f,
			BasePowerSourceType.BIOREACTOR => 500f,
			BasePowerSourceType.NUCLEAR => 2500f,
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




