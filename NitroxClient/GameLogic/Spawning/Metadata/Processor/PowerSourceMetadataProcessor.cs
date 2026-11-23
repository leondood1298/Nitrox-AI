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
        BasePowerState state = Resolve<BasePowerState>();
        if (gameObject.TryGetComponent(out PowerSource powerSource))
        {
            // A broadcast can arrive before this object spawns. The snapshot is then older than what is already known,
            // and the newer state must still be applied or the source keeps its prefab default.
            bool hasId = gameObject.TryGetNitroxId(out NitroxId sourceId);
            PowerSourceMetadata accepted = hasId ? state.Reconcile(sourceId, metadata) : metadata;
            powerSource.SetPower(accepted.Power);
            BasePowerSources.SetFuelConsumed(powerSource, accepted.SourceType, accepted.FuelConsumed);
            // The temperature comes from the saved snapshot, never from live power broadcasts, and the game only ever
            // raises it. Without it a restored thermal plant forgets the peak it reached (e.g. from a geyser).
            if (metadata.Temperature > 0f && gameObject.TryGetComponent(out ThermalPlant thermalPlant))
            {
                thermalPlant.temperature = Mathf.Max(thermalPlant.temperature, metadata.Temperature);
            }
            RecordReconciliationApply(hasId ? sourceId : null, accepted, true);
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
