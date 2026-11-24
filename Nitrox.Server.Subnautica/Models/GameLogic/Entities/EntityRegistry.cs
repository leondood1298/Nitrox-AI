using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities;

namespace Nitrox.Server.Subnautica.Models.GameLogic.Entities
{
    public class EntityRegistry(ILogger<EntityRegistry> logger)
    {
        private readonly ILogger<EntityRegistry> logger = logger;
        private readonly ConcurrentDictionary<NitroxId, Entity> entitiesById = new();

        public Optional<T> GetEntityById<T>(NitroxId id) where T : Entity
        {
            TryGetEntityById(id, out T entity);

            return Optional.OfNullable(entity);
        }

        public Optional<Entity> GetEntityById(NitroxId id)
        {
            return GetEntityById<Entity>(id);
        }

        public bool TryGetEntityById<T>(NitroxId id, [NotNullWhen(true)] out T? entity) where T : Entity
        {
            if (entitiesById.TryGetValue(id, out Entity e) && e is T typedEntity)
            {
                entity = typedEntity;
                return true;
            }
            entity = null;
            return false;
        }

        public List<Entity> GetAllEntities(bool exceptGlobalRoot = false)
        {
            if (exceptGlobalRoot)
            {
                return new(entitiesById.Values.Where(entity => entity is not GlobalRootEntity));
            }
            return new List<Entity>(entitiesById.Values);            
        }

        public List<Entity> GetEntities(List<NitroxId> ids)
        {
            return entitiesById.Join(ids,
                                        entity => entity.Value.Id,
                                        id => id,
                                        (entity, id) => entity.Value)
                                .ToList();
        }

        public List<T> GetEntities<T>()
        {
            return entitiesById.Values.OfType<T>().ToList();
        }

        public void AddEntity(Entity entity)
        {
            if (!entitiesById.TryAdd(entity.Id, entity))
            {
                // Log an error to show stack trace but don't halt execution.
                logger.ZLogError(new InvalidOperationException(), $"Trying to add duplicate entity {entity.Id}");
            }
        }

        /// <summary>
        /// Registers or updates an entity and its children.
        /// </summary>
        public void AddOrUpdate(Entity entity)
        {
            KeepLastChildPerId(entity);
            if (!entitiesById.TryAdd(entity.Id, entity))
            {
                Entity current = entitiesById[entity.Id];

                RemoveFromParent(current);

                entitiesById.TryUpdate(entity.Id, entity, current);
            }

            AddToParent(entity);
            AddEntitiesIgnoringDuplicate(entity.ChildEntities);
        }

        public void AddEntities(IEnumerable<Entity> entities)
        {
            foreach(Entity entity in entities)
            {
                AddEntity(entity);
            }
        }

        /// <summary>
        /// Used for situations when some children may be new but others may not be. For
        /// example a dropped InventoryEntity turns into a WorldEntity but keeps its 
        /// battery inside (already known). 
        /// </summary>
        /// <remarks>
        /// Updates entities if they already exist
        /// </remarks>
        public void AddEntitiesIgnoringDuplicate(IEnumerable<Entity> entities)
        {
            foreach (Entity entity in entities)
            {
                KeepLastChildPerId(entity);
                if (entitiesById.TryGetValue(entity.Id, out Entity currentEntity))
                {
                    entitiesById.TryUpdate(entity.Id, entity, currentEntity);
                }
                else
                {
                    entitiesById.TryAdd(entity.Id, entity);
                }
                AddEntitiesIgnoringDuplicate(entity.ChildEntities);
            }
        }

        public Optional<Entity> RemoveEntity(NitroxId id)
        {
            if (entitiesById.TryRemove(id, out Entity entity))
            {
                RemoveFromParent(entity);

                foreach (Entity child in entity.ChildEntities)
                {
                    RemoveEntity(child.Id);
                }
            }

            return Optional.OfNullable(entity);
        }

        public void AddToParent(Entity entity)
        {
            if (entity.ParentId != null)
            {
                Optional<Entity> parent = GetEntityById(entity.ParentId);

                if (parent.HasValue)
                {
                    AddChildReplacingSameId(parent.Value, entity);
                }
            }
        }

        /// <summary>
        ///     Adds <paramref name="child" /> to the children of <paramref name="parent" />, in place of a child with the
        ///     same id. The id index holds one entity per id, so a second child with that id would no longer get any update
        ///     and would still be sent to the players and saved (a tool's battery kept its charge at the drop that way).
        /// </summary>
        private static void AddChildReplacingSameId(Entity parent, Entity child)
        {
            parent.ChildEntities.RemoveAll(existing => existing.Id.Equals(child.Id));
            parent.ChildEntities.Add(child);
        }

