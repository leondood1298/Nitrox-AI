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

	[IgnoreConstructor]
	protected PowerSourceMetadata()
	{
	}

	// Keep a single public constructor so Newtonsoft.Json can reliably select it.
	// The optional values preserve compatibility with saves that only contain Power.
	public PowerSourceMetadata(float power, float maxPower = 0f, BasePowerSourceType sourceType = BasePowerSourceType.UNKNOWN, long revision = 0)
	{
		Power = power;
		MaxPower = maxPower;
		SourceType = sourceType;
		Revision = revision;
	}

	public override string ToString()
	{
		return $"[PowerSourceMetadata Type: {SourceType}, Power: {Power}/{MaxPower}, Revision: {Revision}]";
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
}




