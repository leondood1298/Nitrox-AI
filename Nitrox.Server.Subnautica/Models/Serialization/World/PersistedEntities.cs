using System.Collections.Generic;
using System.Linq;
using System.Text;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities;
using Nitrox.Server.Subnautica.Models.GameLogic.Entities;

namespace Nitrox.Server.Subnautica.Models.Serialization.World;

/// <summary>
///     How the entities that are not under the global root are written to and read from <see cref="EntityData" />.
/// </summary>
/// <remarks>
///     Each entity is written once: the top ones in the list, every other inside its parent. Entities under a global root
///     entity are written with it in <see cref="GlobalRootData" />. A world written before this also listed every child
///     at the top and every child of a global root entity in both files; <see cref="PrepareLoaded" /> reads both.
/// </remarks>
internal static class PersistedEntities
{
    /// <param name="entitiesExceptGlobalRoot">All registered entities that are not global root entities.</param>
    /// <returns>
    ///     The top ones only, their children inside them. An entity whose parent is not saved (the contents of a destroyed
    ///     Cyclops, which is not saved) is left out with it instead of being kept forever without a parent.
    /// </returns>
    public static List<Entity> SelectForSave(IEnumerable<Entity> entitiesExceptGlobalRoot)
    {
        return [.. entitiesExceptGlobalRoot.Where(entity => entity.ParentId == null)];
    }

    /// <summary>
    ///     Turns the entities read from <see cref="EntityData" /> into a flat list of each entity once, every child inside
    ///     its parent again, without the entities that have no parent to belong to, and without exact copies of the
    ///     game's serialized objects (an older build made every one twice).
    /// </summary>
    /// <param name="topEntities">The entities read from the file.</param>
    /// <param name="globalRootEntities">The entities read from <see cref="GlobalRootData" />, with their children.</param>
    public static LoadedEntities PrepareLoaded(List<Entity> topEntities, IEnumerable<GlobalRootEntity> globalRootEntities)
    {
        // A dropped tool's battery was saved twice under the tool with one id, the first copy with the charge at the drop.
        // The last one is kept, the one that received the charge (as the id index already did for global root entities).
        int duplicateChildren = 0;
        HashSet<NitroxId> globalRootTreeIds = [];
        foreach (GlobalRootEntity globalRootEntity in globalRootEntities)
        {
            duplicateChildren += EntityRegistry.KeepLastChildPerId(globalRootEntity);
            CollectIds(globalRootEntity, globalRootTreeIds);
        }
        foreach (Entity topEntity in topEntities)
        {
            duplicateChildren += EntityRegistry.KeepLastChildPerId(topEntity);
        }

        // An older world lists a child both at the top and inside its parent; the top copy is kept, as before
        Dictionary<NitroxId, Entity> entitiesById = [];
        int copiesOfGlobalRootChildren = 0;
        foreach (Entity entity in topEntities.Concat(topEntities.SelectMany(Descendants)))
        {
            if (globalRootTreeIds.Contains(entity.Id))
            {
                copiesOfGlobalRootChildren++;
                continue;
            }
            entitiesById.TryAdd(entity.Id, entity);
        }

        foreach (Entity entity in entitiesById.Values)
        {
            entity.ChildEntities.Clear();
        }

        Dictionary<NitroxId, bool> belongs = [];
        List<Entity> kept = [];
        int orphans = 0;
        foreach (Entity entity in entitiesById.Values)
        {
            if (Belongs(entity, entitiesById, belongs))
            {
                kept.Add(entity);
            }
            else
            {
                orphans++;
            }
        }

        int duplicates = RemoveSerializedDuplicates(kept);

        foreach (Entity entity in kept)
        {
            if (entity.ParentId != null && entitiesById.TryGetValue(entity.ParentId, out Entity? parentEntity))
            {
                parentEntity.ChildEntities.Add(entity);
                if (entity is WorldEntity worldEntity && parentEntity is WorldEntity parentWorldEntity)
                {
                    worldEntity.Transform.SetParent(parentWorldEntity.Transform, false);
                }
            }
        }

        return new LoadedEntities(kept, orphans, duplicates, copiesOfGlobalRootChildren, duplicateChildren);
    }

    /// <summary>
    ///     Removes all but the first of the game's serialized objects (lights, a few colliders) that are exactly alike: same
    ///     cell, level, position and component bytes. Such pairs were made by an older build which spawned every one of them
    ///     twice, which doubled the light everywhere the game placed one.
    /// </summary>
    /// <returns>How many were removed.</returns>
    public static int RemoveSerializedDuplicates(List<Entity> entities)
    {
        HashSet<string> seen = [];
        return entities.RemoveAll(entity => entity is SerializedWorldEntity { ParentId: null } serialized && !seen.Add(KeyOf(serialized)));
    }

    private static string KeyOf(SerializedWorldEntity entity)
    {
        StringBuilder key = new();
        key.Append(entity.BatchId).Append('|').Append(entity.CellId).Append('|').Append(entity.Level).Append('|')
           .Append(entity.Transform.LocalPosition.X.ToString("R")).Append(',')
           .Append(entity.Transform.LocalPosition.Y.ToString("R")).Append(',')
           .Append(entity.Transform.LocalPosition.Z.ToString("R"));
        foreach (SerializedComponent component in entity.Components)
        {
            key.Append('|').Append(component.TypeName).Append(':').Append(component.IsEnabled).Append(':').Append(Convert.ToBase64String(component.Data ?? []));
        }
        return key.ToString();
    }

    /// <summary>
    ///     An entity belongs in the world if it is at the top or its parent belongs. A parent that is a global root entity
    ///     does not count: its children are read from <see cref="GlobalRootData" />.
    /// </summary>
    private static bool Belongs(Entity entity, Dictionary<NitroxId, Entity> entitiesById, Dictionary<NitroxId, bool> belongs)
    {
        List<Entity> chain = [];
        Entity? current = entity;
        bool result;
        while (true)
        {
            if (belongs.TryGetValue(current.Id, out bool known))
            {
                result = known;
                break;
            }
            if (current.ParentId == null)
            {
                result = true;
                chain.Add(current);
                break;
            }
            chain.Add(current);
            // A parent missing, or parents that loop, leave the whole chain without a place in the world
            if (!entitiesById.TryGetValue(current.ParentId, out Entity? parent) || chain.Contains(parent))
            {
                result = false;
                break;
            }
            current = parent;
        }
        foreach (Entity link in chain)
        {
            belongs[link.Id] = result;
        }
        return result;
    }

    private static IEnumerable<Entity> Descendants(Entity entity)
    {
        foreach (Entity child in entity.ChildEntities)
        {
            yield return child;
            foreach (Entity descendant in Descendants(child))
            {
                yield return descendant;
            }
        }
    }

    private static void CollectIds(Entity entity, HashSet<NitroxId> ids)
    {
        ids.Add(entity.Id);
        foreach (Entity child in entity.ChildEntities)
        {
            CollectIds(child, ids);
        }
    }

    /// <param name="Entities">Every entity once, children inside their parents and also listed here.</param>
    /// <param name="Orphans">Entities left out because no parent of theirs was saved.</param>
    /// <param name="SerializedDuplicates">Exact copies of the game's serialized objects left out.</param>
    /// <param name="CopiesOfGlobalRootChildren">Entities left out because they are read from <see cref="GlobalRootData" />.</param>
    /// <param name="DuplicateChildren">Earlier children with the id of a later child under the same parent, left out.</param>
    public sealed record LoadedEntities(List<Entity> Entities, int Orphans, int SerializedDuplicates, int CopiesOfGlobalRootChildren, int DuplicateChildren);
}
