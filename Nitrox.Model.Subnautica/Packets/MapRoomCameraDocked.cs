using System;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Packets;

namespace Nitrox.Model.Subnautica.Packets;

/// <summary>
///     Sent by the player who simulates a Scanner Room camera when the camera went into a dock, and passed on by the
///     server to the other players. The server makes the camera a child of the room.
/// </summary>
[Serializable]
public class MapRoomCameraDocked(NitroxId cameraId, NitroxId mapRoomId, int dockingIndex) : Packet
{
    public NitroxId CameraId { get; } = cameraId;
    public NitroxId MapRoomId { get; } = mapRoomId;
    public int DockingIndex { get; } = dockingIndex;
}
