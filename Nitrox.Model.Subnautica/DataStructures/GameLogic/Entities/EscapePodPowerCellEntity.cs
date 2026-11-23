using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using BinaryPack.Attributes;
using Nitrox.Model.DataStructures;
using Nitrox.Model.DataStructures.Unity;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;

namespace Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities;

/// <summary>
///     One of the Lifepod's power cells, a child of its <see cref="EscapePodEntity" />. Its level is a base power
///     source's <see cref="PowerSourceMetadata" />.
///     <para>
///     A cell is part of the pod's scene object, has no PrefabIdentifier and no known path, so it is named by its place
///     among the pod's RegeneratePowerSource components (GetComponentsInChildren, inactive ones included).
///     </para>
///     <para>
///     A global root entity like the base power sources, so that it gets a simulating player the same way: every global
///     root entity is offered to a player who joins, and handed to another one when that player leaves.
///     </para>
/// </summary>
[Serializable, DataContract]
public class EscapePodPowerCellEntity : GlobalRootEntity
{
    [DataMember(Order = 1)]
    public int CellIndex { get; set; }

    [IgnoreConstructor]
    protected EscapePodPowerCellEntity()
    {
        // Constructor for serialization. Has to be "protected" for json serialization.
    }

    public EscapePodPowerCellEntity(NitroxId id, int cellIndex, NitroxId podId)
    {
        Id = id;
        CellIndex = cellIndex;
        ParentId = podId;
        TechType = NitroxTechType.None;
        SpawnedByServer = true;
        Transform = new();
        ChildEntities = [];
    }

    /// <remarks>Used for deserialization</remarks>
    public EscapePodPowerCellEntity(int cellIndex, NitroxTransform transform, int level, string classId, bool spawnedByServer, NitroxId id, NitroxTechType techType, EntityMetadata metadata, NitroxId parentId, List<Entity> childEntities) :
        base(transform, level, classId, spawnedByServer, id, techType, metadata, parentId, childEntities)
    {
        CellIndex = cellIndex;
    }

    public override string ToString()
    {
        return $"[EscapePodPowerCellEntity CellIndex: {CellIndex} {base.ToString()}]";
    }
}
