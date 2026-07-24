using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using BinaryPack.Attributes;
using Nitrox.Model.DataStructures;
using Nitrox.Model.DataStructures.Unity;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Bases;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;

namespace Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Bases;

[Serializable, DataContract]
public class BuildEntity : GlobalRootEntity
{
    [DataMember(Order = 1)]
    public BaseData BaseData { get; set; }

    [IgnoredMember]
    public int OperationId;

    [IgnoreConstructor]
    protected BuildEntity()
    {
        // Constructor for serialization. Has to be "protected" for json serialization.
    }

    public static BuildEntity MakeEmpty()
    {
        return new BuildEntity();
    }

    /// <summary>
    /// Reports whether vanilla will treat this base as empty after its construction ghosts are restored.
    /// A malformed cell stream is reported by returning <c>false</c>.
    /// </summary>
    public bool TryIsStructurallyEmpty(out bool isStructurallyEmpty)
    {
        isStructurallyEmpty = false;
        if (BaseData == null || !BaseData.TryHasOccupiedCell(out bool hasOccupiedCell))
        {
            return false;
        }

        isStructurallyEmpty = !hasOccupiedCell && !ChildEntities.Any(entity => entity is GhostEntity);
        return true;
    }

    /// <remarks>
    /// Used for deserialization.
    /// <see cref="WorldEntity.SpawnedByServer"/> is set to true because this entity is meant to receive simulation locks
    /// </remarks>
    public BuildEntity(BaseData baseData, NitroxTransform transform, int level, string classId, bool spawnedByServer, NitroxId id, NitroxTechType techType, EntityMetadata metadata, NitroxId parentId, List<Entity> childEntities) :
        base(transform, level, classId, true, id, techType, metadata, parentId, childEntities)
    {
        BaseData = baseData;
    }

    public override string ToString()
    {
        return $"[BuildEntity Id: {Id}]";
    }
}
