using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Linq;
using BinaryPack.Attributes;
using Nitrox.Model.DataStructures;
using Nitrox.Model.DataStructures.Unity;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;

namespace Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Bases;

[Serializable, DataContract]
public class MapRoomEntity : GlobalRootEntity
{
    [DataMember(Order = 1)]
    public NitroxInt3 Cell { get; set; }

    [DataMember(Order = 2)]
    public NitroxId? LeftDockCameraId { get; set; }

    [DataMember(Order = 3)]
    public NitroxId? RightDockCameraId { get; set; }

    [DataMember(Order = 4)]
    public long DockingRevision { get; set; }

    [DataMember(Order = 5)]
    public List<MapRoomCameraRecord> CameraRegistry { get; set; } = [];

    [IgnoreConstructor]
    protected MapRoomEntity()
    {
        // Constructor for serialization. Has to be "protected" for json serialization.
    }

    public MapRoomEntity(NitroxId id, NitroxId parentId, NitroxInt3 cell)
    {
        Id = id;
        ParentId = parentId;
        Cell = cell;

        Transform = new();
    }

    /// <remarks>
    /// Used for deserialization.
    /// <see cref="WorldEntity.SpawnedByServer"/> is set to true because this entity is meant to receive simulation locks
    /// </remarks>
    public MapRoomEntity(NitroxInt3 cell, NitroxId? leftDockCameraId, NitroxId? rightDockCameraId, long dockingRevision, List<MapRoomCameraRecord> cameraRegistry, NitroxTransform transform, int level, string classId, bool spawnedByServer, NitroxId id, NitroxTechType techType, EntityMetadata metadata, NitroxId parentId, List<Entity> childEntities) :
        base(transform, level, classId, true, id, techType, metadata, parentId, childEntities)
    {
        Cell = cell;
        LeftDockCameraId = leftDockCameraId;
        RightDockCameraId = rightDockCameraId;
        DockingRevision = dockingRevision;
        CameraRegistry = cameraRegistry ?? [];
    }

    public NitroxId? GetDockedCamera(int dockingIndex) => dockingIndex == 0 ? LeftDockCameraId : RightDockCameraId;

    public void SetDockedCamera(int dockingIndex, NitroxId cameraId)
    {
        if (dockingIndex == 0)
        {
            LeftDockCameraId = cameraId;
        }
        else
        {
            RightDockCameraId = cameraId;
        }
        DockingRevision++;
    }

    public bool TryClearDockedCamera(int dockingIndex, NitroxId cameraId)
    {
        if (GetDockedCamera(dockingIndex) != cameraId)
        {
            return false;
        }
        if (dockingIndex == 0)
        {
            LeftDockCameraId = null;
        }
        else
        {
            RightDockCameraId = null;
        }
        DockingRevision++;
        return true;
    }

    public bool IsCameraDocked(NitroxId cameraId) => LeftDockCameraId == cameraId || RightDockCameraId == cameraId;

    public int GetOrAssignCameraNumber(NitroxId cameraId, int preferredNumber)
    {
        MapRoomCameraRecord? existing = CameraRegistry.Find(record => record.CameraId == cameraId);
        if (existing != null)
        {
            return existing.CameraNumber;
        }
        int cameraNumber = preferredNumber > 0 && CameraRegistry.TrueForAll(record => record.CameraNumber != preferredNumber)
            ? preferredNumber
            : CameraRegistry.Count == 0 ? 1 : CameraRegistry.Max(record => record.CameraNumber) + 1;
        CameraRegistry.Add(new MapRoomCameraRecord(cameraId, cameraNumber));
        return cameraNumber;
    }

    public override string ToString()
    {
        return $"[MapRoomEntity Id: {Id}, Cell: {Cell}, LeftDockCameraId: {LeftDockCameraId}, RightDockCameraId: {RightDockCameraId}, DockingRevision: {DockingRevision}, RegisteredCameras: {CameraRegistry.Count}]";
    }
}
