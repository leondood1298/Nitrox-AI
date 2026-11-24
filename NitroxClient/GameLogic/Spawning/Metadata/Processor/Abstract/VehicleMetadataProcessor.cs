using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;
using NitroxClient.MonoBehaviours.Vehicles;
using UnityEngine;

namespace NitroxClient.GameLogic.Spawning.Metadata.Processor.Abstract;

public abstract class VehicleMetadataProcessor<T> : EntityMetadataProcessor<T> where T : VehicleMetadata
{
    private readonly LiveMixinManager liveMixinManager;

    public VehicleMetadataProcessor(LiveMixinManager liveMixinManager)
    {
        this.liveMixinManager = liveMixinManager;
    }

    protected void SetHealth(GameObject gameObject, float health)
    {
        LiveMixin liveMixin = gameObject.RequireComponentInChildren<LiveMixin>(true);
        liveMixinManager.SyncRemoteHealth(liveMixin, health);
    }

    protected void SetInPrecursor(Vehicle vehicle, bool inPrecursor)
    {
        vehicle.precursorOutOfWater = inPrecursor;
    }

    protected void SetNameAndColors(SubName subName, VehicleMetadata metadata)
    {
        SubNameInputMetadataProcessor.SetNameAndColors(subName, metadata.Name, metadata.Colors);
        // The next rename made here must be newer than the one just received (see VehicleMetadataAuthority)
        VehicleNameVersion.Set(subName.gameObject, metadata.NameVersion);
    }
}
