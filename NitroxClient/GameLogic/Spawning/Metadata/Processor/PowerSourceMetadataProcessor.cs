using Nitrox.Model.Logger;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;
using NitroxClient.Extensions;
using NitroxClient.GameLogic;
using NitroxClient.GameLogic.Spawning.Metadata.Processor.Abstract;
using NitroxClient.MonoBehaviours;
using UnityEngine;

namespace NitroxClient.GameLogic.Spawning.Metadata.Processor;

public class PowerSourceMetadataProcessor : EntityMetadataProcessor<PowerSourceMetadata>
{
	public override void ProcessMetadata(GameObject gameObject, PowerSourceMetadata metadata)
	{
		BasePowerState state = Resolve<BasePowerState>();
		if (gameObject.TryGetComponent<PowerSource>(out var component))
		{
			PowerSourceMetadata accepted = metadata;
			if (!gameObject.TryGetNitroxId(out var sourceId) || state.TryApply(sourceId, metadata, out accepted))
			{
				component.SetPower(accepted.Power);
				BasePowerSources.SetFuelConsumed(component, accepted.SourceType, accepted.FuelConsumed);
				RecordReconciliationApply(gameObject.TryGetNitroxId(out sourceId) ? sourceId : null, accepted, true);
			}
		}
		else
		{
			Log.Error("Could not find PowerSource on " + gameObject.name);
			RecordReconciliationApply(gameObject.TryGetNitroxId(out var sourceId) ? sourceId : null, metadata, false);
		}
	}

	private void RecordReconciliationApply(Nitrox.Model.DataStructures.NitroxId? sourceId, PowerSourceMetadata metadata, bool objectFound)
	{
		bool initialSyncCompleted = Multiplayer.Main && Multiplayer.Main.InitialSyncCompleted;
		bool waitScreenWaiting = WaitScreen.IsWaiting;
		if (Multiplayer.Active && (!initialSyncCompleted || waitScreenWaiting))
		{
			Resolve<BasePowerClientDiagnostics>().RecordSourceApply(sourceId, metadata, objectFound, initialSyncCompleted, waitScreenWaiting, "metadata");
		}
	}
}


