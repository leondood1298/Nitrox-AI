using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Bases;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;
using Nitrox.Model.Subnautica.Packets;
using NitroxClient.Communication;
using NitroxClient.Communication.Abstract;
using NitroxClient.MonoBehaviours;
using UnityEngine;

namespace NitroxClient.GameLogic;

/// <summary>
///     Which Scanner Room camera sits in which dock. On the server a docked camera is a child entity of its room, as a
///     docked vehicle is a child of its moonpool. In the game a docked camera is an object of its own that the dock
///     holds on to. This class keeps the two alike: it tells the server when the game docks or undocks a camera this
///     client simulates, and docks or undocks the game's camera when the server says so.
/// </summary>
public sealed class MapRoomCameraDocks(IPacketSender packetSender, SimulationOwnership simulationOwnership, ScannerRoomClientDiagnostics diagnostics)
{
    private const float DOCK_POSITION_SORT_TOLERANCE = 0.01f;

    /// <summary>
    ///     The room and the docking index of every dock asked about so far. A dock never changes its room, and finding
    ///     the room means walking the whole base, which the game would otherwise have this class do for every camera
    ///     every frame.
    /// </summary>
    private static readonly ConditionalWeakTable<MapRoomCameraDocking, DockPlace> placesByDock = new();

    /// <summary>
    ///     The dock every camera this client undocked was in before. If the server refuses the undock, the camera goes
    ///     back there.
    /// </summary>
    private readonly Dictionary<NitroxId, MapRoomCameraDocking> lastDockByCameraId = [];

    /// <summary>
    ///     The room a dock belongs to and the dock's index in that room.
    /// </summary>
    public static bool TryGetPlace(MapRoomCameraDocking dockingPoint, out MapRoomFunctionality mapRoom, out int dockingIndex)
    {
        mapRoom = null;
        dockingIndex = MapRoomCameraMetadata.NOT_DOCKED;
        if (!dockingPoint)
        {
            return false;
        }
        DockPlace place = placesByDock.GetOrCreateValue(dockingPoint);
        if (!place.Room)
        {
            place.Room = FindRoom(dockingPoint);
            place.DockingIndex = place.Room ? GetDockingPoints(place.Room).IndexOf(dockingPoint) : MapRoomCameraMetadata.NOT_DOCKED;
        }
        mapRoom = place.Room;
        dockingIndex = place.DockingIndex;
        return mapRoom && dockingIndex >= 0;
    }

    /// <summary>
    ///     The docks of a room, in the order of their docking index. The game has no such order, so they are sorted by
    ///     where they are, which is the same on every client.
    /// </summary>
    public static List<MapRoomCameraDocking> GetDockingPoints(MapRoomFunctionality mapRoom)
    {
        List<MapRoomCameraDocking> dockingPoints = [];
        if (!mapRoom)
        {
            return dockingPoints;
        }
        Base parentBase = mapRoom.GetComponentInParent<Base>();
        if (parentBase)
        {
            foreach (MapRoomCameraDocking dockingPoint in parentBase.GetComponentsInChildren<MapRoomCameraDocking>(true))
            {
                if (dockingPoint && FindRoom(dockingPoint) == mapRoom)
                {
                    dockingPoints.Add(dockingPoint);
                }
            }
        }
        if (dockingPoints.Count == 0)
        {
            dockingPoints.AddRange(mapRoom.GetComponentsInChildren<MapRoomCameraDocking>(true));
        }
        dockingPoints.Sort((a, b) => CompareWorldPosition(a.transform.position, b.transform.position));
        return dockingPoints;
    }

    /// <summary>
    ///     Called after the game put a camera into a dock.
    /// </summary>
    public void CameraDocked(MapRoomCameraDocking dockingPoint, MapRoomCamera camera)
    {
        if (PacketSuppressor<MapRoomCameraDocked>.IsSuppressed || !camera || !camera.TryGetNitroxId(out NitroxId cameraId))
        {
            return;
        }
        if (!TryGetPlace(dockingPoint, out MapRoomFunctionality mapRoom, out int dockingIndex) || !mapRoom.TryGetNitroxId(out NitroxId mapRoomId))
        {
            return;
        }
        // The game docks every camera that drifts into a dock, on every client that sees it. Only the camera's
        // simulating player says so; the others are told by the server.
        if (!simulationOwnership.HasAnyLockType(cameraId))
        {
            return;
        }
        lastDockByCameraId.Remove(cameraId);
        packetSender.Send(new MapRoomCameraDocked(cameraId, mapRoomId, dockingIndex));
        diagnostics.Record("dock_publish", "ok", cameraId, mapRoomId, slot: dockingIndex);
    }

