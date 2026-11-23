using System;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Packets;

namespace Nitrox.Model.Subnautica.Packets;

/// <summary>
///     The picture of what a Scanner Room camera sees that every screen shows for the camera that was last flown. The
///     player who controls a camera sends one when they leave its view, once for every time they take control, and the
///     server passes it on to the other players.
/// </summary>
[Serializable]
public class MapRoomCameraPreview(NitroxId cameraId, byte[] jpegBytes) : Packet
{
    public NitroxId CameraId { get; } = cameraId;
    public byte[] JpegBytes { get; } = jpegBytes;
}