        /// <summary>
        ///     Leaves one child per id under <paramref name="entity" /> and each of its descendants: the last one, which is
        ///     the one the id index points at once the tree is registered.
        /// </summary>
        /// <returns>How many children were removed.</returns>
        public static int KeepLastChildPerId(Entity entity)
        {
            int removed = 0;
            HashSet<NitroxId> seen = [];
            for (int i = entity.ChildEntities.Count - 1; i >= 0; i--)
            {
                if (!seen.Add(entity.ChildEntities[i].Id))
                {
                    entity.ChildEntities.RemoveAt(i);
                    removed++;
                }
            }
            foreach (Entity child in entity.ChildEntities)
            {
                removed += KeepLastChildPerId(child);
            }
            return removed;
        }

        public void RemoveFromParent(Entity? entity)
        {
            if (entity == null)
            {
                return;
            }

            if (entity.ParentId != null && TryGetEntityById(entity.ParentId, out Entity parentEntity))
            {
                parentEntity.ChildEntities.RemoveAll(childEntity => childEntity.Id.Equals(entity.Id));
                entity.ParentId = null;
                if (entity is WorldEntity worldEntity && worldEntity.Transform != null)
                {
                    worldEntity.Transform.SetParent(null, true);
                }
            }
        }


        /// <summary>
        /// Removes all children from <paramref name="entity"/>
        /// </summary>
        public void CleanChildren(Entity entity)
        {
            for (int i = entity.ChildEntities.Count - 1; i >= 0; i--)
            {
                RemoveEntity(entity.ChildEntities[i].Id);
            }
        }

        public void ReparentEntity(NitroxId? entityId, NitroxId? newParentId)
        {
            if (entityId == null || !TryGetEntityById(entityId, out Entity entity))
            {
                logger.ZLogError($"Could not find entity to reparent: {entityId}");
                return;
            }
            ReparentEntity(entity, newParentId);
        }

        public void ReparentEntity(NitroxId entityId, Entity newParent)
        {
            if (entityId == null || !TryGetEntityById(entityId, out Entity entity))
            {
                logger.ZLogError($"Could not find entity to reparent: {entityId}");
                return;
            }
            ReparentEntity(entity, newParent);
        }

        public void ReparentEntity(Entity? entity, NitroxId? newParentId)
        {
            Entity parentEntity = newParentId != null ? GetEntityById(newParentId).Value : null;
            ReparentEntity(entity, parentEntity);
        }

        public void ReparentEntity(Entity? entity, Entity? newParent)
        {
            RemoveFromParent(entity);
            if (newParent == null)
            {
                return;
            }
            if (entity is WorldEntity worldEntity && worldEntity.Transform != null &&
                newParent is WorldEntity parentWorldEntity && parentWorldEntity.Transform != null)
            {
                worldEntity.Transform.SetParent(parentWorldEntity.Transform, true);
            }
            entity.ParentId = newParent.Id;
            AddChildReplacingSameId(newParent, entity);
        }

        /// <summary>
        ///     True when <paramref name="entity" /> is the entity with <paramref name="ancestorId" /> or lies somewhere
        ///     below it. Putting an entity under such an entity would make it its own ancestor.
        /// </summary>
        public bool IsSelfOrDescendantOf(Entity entity, NitroxId ancestorId)
        {
            HashSet<NitroxId> visited = [];
            Entity? current = entity;
            // The visited set ends the walk should the stored parents already form a cycle.
            while (current != null && visited.Add(current.Id))
            {
                if (current.Id.Equals(ancestorId))
                {
                    return true;
                }
                if (current.ParentId == null || !TryGetEntityById(current.ParentId, out current))
                {
                    return false;
                }
            }
            return false;
        }

        public void TransferChildren(NitroxId parentId, NitroxId newParentId, Func<Entity, bool> filter = null)
        {
            if (!TryGetEntityById(parentId, out Entity parentEntity))
            {
                logger.ZLogError($"[{nameof(TransferChildren)}] Couldn't find origin parent entity for {parentId}");
                return;
            }
            if (!TryGetEntityById(newParentId, out Entity newParentEntity))
            {
                logger.ZLogError($"[{nameof(TransferChildren)}] Couldn't find new parent entity for {newParentId}");
                return;
            }
            TransferChildren(parentEntity, newParentEntity, filter);
        }

        public void TransferChildren(Entity parent, Entity newParent, Func<Entity, bool> filter = null)
        {
            List<Entity> childrenToMove = filter != null ?
                [.. parent.ChildEntities.Where(filter)] : parent.ChildEntities;

            // In case parent == newParent (which is actually a case used) we need removal to happen before adding the entities back
            parent.ChildEntities.RemoveAll(entity => filter(entity));

            foreach (Entity childEntity in childrenToMove)
            {
                childEntity.ParentId = newParent.Id;
                AddChildReplacingSameId(newParent, childEntity);
            }
        }
    }
}