    /// <summary>
    ///     Called after the game took a camera out of its dock.
    /// </summary>
    public void CameraUndocked(MapRoomCameraDocking dockingPoint, MapRoomCamera camera)
    {
        if (PacketSuppressor<MapRoomCameraUndocked>.IsSuppressed || !camera || !camera.TryGetNitroxId(out NitroxId cameraId))
        {
            return;
        }
        if (!TryGetPlace(dockingPoint, out MapRoomFunctionality mapRoom, out int dockingIndex) || !mapRoom.TryGetNitroxId(out NitroxId mapRoomId))
        {
            return;
        }
        // A player who picks a camera up that somebody else simulates takes it out of its dock as well. The server
        // does that itself when it is told of the pickup.
        if (!simulationOwnership.HasAnyLockType(cameraId))
        {
            return;
        }
        lastDockByCameraId[cameraId] = dockingPoint;
        packetSender.Send(new MapRoomCameraUndocked(cameraId, mapRoomId));
        diagnostics.Record("undock_publish", "ok", cameraId, mapRoomId, slot: dockingIndex);
    }

    public void ProcessDocked(MapRoomCameraDocked packet)
    {
        if (!NitroxEntity.TryGetComponentFrom(packet.CameraId, out MapRoomCamera camera) ||
            !NitroxEntity.TryGetComponentFrom(packet.MapRoomId, out MapRoomFunctionality mapRoom))
        {
            diagnostics.Record("dock_apply", "pending", packet.CameraId, packet.MapRoomId, slot: packet.DockingIndex, reason: "waiting_object");
            return;
        }
        Dock(camera, mapRoom, packet.DockingIndex);
        diagnostics.Record("dock_apply", "ok", packet.CameraId, packet.MapRoomId, slot: packet.DockingIndex);
    }

    public void ProcessUndocked(MapRoomCameraUndocked packet)
    {
        if (NitroxEntity.TryGetComponentFrom(packet.CameraId, out MapRoomCamera camera))
        {
            Undock(camera);
            diagnostics.Record("undock_apply", "ok", packet.CameraId, packet.MapRoomId);
        }
    }

    /// <summary>
    ///     Makes a camera sit in the dock the server has it in, or in none. Called when the camera spawns, and when the
    ///     server sends the camera's state as the answer to a dock or undock it refused.
    /// </summary>
    public void ApplyDockingIndex(MapRoomCamera camera, int dockingIndex)
    {
        if (dockingIndex < 0)
        {
            Undock(camera);
            return;
        }
        if (camera.pickupAble && camera.pickupAble.attached)
        {
            // In an inventory. The answer to an undock can arrive after the pickup that followed it.
            return;
        }
        if (camera.dockingPoint && TryGetPlace(camera.dockingPoint, out _, out int currentIndex) && currentIndex == dockingIndex)
        {
            return;
        }
        // A camera that spawns as a child of its room is put under the room's object by the spawner. A camera this
        // client undocked and the server did not is put back where it was.
        MapRoomFunctionality mapRoom = camera.transform.parent ? camera.transform.parent.GetComponent<MapRoomFunctionality>() : null;
        if (!mapRoom && camera.TryGetNitroxId(out NitroxId cameraId) && lastDockByCameraId.TryGetValue(cameraId, out MapRoomCameraDocking lastDock))
        {
            TryGetPlace(lastDock, out mapRoom, out _);
        }
        if (mapRoom)
        {
            Dock(camera, mapRoom, dockingIndex);
        }
    }

    /// <summary>
    ///     Makes the two cameras a new Scanner Room comes with, as the game does in MapRoomCameraDocking.Start, which
    ///     does not run in multiplayer. Only the player who builds the room does this; everybody else is sent the
    ///     cameras by the server.
    /// </summary>
    public void CreateDefaultCameras(MapRoomFunctionality mapRoom)
    {
        foreach (MapRoomCameraDocking dockingPoint in GetDockingPoints(mapRoom))
        {
            if (dockingPoint.camera || !dockingPoint.cameraPrefab)
            {
                continue;
            }
            GameObject cameraObject = Object.Instantiate(dockingPoint.cameraPrefab);
            CrafterLogic.NotifyCraftEnd(cameraObject, TechType.MapRoomCamera);
            NitroxEntity.SetNewId(cameraObject, new NitroxId());
            using (PacketSuppressor<MapRoomCameraDocked>.Suppress())
            {
                dockingPoint.DockCamera(cameraObject.GetComponent<MapRoomCamera>());
            }
        }
    }

    /// <summary>
    ///     The cameras in a room's docks as the child entities of the room.
    /// </summary>
    public static List<GlobalRootEntity> GetCameraEntities(MapRoomFunctionality mapRoom, NitroxId mapRoomId)
    {
        List<GlobalRootEntity> cameras = [];
        List<MapRoomCameraDocking> dockingPoints = GetDockingPoints(mapRoom);
        for (int dockingIndex = 0; dockingIndex < dockingPoints.Count; dockingIndex++)
        {
            MapRoomCamera camera = dockingPoints[dockingIndex].camera;
            if (!camera || !camera.TryGetNitroxId(out NitroxId cameraId) || !camera.TryGetComponent(out PrefabIdentifier prefabIdentifier))
            {
                continue;
            }
            // Light, battery, health and number are the server's to keep. It only takes the dock from here.
            cameras.Add(new GlobalRootEntity(camera.transform.ToWorldDto(), (int)LargeWorldEntity.CellLevel.Global, prefabIdentifier.ClassId, true, cameraId,
                TechType.MapRoomCamera.ToDto(), new MapRoomCameraMetadata(dockingIndex: dockingIndex), mapRoomId, []));
        }
        return cameras;
    }

