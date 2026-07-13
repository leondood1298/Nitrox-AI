using Nitrox.Model.Logger;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;
using NitroxClient.Extensions;
using NitroxClient.GameLogic;
using NitroxClient.GameLogic.Spawning.Metadata.Processor.Abstract;
using UnityEngine;

namespace NitroxClient.GameLogic.Spawning.Metadata.Processor;

public class PowerSourceMetadataProcessor(BasePowerState state) : EntityMetadataProcessor<PowerSourceMetadata>
{
	public override void ProcessMetadata(GameObject gameObject, PowerSourceMetadata metadata)
	{
		if (gameObject.TryGetComponent<PowerSource>(out var component))
		{
			PowerSourceMetadata accepted = metadata;
			if (!gameObject.TryGetNitroxId(out var sourceId) || state.TryApply(sourceId, metadata, out accepted))
			{
				component.SetPower(accepted.Power);
				BasePowerSources.SetFuelConsumed(component, accepted.SourceType, accepted.FuelConsumed);
			}
		}
		else
		{
			Log.Error("Could not find PowerSource on " + gameObject.name);
		}
	}
}


