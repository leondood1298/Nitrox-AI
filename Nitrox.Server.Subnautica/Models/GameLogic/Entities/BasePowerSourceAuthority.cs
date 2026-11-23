using System.Collections.Generic;
using Nitrox.Model.Core;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Bases;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;

namespace Nitrox.Server.Subnautica.Models.GameLogic.Entities;

internal sealed class BasePowerSourceAuthority
{
    private const float POWER_TOLERANCE = 0.01f;

    /// <summary>
    ///     Upper bound for one consumption request. Clients batch a fraction of a second of use, so anything near the
    ///     largest source capacity (a nuclear reactor) is not a real request.
    /// </summary>
    internal const float MAX_CONSUMPTION_PER_REQUEST = BasePowerSourceTypes.NUCLEAR_MAX_POWER;

    /// <summary>
    ///     Highest thermal plant temperature that is stored. The game has no upper limit for the water temperature
    ///     itself (a biome setting plus curves, plus up to 60 near a heat source), but a thermal plant treats everything
    ///     from here up the same: ThermalPlant.AddPower gives full output and ThermalPlant.UpdateUI a full gauge. A
    ///     higher stored value changes nothing, so no report needs to be believed beyond it.
    /// </summary>
    internal const float MAX_THERMAL_TEMPERATURE = 100f;
    private readonly Dictionary<NitroxId, OwnerSequence> lastOwnerSequenceBySource = [];
    private readonly Dictionary<NitroxId, long> highestRevisionBySource = [];

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
            // The thermal plant temperature shares this metadata slot and must survive every power update.
            accepted = new PowerSourceMetadata(power, maxPower, requested.SourceType, NextRevision(entity), fuelConsumed, GetStoredTemperature(entity.Metadata));
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
            if (entity.Metadata is not PowerSourceMetadata current || !float.IsFinite(report.Temperature))
            {
                return false;
            }
            float temperature = Math.Min(report.Temperature, MAX_THERMAL_TEMPERATURE);
            if (temperature <= current.Temperature)
            {
                return false;
            }
            entity.Metadata = current.WithTemperature(temperature);
            return true;
        }
    }

    /// <summary>
    ///     Checks a thermal plant temperature report, which any player near the plant may send, and stores it when the
    ///     plant already has power state. Otherwise the caller stores <paramref name="accepted" /> as the metadata.
    /// </summary>
    /// <param name="accepted">The report with its temperature limited to <see cref="MAX_THERMAL_TEMPERATURE" />.</param>
    /// <returns>False when the report is not a number or not higher than what is stored.</returns>
    public bool TryAcceptTemperature(Entity entity, ThermalPlantMetadata report, out ThermalPlantMetadata accepted)
    {
        accepted = new ThermalPlantMetadata(Math.Min(report.Temperature, MAX_THERMAL_TEMPERATURE));
        if (entity.Metadata is PowerSourceMetadata)
        {
            return TryApplyTemperature(entity, report);
        }
        return float.IsFinite(report.Temperature) &&
               (entity.Metadata is not ThermalPlantMetadata current || accepted.Temperature > current.Temperature);
    }

    /// <summary>
    ///     Whether metadata of another kind may take the place of a source's power state. Only the temperature every
    ///     nearby player measures is taken from anyone (and merged, see <see cref="TryAcceptTemperature" />). Anything
    ///     else would erase the level, fuel progress and revision, and the source would come back full for the next
    ///     player who joins.
    /// </summary>
    public static bool MayReplacePowerState(EntityMetadata incoming, bool senderSimulatesSource) =>
        incoming is ThermalPlantMetadata || senderSimulatesSource;

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
                NextRevision(entity), current.FuelConsumed, current.Temperature);
            entity.Metadata = accepted;
            return ConsumptionRoute.APPLIED_BY_SERVER;
        }
    }

    /// <summary>
    ///     The revision of the next state of a source. Clients ignore states with a lower revision than they have seen,
    ///     so it must never go down for an id. The stored state alone does not guarantee that: a reactor that is
    ///     deconstructed and built again keeps its id but comes back as a new entity whose state starts at zero.
    /// </summary>
    private long NextRevision(Entity entity)
    {
        long stored = entity.Metadata is PowerSourceMetadata current ? current.Revision : 0;
        highestRevisionBySource.TryGetValue(entity.Id, out long highest);
        long next = Math.Max(stored, highest) + 1;
        highestRevisionBySource[entity.Id] = next;
        return next;
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
