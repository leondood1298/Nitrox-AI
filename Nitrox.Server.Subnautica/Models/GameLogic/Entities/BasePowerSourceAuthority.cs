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
            long revision = entity.Metadata is PowerSourceMetadata current ? current.Revision + 1 : 1;
            accepted = new PowerSourceMetadata(power, maxPower, requested.SourceType, revision);
            entity.Metadata = accepted;
            lastOwnerSequenceBySource[entity.Id] = new OwnerSequence(senderSessionId, requested.ClientSequence);
            return true;
        }
    }

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
        if (requested.ClientSequence <= 0)
        {
            return "client sequence must be positive";
        }
        if (entity.Metadata is PowerSourceMetadata current && current.SourceType != BasePowerSourceType.UNKNOWN && current.SourceType != requested.SourceType)
        {
            return $"source type changed from {current.SourceType} to {requested.SourceType}";
        }
        if (lastOwnerSequenceBySource.TryGetValue(entity.Id, out OwnerSequence last) && last.SessionId == senderSessionId && requested.ClientSequence <= last.Sequence)
        {
            return $"stale client sequence {requested.ClientSequence}; last accepted {last.Sequence}";
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
        return new PowerSourceMetadata(power, maxPower, requested.SourceType, 0);
    }

    private readonly record struct OwnerSequence(SessionId SessionId, long Sequence);
}
