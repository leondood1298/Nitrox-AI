using Nitrox.Model.DataStructures;
using Nitrox.Model.Helper;
using Nitrox.Model.Subnautica.Packets;
using NitroxClient.Communication.Packets.Processors.Core;
using NitroxClient.GameLogic;
using NitroxClient.GameLogic.Bases;
using NitroxClient.GameLogic.Spawning.Metadata;
using NitroxClient.GameLogic.Spawning.Metadata.Processor.Abstract;
using NitroxClient.MonoBehaviours;
using UnityEngine;

namespace NitroxClient.Communication.Packets.Processors;

internal sealed class EntityMetadataUpdateProcessor(Entities entities, EntityMetadataManager entityMetadataManager) : IClientPacketProcessor<EntityMetadataUpdate>
{
    private readonly Entities entities = entities;
    private readonly EntityMetadataManager entityMetadataManager = entityMetadataManager;

    /// <summary>
    ///     Whether an update has to be kept so that the entity gets it when it (re)spawns.
    ///     <para>
    ///     An entity spawns some time after its spawn packet arrived, with the state that packet carried. Updates are
    ///     sent in order, so one that arrives before the entity has finished spawning is newer than that state. Upstream
    ///     only keeps it while the spawn queue runs. The initial sync and a building resync spawn without the queue, and
    ///     an update that arrived then was dropped, or applied and overwritten by the final application of the older
    ///     state. For a player who then becomes the entity's simulating player that older state would become everyone's.
    ///     </para>
    /// </summary>
    internal static bool MustKeepForSpawn(bool spawningEntities, bool rebuildingBases, bool objectExists, bool awaitingSpawnEnd)
    {
        return spawningEntities || rebuildingBases || !objectExists || awaitingSpawnEnd;
    }

    public Task Process(ClientProcessorContext context, EntityMetadataUpdate update)
    {
        bool objectExists = NitroxEntity.TryGetObjectFrom(update.Id, out GameObject gameObject);
        bool rebuildingBases = BuildingHandler.Main && BuildingHandler.Main.Resyncing;
        if (MustKeepForSpawn(entities.SpawningEntities, rebuildingBases, objectExists, entityMetadataManager.IsAwaitingSpawnEnd(update.Id, update.NewValue)))
        {
            entityMetadataManager.RegisterNewerMetadata(update.Id, update.NewValue);
        }

        if (!objectExists)
        {
            return Task.CompletedTask;
        }

        Optional<IEntityMetadataProcessor> metadataProcessor = entityMetadataManager.FromMetaData(update.NewValue);
        Validate.IsTrue(metadataProcessor.HasValue, $"No processor found for EntityMetadata of type {update.NewValue.GetType()}");

        metadataProcessor.Value.ProcessMetadata(gameObject, update.NewValue);
        return Task.CompletedTask;
    }
}
