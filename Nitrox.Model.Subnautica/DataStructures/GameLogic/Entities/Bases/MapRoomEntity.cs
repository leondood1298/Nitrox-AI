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

    [DataMember(Order = 6)]
    public long ScanResultGeneration { get; set; }

    [DataMember(Order = 7)]
    public long ScanResultRevision { get; set; }

    [DataMember(Order = 8)]
    public List<MapRoomScanResultRecord> ScanResults { get; set; } = [];

    [DataMember(Order = 9)]
    public long AvailableScanTypesRevision { get; set; }

    [DataMember(Order = 10)]
    public List<NitroxTechType> AvailableScanTypes { get; set; } = [];

    [DataMember(Order = 11)]
    public CrafterMetadata? FabricatorMetadata { get; set; }

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
    public MapRoomEntity(NitroxInt3 cell, NitroxId? leftDockCameraId, NitroxId? rightDockCameraId, long dockingRevision, List<MapRoomCameraRecord> cameraRegistry, long scanResultGeneration, long scanResultRevision, List<MapRoomScanResultRecord> scanResults, long availableScanTypesRevision, List<NitroxTechType> availableScanTypes, CrafterMetadata? fabricatorMetadata, NitroxTransform transform, int level, string classId, bool spawnedByServer, NitroxId id, NitroxTechType techType, EntityMetadata metadata, NitroxId parentId, List<Entity> childEntities) :
        base(transform, level, classId, true, id, techType, metadata, parentId, childEntities)
    {
        Cell = cell;
        LeftDockCameraId = leftDockCameraId;
        RightDockCameraId = rightDockCameraId;
        DockingRevision = dockingRevision;
        CameraRegistry = cameraRegistry ?? [];
        ScanResultRevision = scanResultRevision;
        ScanResults = scanResults ?? [];
        AvailableScanTypesRevision = availableScanTypesRevision;
        AvailableScanTypes = availableScanTypes ?? [];
        FabricatorMetadata = fabricatorMetadata;
        ScanResultGeneration = scanResultGeneration == 0 && ScanResults.Count == 0 && metadata is MapRoomMetadata mapRoomMetadata
            ? mapRoomMetadata.Generation
            : scanResultGeneration;
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

    public MapRoomCameraRecord? GetCameraRecord(NitroxId cameraId) => CameraRegistry.Find(record => record.CameraId == cameraId);

    public bool TryApplyScanResult(long generation, MapRoomScanResultRecord result)
    {
        if (generation != ScanResultGeneration || string.IsNullOrEmpty(result.ResourceId))
        {
            return false;
        }
        MapRoomScanResultRecord? current = ScanResults.Find(record => record.ResourceId == result.ResourceId);
        if (current == null)
        {
            ScanResults.Add(result);
        }
        else
        {
            if (current.TechType.Equals(result.TechType) && current.Position.Equals(result.Position))
            {
                return false;
            }
            current.TechType = result.TechType;
            current.Position = result.Position;
        }
        ScanResultRevision++;
        return true;
    }

    public bool TryRemoveScanResult(long generation, string resourceId)
    {
        if (generation != ScanResultGeneration)
        {
            return false;
        }
        int removed = ScanResults.RemoveAll(record => record.ResourceId == resourceId);
        if (removed == 0)
        {
            return false;
        }
        ScanResultRevision++;
        return true;
    }

    public void BeginScanResultGeneration(long generation)
    {
        if (generation <= ScanResultGeneration)
        {
            return;
        }
        ScanResultGeneration = generation;
        ScanResultRevision++;
        ScanResults.Clear();
    }

    public override string ToString()
    {
        return $"[MapRoomEntity Id: {Id}, Cell: {Cell}, LeftDockCameraId: {LeftDockCameraId}, RightDockCameraId: {RightDockCameraId}, DockingRevision: {DockingRevision}, RegisteredCameras: {CameraRegistry.Count}, ScanResultGeneration: {ScanResultGeneration}, ScanResultRevision: {ScanResultRevision}, ScanResults: {ScanResults.Count}]";
    }
}
