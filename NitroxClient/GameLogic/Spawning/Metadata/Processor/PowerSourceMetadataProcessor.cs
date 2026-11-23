using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;
using NitroxClient.GameLogic.Spawning.Metadata.Processor.Abstract;
using NitroxClient.MonoBehaviours;
using UnityEngine;

namespace NitroxClient.GameLogic.Spawning.Metadata.Processor;

public class PowerSourceMetadataProcessor : EntityMetadataProcessor<PowerSourceMetadata>
{
    public override void ProcessMetadata(GameObject gameObject, PowerSourceMetadata metadata)
    {
        if (gameObject.TryGetComponent(out PowerSource powerSource))
        {
            // Whatever arrives here is the newest state: updates come in order, and one that arrived before this
            // object had spawned was kept for it (see EntityMetadataManager.TakeNewest)
            powerSource.SetPower(metadata.Power);
            BasePowerSources.SetFuelConsumed(powerSource, metadata.SourceType, metadata.FuelConsumed);
            // The game only ever raises the temperature. Without the stored one a restored thermal plant forgets the
            // peak it reached (e.g. from a geyser).
            if (metadata.Temperature > 0f && gameObject.TryGetComponent(out ThermalPlant thermalPlant))
            {
                thermalPlant.temperature = Mathf.Max(thermalPlant.temperature, metadata.Temperature);
            }
            RecordReconciliationApply(gameObject.TryGetNitroxId(out NitroxId sourceId) ? sourceId : null, metadata, true);
        }
        else
        {
            Log.Error($"Could not find PowerSource on {gameObject.name}");
            RecordReconciliationApply(gameObject.TryGetNitroxId(out NitroxId sourceId) ? sourceId : null, metadata, false);
        }
    }

    private void RecordReconciliationApply(NitroxId? sourceId, PowerSourceMetadata metadata, bool objectFound)
    {
        bool initialSyncCompleted = Multiplayer.Main && Multiplayer.Main.InitialSyncCompleted;
        bool waitScreenWaiting = WaitScreen.IsWaiting;
        if (Multiplayer.Active && (!initialSyncCompleted || waitScreenWaiting))
        {
            Resolve<BasePowerClientDiagnostics>().RecordSourceApply(sourceId, metadata, objectFound, initialSyncCompleted, waitScreenWaiting, "metadata");
        }
    }
}
