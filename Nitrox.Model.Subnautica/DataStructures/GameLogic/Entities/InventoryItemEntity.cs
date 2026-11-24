using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using BinaryPack.Attributes;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;

namespace Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities;

[Serializable]
[DataContract]
public class InventoryItemEntity : Entity
{
    [DataMember(Order = 1)]
    public string ClassId { get; set; }

    /// <summary>
    ///     Whether the item is lost when its player dies (Pickupable.destroyOnDeath): an item picked up in the open is,
    ///     until the player enters a base or vehicle, which secures everything the player carries.
    /// </summary>
    [DataMember(Order = 2)]
    public bool DestroyOnDeath { get; set; }

    [IgnoreConstructor]
    protected InventoryItemEntity()
    {
        // Constructor for serialization. Has to be "protected" for json serialization.
    }

    /// <remarks>Used for deserialization</remarks>
    public InventoryItemEntity(NitroxId id, string classId, NitroxTechType techType, EntityMetadata? metadata, NitroxId parentId, List<Entity> childEntities, bool destroyOnDeath = false)
    {
        DestroyOnDeath = destroyOnDeath;
        ClassId = classId;
        Id = id;
        TechType = techType;
        Metadata = metadata;
        ParentId = parentId;
        ChildEntities = childEntities;
    }

    public override string ToString()
    {
        return $"[InventoryItemEntity ClassId: {ClassId} DestroyOnDeath: {DestroyOnDeath} {base.ToString()}]";
    }
}
