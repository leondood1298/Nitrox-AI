using System;
using Nitrox.Model.Core;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Packets;

namespace Nitrox.Model.Subnautica.Packets;

/// <summary>
///     Says that a player controls a Scanner Room camera now, or no longer. Sent by that player once it holds the
///     exclusive simulation lock on the camera, and when it leaves the camera's view; the server passes it on to the
///     other players. The server sends it to everyone when control ends without the player's doing: the camera was
///     destroyed or picked up, its room was deconstructed, or the player left. A release only ends the control of the
///     player it names.
/// </summary>
[Serializable]
public class MapRoomCameraControlChanged(NitroxId cameraId, SessionId sessionId, bool isControlling) : Packet
{
    public NitroxId CameraId { get; } = cameraId;
    public SessionId SessionId { get; } = sessionId;
    public bool IsControlling { get; } = isControlling;
}
