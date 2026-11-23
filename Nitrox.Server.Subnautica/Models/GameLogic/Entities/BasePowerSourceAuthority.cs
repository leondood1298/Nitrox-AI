using System.Collections.Generic;
using Nitrox.Model.Core;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Bases;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;
using Nitrox.Model.Subnautica.Packets;

namespace Nitrox.Server.Subnautica.Models.GameLogic.Entities;

internal sealed class BasePowerSourceAuthority
{
    private const float POWER_TOLERANCE = 0.01f;

    /// <summary>
    ///     Upper bound for one consumption request. Clients batch a fraction of a second of use, so anything near the
    ///     largest source capacity (a nuclear reactor) is not a real request.
    /// </summary>
    internal const float MAX_CONSUMPTION_PER_REQUEST = 2500f;
    private readonly Dictionary<NitroxId, OwnerSequence> lastOwnerSequenceBySource = [];

    public bool TryApply(Entity entity, SessionId senderSessionId, BasePowerSourceUpdate requested, out PowerSourceMetadata accepted, out string rejectionReason)
    {
        lock (lastOwnerSequenceBySource)
        {
            accepted = CanonicalOrFallback(entity, requested);
            rejectionReason = Validate(entity, senderSessionId, requested);
            if (rejectionReason.Length > 0)
            {
                return false;
            }

            BasePowerSourceTypes.TryGetMaxPower(requested.SourceType, out float maxPower);
            float power = Math.Clamp(requested.Power, 0f, maxPower);
			BasePowerSourceTypes.TryGetMaxFuelProgress(requested.SourceType, out float maxFuelProgress);
			float fuelConsumed = Math.Clamp(requested.FuelConsumed, 0f, maxFuelProgress);
            long revision = entity.Metadata is PowerSourceMetadata current ? current.Revision + 1 : 1;
            // The thermal plant temperature shares this metadata slot and must survive every power update.
            accepted = new PowerSourceMetadata(power, maxPower, requested.SourceType, revision, fuelConsumed, GetStoredTemperature(entity.Metadata));
            entity.Metadata = accepted;
            lastOwnerSequenceBySource[entity.Id] = new OwnerSequence(senderSessionId, requested.ClientSequence);
            return true;
        }
    }

    /// <summary>
    ///     Folds a thermal plant temperature report into a source that already has power state, instead of letting it
    ///     replace that state (which would reset the revision clients order updates by, and make the plant look like
    ///     it has no saved power). The temperature only ever rises, so an equal or lower report is stale.
    /// </summary>
    /// <returns>False when the entity has no power state yet or the report is stale.</returns>
    public bool TryApplyTemperature(Entity entity, ThermalPlantMetadata report)
    {
        lock (lastOwnerSequenceBySource)
        {
            if (entity.Metadata is not PowerSourceMetadata current || !float.IsFinite(report.Temperature) || report.Temperature <= current.Temperature)
            {
                return false;
            }
            entity.Metadata = current.WithTemperature(report.Temperature);
            return true;
        }
    }

    /// <summary>
    ///     Decides what to do with energy that <paramref name="consumerSessionId" /> took from a source it does not
    ///     simulate. Only the simulation owner's state is authoritative, so the request normally goes to that owner to
    ///     apply. When nobody simulates the source, the server applies it to the stored state itself.
    /// </summary>
    public ConsumptionRoute RouteConsumption(Entity entity, SessionId? ownerSessionId, SessionId consumerSessionId, float amount,
        out PowerSourceMetadata accepted, out string rejectionReason)
    {
        lock (lastOwnerSequenceBySource)
        {
            accepted = entity.Metadata as PowerSourceMetadata ?? new PowerSourceMetadata(0f);
            rejectionReason = "";
            if (entity is not (ModuleEntity or InteriorPieceEntity))
            {
                rejectionReason = $"{entity.GetType().Name} is not a base power source";
                return ConsumptionRoute.REJECTED;
            }
            if (!float.IsFinite(amount) || amount <= 0f || amount > MAX_CONSUMPTION_PER_REQUEST)
            {
                rejectionReason = $"amount {amount} is outside 0-{MAX_CONSUMPTION_PER_REQUEST}";
                return ConsumptionRoute.REJECTED;
            }
            if (ownerSessionId.HasValue)
            {
                if (ownerSessionId.Value == consumerSessionId)
                {
                    // The owner's own consumption is already part of what it simulates and broadcasts.
                    rejectionReason = "sender simulates this source itself";
                    return ConsumptionRoute.REJECTED;
                }
                return ConsumptionRoute.FORWARD_TO_OWNER;
            }
            if (entity.Metadata is not PowerSourceMetadata current)
            {
                rejectionReason = "source has no stored power state and no simulation owner";
                return ConsumptionRoute.REJECTED;
            }

            accepted = new PowerSourceMetadata(Math.Max(0f, current.Power - amount), current.MaxPower, current.SourceType,
                current.Revision + 1, current.FuelConsumed, current.Temperature);
            entity.Metadata = accepted;
            return ConsumptionRoute.APPLIED_BY_SERVER;
        }
    }

