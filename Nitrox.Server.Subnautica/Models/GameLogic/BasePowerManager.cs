using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Bases;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;
using Nitrox.Server.Subnautica.Models.GameLogic.Entities;

namespace Nitrox.Server.Subnautica.Models.GameLogic;

/// <summary>
///     Decides what is stored as the state of a base power source (solar panel, thermal plant, bioreactor, nuclear
///     reactor). The level and fuel progress are simulated by one player, the source's simulating player, and only that
///     player may set them; everything else is checked against that here.
/// </summary>
internal sealed class BasePowerManager(EntityRegistry entityRegistry, SimulationOwnershipData simulationOwnershipData, BasePowerDiagnostics diagnostics, ILogger<BasePowerManager> logger)
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

    /// <summary>
    ///     Guards every read-then-write of a source's stored state, so that a level update, a temperature report and a
    ///     consumption the server applies itself cannot overwrite each other.
    /// </summary>
    private readonly Lock stateLock = new();

    /// <summary>
    ///     The gate for a source's level and fuel progress. The simulating player already holds what it sent, so an
    ///     accepted state only goes to the others. A sender that is refused keeps showing what it sent unless it is
    ///     given the stored state back.
    /// </summary>
    public MetadataUpdateResult AcceptState(Player sender, Entity source, PowerSourceMetadata requested)
    {
        if (TryAccept(sender, source, requested, out PowerSourceMetadata? accepted, out string reason))
        {
            diagnostics.RecordAccepted(source.Id, sender.SessionId, accepted);
            if (diagnostics.TraceEnabled)
            {
                logger.ZLogInformation($"[BasePower] accepted {accepted.SourceType} source {source.Id}: {accepted.Power:F2}, fuel progress {accepted.FuelConsumed:F2}, simulated by {sender.Name} #{sender.SessionId}");
            }
            return MetadataUpdateResult.Accept(accepted);
        }

        PowerSourceMetadata? stored = source.Metadata as PowerSourceMetadata;
        // A simulating player sends a source's state up to once a second, so the diagnostics thin out the repeats of a
        // rejection
        diagnostics.RecordRejected(source.Id, sender.SessionId, stored ?? WithinLimits(requested), reason);
        return stored != null ? MetadataUpdateResult.RejectAndCorrect(stored) : MetadataUpdateResult.Reject;
    }

    /// <summary>
    ///     Records a state sent for a source the server does not have: it was destroyed while the state travelled, or it
    ///     only exists on that client. There is nothing to store and no stored state to answer with.
    /// </summary>
    public void RejectUnknownSource(Player sender, NitroxId sourceId)
    {
        diagnostics.RecordRejected(sourceId, sender.SessionId, new PowerSourceMetadata(0f, 0f, BasePowerSourceType.UNKNOWN), "unknown source entity");
    }

    /// <summary>
    ///     The gate for a thermal plant's temperature, which every player near the plant measures and reports. It is a
    ///     ratchet (see ThermalPlant.QueryTemperature's Mathf.Max), so only the first report of a rise is news.
    /// </summary>
    public MetadataUpdateResult AcceptTemperature(Entity source, ThermalPlantMetadata report)
    {
        return TryApplyTemperature(source, report)
            ? MetadataUpdateResult.Accept(new ThermalPlantMetadata(Math.Min(report.Temperature, MAX_THERMAL_TEMPERATURE)))
            : MetadataUpdateResult.Reject;
    }

    /// <summary>
    ///     The gate for metadata of any other kind sent for an entity that holds power state. An entity has one
    ///     metadata slot: taking it would erase the level and fuel progress, and the source would come back full for
    ///     the next player who joins.
    /// </summary>
    public MetadataUpdateResult RejectReplacement(Player sender, Entity source, EntityMetadata metadata)
    {
        logger.ZLogWarning($"[BasePower] Player {sender.Name} tried replacing the power state of source {source.Id} with {metadata.GetType().Name}");
        return MetadataUpdateResult.Reject;
    }

    /// <summary>
    ///     Stores <paramref name="requested" /> as the source's level and fuel progress when it comes from the player
    ///     simulating the source and is possible for that kind of source.
    /// </summary>
    public bool TryAccept(Player sender, Entity source, PowerSourceMetadata requested, [NotNullWhen(true)] out PowerSourceMetadata? accepted, out string reason)
    {
        lock (stateLock)
        {
            accepted = null;
            reason = Validate(sender, source, requested);
            if (reason.Length > 0)
            {
                return false;
            }

            BasePowerSourceTypes.TryGetMaxPower(requested.SourceType, out float maxPower);
            BasePowerSourceTypes.TryGetMaxFuelProgress(requested.SourceType, out float maxFuelProgress);
            // The thermal plant temperature shares this metadata slot. It comes from temperature reports, which any
            // player sends, so the simulating player's state must not reset it.
            accepted = new PowerSourceMetadata(Math.Clamp(requested.Power, 0f, maxPower), Math.Clamp(requested.FuelConsumed, 0f, maxFuelProgress),
                requested.SourceType, GetStoredTemperature(source.Metadata));
            source.Metadata = accepted;
            return true;
        }
    }

    /// <summary>
    ///     Stores a thermal plant temperature report: inside the power state when the source has one, so that the report
    ///     does not take the place of that state, otherwise as the entity's metadata.
    /// </summary>
    /// <returns>False when the report is not a number, not higher than what is stored, or not for a thermal plant.</returns>
    public bool TryApplyTemperature(Entity source, ThermalPlantMetadata report)
    {
        // Any player may report a temperature, so without the second check a report could take the place of the
        // metadata of anything: a vehicle, a Scanner Room camera, another kind of power source
        if (!float.IsFinite(report.Temperature) || !IsThermalPlant(source))
        {
            return false;
        }
        float temperature = Math.Min(report.Temperature, MAX_THERMAL_TEMPERATURE);
        lock (stateLock)
        {
            switch (source.Metadata)
            {
                case PowerSourceMetadata state when temperature > state.Temperature:
                    source.Metadata = state.WithTemperature(temperature);
                    return true;
                case PowerSourceMetadata:
                    return false;
                case ThermalPlantMetadata plain when temperature <= plain.Temperature:
                    return false;
                default:
                    source.Metadata = new ThermalPlantMetadata(temperature);
                    return true;
            }
        }
    }

    /// <summary>
    ///     Decides what happens with energy that <paramref name="consumer" /> took from a source it does not simulate.
    ///     Only the simulating player's level counts, so the request normally goes to that player to apply. When nobody
    ///     simulates the source, the server takes it from the stored state itself.
    /// </summary>
    /// <param name="simulator">The player to pass the request to.</param>
    /// <param name="accepted">The new stored state when the server applied the consumption.</param>
    public ConsumptionRoute RouteConsumption(Player consumer, Entity source, float amount, out Player? simulator, out PowerSourceMetadata? accepted)
    {
        ConsumptionRoute route = ConsumptionRoute.REJECTED;
        string reason = "";
        PowerSourceMetadata? stored;
        lock (stateLock)
        {
            simulator = simulationOwnershipData.GetPlayerForLock(source.Id);
            accepted = null;
            if (!IsPowerSource(source))
            {
                // Passing this on would make the player simulating the entity look for a power source that is not
                // there and give the entity's simulation up
                reason = $"{source.GetType().Name} {source.TechType?.Name ?? "None"} is not a base power source";
            }
            else if (!float.IsFinite(amount) || amount <= 0f || amount > MAX_CONSUMPTION_PER_REQUEST)
            {
                reason = $"amount {amount} is outside 0-{MAX_CONSUMPTION_PER_REQUEST}";
            }
            else if (simulator == consumer)
            {
                // The simulating player's own consumption is already part of the level it simulates and sends
                reason = "sender simulates this source itself";
            }
            else if (simulator != null)
            {
                route = ConsumptionRoute.FORWARD_TO_SIMULATOR;
            }
            else if (source.Metadata is PowerSourceMetadata current)
            {
                accepted = current.WithPower(Math.Max(0f, current.Power - amount));
                source.Metadata = accepted;
                route = ConsumptionRoute.APPLIED_BY_SERVER;
            }
            else
            {
                reason = "source has no stored power state and no simulation owner";
            }
            stored = source.Metadata as PowerSourceMetadata;
        }

        // A consumer reports several times a second. The diagnostics count every report and thin out the lines.
        diagnostics.RecordConsumption(source.Id, consumer.SessionId, simulator?.SessionId, amount, route, stored, reason);
        return route;
    }

    /// <summary>
    ///     The stored state of every source with the player simulating it, for the basepower command.
    /// </summary>
    public List<SourceSnapshot> GetSources()
    {
        lock (stateLock)
        {
            return entityRegistry.GetAllEntities()
                                 .Where(entity => entity.Metadata is PowerSourceMetadata)
                                 .Select(entity => new SourceSnapshot(entity, (PowerSourceMetadata)entity.Metadata, simulationOwnershipData.GetPlayerForLock(entity.Id)))
                                 .ToList();
        }
    }

    /// <summary>
    ///     What a rejection is recorded with when the source has no stored state yet: the request, cut down to what a
    ///     source of that type can hold.
    /// </summary>
    private static PowerSourceMetadata WithinLimits(PowerSourceMetadata requested)
    {
        BasePowerSourceTypes.TryGetMaxPower(requested.SourceType, out float maxPower);
        float power = float.IsFinite(requested.Power) ? Math.Clamp(requested.Power, 0f, maxPower) : 0f;
        return new PowerSourceMetadata(power, requested.FuelConsumed, requested.SourceType);
    }

    internal static float GetStoredTemperature(EntityMetadata? metadata) => metadata switch
    {
        PowerSourceMetadata powerSource => powerSource.Temperature,
        ThermalPlantMetadata thermalPlant => thermalPlant.Temperature,
        _ => 0f
    };

    private string Validate(Player sender, Entity source, PowerSourceMetadata requested)
    {
        // Without this a client whose simulation was handed on, or that never had it, could put a stale level in
        // place of the one the simulating player keeps
        if (simulationOwnershipData.GetPlayerForLock(source.Id) != sender)
        {
            return "sender does not own source simulation";
        }
        if (!BasePowerSourceTypes.TryGetMaxPower(requested.SourceType, out float maxPower))
        {
            return "unknown source type";
        }
        if (!IsCompatibleEntity(source, requested.SourceType))
        {
            return $"source type {requested.SourceType} is incompatible with {source.GetType().Name}";
        }
        if (!HasCompatibleTechType(source, requested.SourceType))
        {
            return $"source type {requested.SourceType} does not match tech type {source.TechType}";
        }
        if (!float.IsFinite(requested.Power) || requested.Power < -POWER_TOLERANCE || requested.Power > maxPower + POWER_TOLERANCE)
        {
            return $"power {requested.Power} is outside 0-{maxPower}";
        }
        BasePowerSourceTypes.TryGetMaxFuelProgress(requested.SourceType, out float maxFuelProgress);
        if (!float.IsFinite(requested.FuelConsumed) || requested.FuelConsumed < -POWER_TOLERANCE || requested.FuelConsumed > maxFuelProgress + POWER_TOLERANCE)
        {
            return $"fuel progress {requested.FuelConsumed} is outside 0-{maxFuelProgress}";
        }
        if (source.Metadata is PowerSourceMetadata current && current.SourceType != BasePowerSourceType.UNKNOWN && current.SourceType != requested.SourceType)
        {
            return $"source type changed from {current.SourceType} to {requested.SourceType}";
        }
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
            // The entity carries no tech type to check against
            return true;
        }
        return GetSourceType(entity) == sourceType;
    }

    /// <summary>
    ///     The kind of power source an entity's tech type says it is.
    /// </summary>
    private static BasePowerSourceType GetSourceType(Entity entity) => entity.TechType?.Name switch
    {
        "SolarPanel" => BasePowerSourceType.SOLAR,
        "ThermalPlant" => BasePowerSourceType.THERMAL,
        "Bioreactor" or "BaseBioReactor" => BasePowerSourceType.BIOREACTOR,
        "NuclearReactor" or "BaseNuclearReactor" => BasePowerSourceType.NUCLEAR,
        _ => BasePowerSourceType.UNKNOWN
    };

    /// <summary>
    ///     A source that has reported its state holds it as metadata. One that was just built is known by its tech type.
    /// </summary>
    private static bool IsPowerSource(Entity entity) =>
        entity.Metadata is PowerSourceMetadata || (entity is ModuleEntity or InteriorPieceEntity && GetSourceType(entity) != BasePowerSourceType.UNKNOWN);

    private static bool IsThermalPlant(Entity entity) => entity is ModuleEntity && entity.Metadata switch
    {
        PowerSourceMetadata state => state.SourceType == BasePowerSourceType.THERMAL,
        ThermalPlantMetadata => true,
        _ => GetSourceType(entity) == BasePowerSourceType.THERMAL
    };

    internal readonly record struct SourceSnapshot(Entity Source, PowerSourceMetadata State, Player? Simulator);
}

internal enum ConsumptionRoute
{
    REJECTED,
    FORWARD_TO_SIMULATOR,
    APPLIED_BY_SERVER
}
