using Nitrox.Model.DataStructures;
using Nitrox.Model.Helper;
using Nitrox.Model.Subnautica.Extensions;
using Nitrox.Model.Subnautica.Packets;
using NitroxClient.Communication.Packets.Processors.Core;
using NitroxClient.GameLogic;
using NitroxClient.GameLogic.Spawning.Metadata;
using NitroxClient.GameLogic.Spawning.Metadata.Processor.Abstract;
using NitroxClient.MonoBehaviours;
using UnityEngine;

namespace NitroxClient.Communication.Packets.Processors;

internal sealed class EntityMetadataUpdateProcessor(Entities entities, EntityMetadataManager entityMetadataManager, MapRoomScanResultBroadcaster scanResultBroadcaster, SimulationOwnership simulationOwnership) : IClientPacketProcessor<EntityMetadataUpdate>
{
    private readonly Entities entities = entities;
    private readonly EntityMetadataManager entityMetadataManager = entityMetadataManager;

    public Task Process(ClientProcessorContext context, EntityMetadataUpdate update)
    {
        if (entities.SpawningEntities)
        {
            entityMetadataManager.RegisterNewerMetadata(update.Id, update.NewValue);
        }

        if (!NitroxEntity.TryGetObjectFrom(update.Id, out GameObject gameObject))
        {
            return Task.CompletedTask;
        }

        Optional<IEntityMetadataProcessor> metadataProcessor = entityMetadataManager.FromMetaData(update.NewValue);
        Validate.IsTrue(metadataProcessor.HasValue, $"No processor found for EntityMetadata of type {update.NewValue.GetType()}");

        long previousGeneration = gameObject.TryGetComponent(out MapRoomNetworkState previousState) ? previousState.Generation : 0;
        bool hadMapRoom = gameObject.TryGetComponent(out MapRoomFunctionality existingMapRoom);
        int previousProgress = hadMapRoom ? existingMapRoom.numNodesScanned : 0;
        TechType previousTarget = hadMapRoom ? existingMapRoom.typeToScan : TechType.None;
        metadataProcessor.Value.ProcessMetadata(gameObject, update.NewValue);
        if (update.NewValue is Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata.MapRoomMetadata metadata && metadata.Generation > previousGeneration && gameObject.TryGetComponent(out MapRoomFunctionality mapRoom))
        {
            if (MapRoomScanResults.ShouldRepublishProgress(previousGeneration, metadata.Generation, previousTarget == metadata.TypeToScan.ToUnity(),
                    previousProgress, metadata.NumNodesScanned, simulationOwnership.HasAnyLockType(update.Id)))
            {
                mapRoom.numNodesScanned = previousProgress;
                MapRoomScanResults.RefreshResultConsumers(mapRoom);
                entities.EntityMetadataChangedThrottled(mapRoom, update.Id);
            }
            scanResultBroadcaster.BroadcastSnapshot(mapRoom);
        }
        return Task.CompletedTask;
    }
}
