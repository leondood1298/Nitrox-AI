using System;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Packets;

namespace Nitrox.Model.Subnautica.Packets;

/// <summary>
///     Sent by the player who simulates a Scanner Room camera when the camera left its dock, and passed on by the
///     server to the other players. The server also sends it to everyone when a docked camera has to leave its dock
///     for another reason: it was picked up, or its room was deconstructed.
/// </summary>
[Serializable]
public class MapRoomCameraUndocked(NitroxId cameraId, NitroxId mapRoomId) : Packet
{
    public NitroxId CameraId { get; } = cameraId;
    public NitroxId MapRoomId { get; } = mapRoomId;
}
