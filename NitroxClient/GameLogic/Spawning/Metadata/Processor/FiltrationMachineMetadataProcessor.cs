using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;
using NitroxClient.GameLogic.Spawning.Metadata.Processor.Abstract;
using UnityEngine;

namespace NitroxClient.GameLogic.Spawning.Metadata.Processor;

public class FiltrationMachineMetadataProcessor : EntityMetadataProcessor<FiltrationMachineMetadata>
{
    public override void ProcessMetadata(GameObject gameObject, FiltrationMachineMetadata metadata)
    {
        if (!gameObject.TryGetComponent(out FiltrationMachine machine))
        {
            Log.Error($"[{nameof(FiltrationMachineMetadataProcessor)}] Could not find a FiltrationMachine on {gameObject.name}");
            return;
        }

        // Set before FiltrationMachine.DelayedStart has run, a running timer also keeps it from starting one anew
        machine.timeRemainingWater = metadata.TimeRemainingWater;
        machine.timeRemainingSalt = metadata.TimeRemainingSalt;

        // Only the simulating player's update refreshes the model by itself (see FiltrationMachines.FiltersHere)
        BaseFiltrationMachineGeometry model = machine.GetModel();
        if (model)
        {
            model.SetDirty();
        }
    }
}