    /// <summary>
    ///     The cameras among the children of a room's entity, for spawning them.
    /// </summary>
    public static List<Entity> GetCameraEntities(MapRoomEntity mapRoom) => mapRoom.ChildEntities.FindAll(child => child is GlobalRootEntity);

    /// <summary>
    ///     Whether the game may put a camera that drifted into a free dock into that dock. It tries on every client
    ///     whose game sees the camera get there. Only the camera's simulating player lets it and tells the server;
    ///     everybody else is told by the server.
    /// </summary>
    public bool MayDockByItself(Collider other)
    {
        MapRoomCamera camera = other ? other.GetComponent<MapRoomCamera>() : null;
        return !camera || !camera.TryGetNitroxId(out NitroxId cameraId) || simulationOwnership.HasAnyLockType(cameraId);
    }

    /// <summary>
    ///     Makes a camera that sits in a dock immovable again. While another player controls a camera it is moved by
    ///     their reports, which takes a rigidbody that physics can move; a camera that ends up in a dock after that
    ///     would be pushed around by whatever touches it.
    /// </summary>
    public static void SettleInDock(GameObject cameraObject)
    {
        if (cameraObject && cameraObject.TryGetComponent(out MapRoomCamera camera) && camera.dockingPoint && camera.rigidBody)
        {
            camera.rigidBody.isKinematic = true;
        }
    }

    private static void Dock(MapRoomCamera camera, MapRoomFunctionality mapRoom, int dockingIndex)
    {
        List<MapRoomCameraDocking> dockingPoints = GetDockingPoints(mapRoom);
        if (dockingIndex >= dockingPoints.Count)
        {
            return;
        }
        MapRoomCameraDocking dockingPoint = dockingPoints[dockingIndex];
        if (dockingPoint.camera == camera && dockingPoint.cameraDocked)
        {
            return;
        }
        using (PacketSuppressor<MapRoomCameraDocked>.Suppress())
        using (PacketSuppressor<MapRoomCameraUndocked>.Suppress())
        {
            if (camera.dockingPoint)
            {
                camera.dockingPoint.UndockCamera();
            }
            if (dockingPoint.camera)
            {
                // The server has this dock for the camera that is to go in. Whatever the game left there has to make room.
                dockingPoint.UndockCamera();
            }
            // In the game a camera is never a child of its dock or its room, and lives on when either is destroyed.
            camera.transform.SetParent(LargeWorldStreamer.main ? LargeWorldStreamer.main.globalRoot.transform : null, true);
            dockingPoint.DockCamera(camera);
        }
        SettleInDock(camera.gameObject);
    }

    private static void Undock(MapRoomCamera camera)
    {
        if (!camera.dockingPoint)
        {
            return;
        }
        using (PacketSuppressor<MapRoomCameraUndocked>.Suppress())
        {
            camera.dockingPoint.UndockCamera();
        }
    }

    private static MapRoomFunctionality FindRoom(MapRoomCameraDocking dockingPoint)
    {
        Base parentBase = dockingPoint.GetComponentInParent<Base>();
        if (parentBase)
        {
            MapRoomFunctionality closestRoom = null;
            float closestSqrDistance = float.MaxValue;
            Vector3 position = dockingPoint.transform.position;
            foreach (MapRoomFunctionality room in parentBase.GetComponentsInChildren<MapRoomFunctionality>(true))
            {
                float sqrDistance = (room.transform.position - position).sqrMagnitude;
                if (sqrDistance < closestSqrDistance)
                {
                    closestSqrDistance = sqrDistance;
                    closestRoom = room;
                }
            }
            if (closestRoom)
            {
                return closestRoom;
            }
        }
        return dockingPoint.GetComponentInParent<MapRoomFunctionality>();
    }

    private static int CompareWorldPosition(Vector3 a, Vector3 b)
    {
        if (Mathf.Abs(a.x - b.x) > DOCK_POSITION_SORT_TOLERANCE)
        {
            return a.x.CompareTo(b.x);
        }
        if (Mathf.Abs(a.y - b.y) > DOCK_POSITION_SORT_TOLERANCE)
        {
            return a.y.CompareTo(b.y);
        }
        return a.z.CompareTo(b.z);
    }

    private sealed class DockPlace
    {
        public MapRoomFunctionality Room;
        public int DockingIndex = MapRoomCameraMetadata.NOT_DOCKED;
    }
}