    internal static float GetStoredTemperature(EntityMetadata? metadata) => metadata switch
    {
        PowerSourceMetadata powerSource => powerSource.Temperature,
        ThermalPlantMetadata thermalPlant => thermalPlant.Temperature,
        _ => 0f
    };

    public long GetLastClientSequence(NitroxId sourceId)
    {
        lock (lastOwnerSequenceBySource)
        {
            return lastOwnerSequenceBySource.TryGetValue(sourceId, out OwnerSequence state) ? state.Sequence : 0;
        }
    }

    private string Validate(Entity entity, SessionId senderSessionId, BasePowerSourceUpdate requested)
    {
        if (requested.IsServerResponse)
        {
            return "client sent a server response";
        }
        if (requested.SourceId != entity.Id)
        {
            return "source id mismatch";
        }
        if (!BasePowerSourceTypes.TryGetMaxPower(requested.SourceType, out float maxPower))
        {
            return "unknown source type";
        }
        if (!IsCompatibleEntity(entity, requested.SourceType))
        {
            return $"source type {requested.SourceType} is incompatible with {entity.GetType().Name}";
        }
		if (!HasCompatibleTechType(entity, requested.SourceType))
		{
			return $"source type {requested.SourceType} does not match tech type {entity.TechType}";
		}
        if (float.IsNaN(requested.Power) || float.IsInfinity(requested.Power) || requested.Power < -POWER_TOLERANCE || requested.Power > maxPower + POWER_TOLERANCE)
        {
            return $"power {requested.Power} is outside 0-{maxPower}";
        }
		BasePowerSourceTypes.TryGetMaxFuelProgress(requested.SourceType, out float maxFuelProgress);
		if (float.IsNaN(requested.FuelConsumed) || float.IsInfinity(requested.FuelConsumed) || requested.FuelConsumed < -POWER_TOLERANCE || requested.FuelConsumed > maxFuelProgress + POWER_TOLERANCE)
		{
			return $"fuel progress {requested.FuelConsumed} is outside 0-{maxFuelProgress}";
		}
        if (requested.ClientSequence <= 0)
        {
            return "client sequence must be positive";
        }
        if (entity.Metadata is PowerSourceMetadata current && current.SourceType != BasePowerSourceType.UNKNOWN && current.SourceType != requested.SourceType)
        {
            return $"source type changed from {current.SourceType} to {requested.SourceType}";
        }
        // The sequence is recorded for diagnostics only. Updates arrive reliable-ordered from the lock owner, and a
        // client restarts its counter on every join while session ids are reused, so ordering must not be enforced here.
        return "";
    }

    private static bool IsCompatibleEntity(Entity entity, BasePowerSourceType sourceType) => sourceType switch
    {
        BasePowerSourceType.SOLAR or BasePowerSourceType.THERMAL => entity is ModuleEntity,
        BasePowerSourceType.BIOREACTOR or BasePowerSourceType.NUCLEAR => entity is InteriorPieceEntity,
        _ => false
    };

	private static bool HasCompatibleTechType(Entity entity, BasePowerSourceType sourceType)
	{
		string techType = entity.TechType?.Name ?? "None";
		if (techType == "None")
		{
			return true; // Legacy build entities did not persist their TechType.
		}
		return sourceType switch
		{
			BasePowerSourceType.SOLAR => techType == "SolarPanel",
			BasePowerSourceType.THERMAL => techType == "ThermalPlant",
			BasePowerSourceType.BIOREACTOR => techType is "Bioreactor" or "BaseBioReactor",
			BasePowerSourceType.NUCLEAR => techType is "NuclearReactor" or "BaseNuclearReactor",
			_ => false
		};
	}

    private static PowerSourceMetadata CanonicalOrFallback(Entity entity, BasePowerSourceUpdate requested)
    {
        if (entity.Metadata is PowerSourceMetadata metadata)
        {
            return metadata;
        }
        BasePowerSourceTypes.TryGetMaxPower(requested.SourceType, out float maxPower);
        float power = float.IsFinite(requested.Power) ? Math.Clamp(requested.Power, 0f, maxPower) : 0f;
        return new PowerSourceMetadata(power, maxPower, requested.SourceType, 0, requested.FuelConsumed);
    }

    private readonly record struct OwnerSequence(SessionId SessionId, long Sequence);
}

internal enum ConsumptionRoute
{
    REJECTED,
    FORWARD_TO_OWNER,
    APPLIED_BY_SERVER
}
