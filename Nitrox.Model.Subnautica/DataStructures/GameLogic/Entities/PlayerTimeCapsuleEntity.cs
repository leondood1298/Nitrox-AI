using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using BinaryPack.Attributes;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;

namespace Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities;

/// <summary>
///     The time capsule in a rocket's cockpit, a child of the rocket's <see cref="VehicleEntity" />. The items put into it
///     are its children, like a locker's, and its text is its <see cref="PlayerTimeCapsuleMetadata" />.
///     <para>
///     The game has one PlayerTimeCapsule per game, a scene object outside the rocket, so this entity has no prefab and
///     no path: a client gives its id to that scene object.
///     </para>
/// </summary>
[Serializable, DataContract]
public class PlayerTimeCapsuleEntity : Entity
{
    [IgnoreConstructor]
    protected PlayerTimeCapsuleEntity()
    {
        // Constructor for serialization. Has to be "protected" for json serialization.
    }

    public PlayerTimeCapsuleEntity(NitroxId id, NitroxId rocketId, PlayerTimeCapsuleMetadata? metadata)
    {
        Id = id;
        ParentId = rocketId;
        TechType = NitroxTechType.None;
        Metadata = metadata;
        ChildEntities = [];
    }

    /// <remarks>Used for deserialization</remarks>
    public PlayerTimeCapsuleEntity(NitroxId id, NitroxTechType techType, EntityMetadata metadata, NitroxId parentId, List<Entity> childEntities)
    {
        Id = id;
        TechType = techType;
        Metadata = metadata;
        ParentId = parentId;
        ChildEntities = childEntities;
    }

    public override string ToString()
    {
        return $"[{nameof(PlayerTimeCapsuleEntity)} {base.ToString()}]";
    }
}
