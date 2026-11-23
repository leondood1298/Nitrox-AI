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
            RecordApply(gameObject.TryGetNitroxId(out NitroxId sourceId) ? sourceId : null, metadata, true);
        }
        else
        {
            Log.Error($"Could not find PowerSource on {gameObject.name}");
            RecordApply(gameObject.TryGetNitroxId(out NitroxId sourceId) ? sourceId : null, metadata, false);
        }
    }

    /// <summary>
    ///     Diagnostics only: the states applied while this player joins, and afterwards the states the server sent
    ///     back in place of one it refused.
    /// </summary>
    private void RecordApply(NitroxId? sourceId, PowerSourceMetadata metadata, bool objectFound)
    {
        if (!Multiplayer.Active)
        {
            return;
        }
        bool initialSyncCompleted = Multiplayer.Main && Multiplayer.Main.InitialSyncCompleted;
        bool waitScreenWaiting = WaitScreen.IsWaiting;
        if (!initialSyncCompleted || waitScreenWaiting)
        {
            Resolve<BasePowerClientDiagnostics>().RecordSourceApply(sourceId, metadata, objectFound, initialSyncCompleted, waitScreenWaiting, "metadata");
            return;
        }
        if (sourceId != null && BasePowerSources.IsAnswerToRefusedState(Resolve<SimulationOwnership>().HasAnyLockType(sourceId), !BasePowerSources.IsRebuildingBases()))
        {
            // The answer carries the stored state and no reason; the server's own line for this source has it
            Resolve<BasePowerClientDiagnostics>().RecordSourceRejected(sourceId, metadata, "server_restored_stored_state");
        }
    }
}
