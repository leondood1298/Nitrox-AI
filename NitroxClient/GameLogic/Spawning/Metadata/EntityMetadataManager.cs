using System;
using System.Collections.Generic;
using System.Linq;
using NitroxClient.GameLogic.Spawning.Metadata.Extractor.Abstract;
using NitroxClient.GameLogic.Spawning.Metadata.Processor.Abstract;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;
using UnityEngine;

namespace NitroxClient.GameLogic.Spawning.Metadata;

public class EntityMetadataManager
{
    private readonly Dictionary<Type, IEntityMetadataExtractor> extractors;
    private readonly Dictionary<Type, IEntityMetadataProcessor> processors;

    /// <summary>
    /// As it takes some time spawning lots of entities, it can happen that we receive metadata updates for those which are still in the queue.
    /// Therefore we list the updates which happened while we didn't have the said objects loaded
    /// </summary>
    /// <summary>
    ///     Updates received while their entity was still waiting to be spawned. Kept per kind of metadata: an entity can
    ///     receive more than one kind (a Scanner Room gets its own and its fabricator's), and a waiting update must only
    ///     ever stand in for older metadata of the same kind.
    /// </summary>
    private readonly Dictionary<NitroxId, Dictionary<Type, EntityMetadata>> newerMetadataById = new();

    public EntityMetadataManager(IEnumerable<IEntityMetadataExtractor> extractors, IEnumerable<IEntityMetadataProcessor> processors)
    {
        this.extractors = extractors.ToDictionary(p => p.GetType().BaseType.GetGenericArguments()[0]);
        this.processors = processors.ToDictionary(p => p.GetType().BaseType.GetGenericArguments()[0]);
    }

    public Optional<EntityMetadata> Extract(object o)
    {
        if (extractors.TryGetValue(o.GetType(), out IEntityMetadataExtractor extractor))
        {
            return extractor.From(o);
        }

        return Optional.Empty;
    }

    public Optional<EntityMetadata> Extract(GameObject o)
    {
        foreach (Component component in o.GetComponents<Component>())
        {
            if (extractors.TryGetValue(component.GetType(), out IEntityMetadataExtractor extractor))
            {
                Optional<EntityMetadata> metadata = extractor.From(component);
                if (metadata.HasValue)
                {
                    return metadata;
                }
            }
        }

        return Optional.Empty;
    }

    public bool TryExtract(object o, out EntityMetadata metadata)
    {
        Optional<EntityMetadata> opMetadata = Extract(o);
        metadata = opMetadata.Value;
        return opMetadata.HasValue;
    }

    public Optional<IEntityMetadataProcessor> FromMetaData(EntityMetadata metadata)
    {
        if (metadata != null && processors.TryGetValue(metadata.GetType(), out IEntityMetadataProcessor processor))
        {
            return Optional.Of(processor);
        }

        return Optional.Empty;
    }

    /// <param name="spawnContinues">
    ///     True when the entity is still being spawned and its metadata will be applied once more at the end (a spawner
    ///     that applies metadata before it spawns the children). What is applied now is then kept as the entity's
    ///     newest state, so that the final application cannot put the older state from the spawn packet back.
    /// </param>
    public void ApplyMetadata(GameObject gameObject, EntityMetadata metadata, bool spawnContinues = false)
    {
        // In case a metadata update was received while this entity was in the spawn queue
        if (metadata != null && gameObject.TryGetNitroxId(out NitroxId objectId))
        {
            metadata = TakeNewest(objectId, metadata, spawnContinues);
        }
        Optional<IEntityMetadataProcessor> metadataProcessor = FromMetaData(metadata);

        if (metadataProcessor.HasValue)
        {
            metadataProcessor.Value.ProcessMetadata(gameObject, metadata);
        }
    }

    /// <summary>
    ///     Returns what an entity that is being spawned should get: <paramref name="metadata" /> from its spawn packet,
    ///     or an update of the same kind that was received after that packet.
    /// </summary>
    internal EntityMetadata TakeNewest(NitroxId entityId, EntityMetadata metadata, bool spawnContinues)
    {
        Type kind = metadata.GetType();
        if (newerMetadataById.TryGetValue(entityId, out Dictionary<Type, EntityMetadata> newerByType) && newerByType.TryGetValue(kind, out EntityMetadata newMetadata))
        {
            metadata = newMetadata;
            newerByType.Remove(kind);
            if (newerByType.Count == 0)
            {
                newerMetadataById.Remove(entityId);
            }
        }
        if (spawnContinues)
        {
            RegisterNewerMetadata(entityId, metadata);
        }
        return metadata;
    }

    /// <summary>
    ///     True between a spawner's own application of metadata and the final one (see <see cref="ApplyMetadata" />).
    ///     An update that arrives in between has to be kept for the final application as well.
    /// </summary>
    public bool IsAwaitingSpawnEnd(NitroxId entityId, EntityMetadata metadata)
    {
        return newerMetadataById.TryGetValue(entityId, out Dictionary<Type, EntityMetadata> newerByType) && newerByType.ContainsKey(metadata.GetType());
    }

    public void RegisterNewerMetadata(NitroxId entityId, EntityMetadata metadata)
    {
        if (!newerMetadataById.TryGetValue(entityId, out Dictionary<Type, EntityMetadata> newerByType))
        {
            newerMetadataById[entityId] = newerByType = [];
        }
        newerByType[metadata.GetType()] = metadata;
    }

    public void ClearNewerMetadata()
    {
        newerMetadataById.Clear();
    }
}
