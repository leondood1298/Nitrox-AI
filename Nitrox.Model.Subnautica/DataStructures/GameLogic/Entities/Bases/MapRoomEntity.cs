using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using BinaryPack.Attributes;
using Nitrox.Model.DataStructures;
using Nitrox.Model.DataStructures.Unity;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;

namespace Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Bases;

/// <summary>
///     A Scanner Room: its camera docks, the cameras that belong to it and what its fabricator is crafting. What it
///     scans for and how far the scan has got is its metadata (<see cref="MapRoomMetadata" />). What the scan has found
///     is no part of the entity. It is neither saved nor sent to a joining player with the room; the room's simulating
///     player reports it and the server sends it to the players who subscribe.
/// </summary>
[Serializable, DataContract]
public class MapRoomEntity : GlobalRootEntity
{
    /// <summary>
    ///     A Scanner Room has a left and a right camera dock, addressed by docking index 0 and 1.
    /// </summary>
    public const int DOCK_COUNT = 2;
    private const int LEFT_DOCK_INDEX = 0;
    private const int RIGHT_DOCK_INDEX = 1;

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
    public MapRoomEntity(NitroxInt3 cell, NitroxId? leftDockCameraId, NitroxId? rightDockCameraId, long dockingRevision, List<MapRoomCameraRecord> cameraRegistry, CrafterMetadata? fabricatorMetadata, NitroxTransform transform, int level, string classId, bool spawnedByServer, NitroxId id, NitroxTechType techType, EntityMetadata metadata, NitroxId parentId, List<Entity> childEntities) :
        base(transform, level, classId, true, id, techType, metadata, parentId, childEntities)
    {
        Cell = cell;
        LeftDockCameraId = leftDockCameraId;
        RightDockCameraId = rightDockCameraId;
        DockingRevision = dockingRevision;
        CameraRegistry = cameraRegistry ?? [];
        FabricatorMetadata = fabricatorMetadata;
        NormalizeCameraState();
    }

    public NitroxId? GetDockedCamera(int dockingIndex) => dockingIndex == LEFT_DOCK_INDEX ? LeftDockCameraId : RightDockCameraId;

    public void SetDockedCamera(int dockingIndex, NitroxId cameraId)
    {
        if (dockingIndex == LEFT_DOCK_INDEX)
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
        if (dockingIndex == LEFT_DOCK_INDEX)
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

    public bool TryClearDockedCamera(NitroxId cameraId, out int dockingIndex)
    {
        dockingIndex = LeftDockCameraId == cameraId ? LEFT_DOCK_INDEX : RightDockCameraId == cameraId ? RIGHT_DOCK_INDEX : -1;
        return dockingIndex >= 0 && TryClearDockedCamera(dockingIndex, cameraId);
    }

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

    internal int NormalizeCameraState()
    {
        CameraRegistry ??= [];
        int changes = 0;
        HashSet<NitroxId> seenIds = [];
        HashSet<int> seenNumbers = [];
        for (int i = 0; i < CameraRegistry.Count; i++)
        {
            MapRoomCameraRecord? record = CameraRegistry[i];
            if (record == null || record.CameraId == null || !seenIds.Add(record.CameraId))
            {
                CameraRegistry.RemoveAt(i);
                i--;
                changes++;
            }
        }
        int nextNumber = 1;
        foreach (MapRoomCameraRecord record in CameraRegistry)
        {
            if (record.CameraNumber > 0 && seenNumbers.Add(record.CameraNumber))
            {
                continue;
            }
            while (seenNumbers.Contains(nextNumber))
            {
                nextNumber++;
            }
            record.CameraNumber = nextNumber;
            seenNumbers.Add(nextNumber);
            changes++;
        }
        if (LeftDockCameraId != null && LeftDockCameraId == RightDockCameraId)
        {
            RightDockCameraId = null;
            DockingRevision++;
            changes++;
        }
        if (LeftDockCameraId != null && GetCameraRecord(LeftDockCameraId) == null)
        {
            GetOrAssignCameraNumber(LeftDockCameraId, LEFT_DOCK_INDEX + 1);
            changes++;
        }
        if (RightDockCameraId != null && GetCameraRecord(RightDockCameraId) == null)
        {
            GetOrAssignCameraNumber(RightDockCameraId, RIGHT_DOCK_INDEX + 1);
            changes++;
        }
        return changes;
    }

    public bool RemoveCamera(NitroxId cameraId, out int dockingIndex)
    {
        bool clearedDock = TryClearDockedCamera(cameraId, out dockingIndex);
        bool removedRecord = CameraRegistry.RemoveAll(record => record.CameraId == cameraId) > 0;
        return clearedDock || removedRecord;
    }

    public override string ToString()
    {
        return $"[MapRoomEntity Id: {Id}, Cell: {Cell}, LeftDockCameraId: {LeftDockCameraId}, RightDockCameraId: {RightDockCameraId}, DockingRevision: {DockingRevision}, RegisteredCameras: {CameraRegistry.Count}]";
    }
}
