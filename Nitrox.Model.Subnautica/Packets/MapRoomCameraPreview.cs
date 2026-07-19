using System;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Packets;

namespace Nitrox.Model.Subnautica.Packets;

/// <summary>
///     A bounded, session-only copy of vanilla's process-global Scanner Room camera preview.
///     It is intentionally absent from save and initial-sync models.
/// </summary>
[Serializable]
public sealed class MapRoomCameraPreview : Packet
{
    // Loose cameras have no persisted room record, so their process-global vanilla list number is
    // presentation-only. This bound is far above practical camera counts while rejecting absurd UI values.
    public const int MAX_LOOSE_CAMERA_NUMBER = 4096;

    public NitroxId CameraId { get; }
    public int CameraNumber { get; }
    public byte[] JpegBytes { get; }
    public bool IsServerResponse { get; }
    public bool Granted { get; }
    public long Revision { get; }

    public MapRoomCameraPreview(NitroxId cameraId, int cameraNumber, byte[] jpegBytes,
        bool isServerResponse = false, bool granted = false, long revision = 0)
    {
        CameraId = cameraId;
        CameraNumber = cameraNumber;
        JpegBytes = jpegBytes ?? Array.Empty<byte>();
        IsServerResponse = isServerResponse;
        Granted = granted;
        Revision = revision;
    }

    public static bool IsValidLooseCameraNumber(int cameraNumber) =>
        cameraNumber is >= 1 and <= MAX_LOOSE_CAMERA_NUMBER;

    public override string ToString() =>
        $"[MapRoomCameraPreview - CameraId: {CameraId}, CameraNumber: {CameraNumber}, Bytes: {JpegBytes.Length}, IsServerResponse: {IsServerResponse}, Granted: {Granted}, Revision: {Revision}]";
}
