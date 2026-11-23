using System;
using System.Collections;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.Helper;
using Nitrox.Model.Subnautica.Packets;
using NitroxClient.Communication.Abstract;
using NitroxClient.MonoBehaviours;
using UnityEngine;

namespace NitroxClient.GameLogic;

/// <summary>
///     The pictures of what a Scanner Room camera saw last, which the rooms' screens show. The game takes one right
///     before it takes a player out of a camera's view (MapRoomCamera.CaptureScreenAndExitLockedMode), in that
///     player's game only. This class sends it to everybody else and shows the pictures the others send.
/// </summary>
public sealed class MapRoomCameraPreviews(IPacketSender packetSender, ScannerRoomClientDiagnostics diagnostics)
{
    private const int PREVIEW_JPEG_QUALITY = 55;

    /// <summary>
    ///     The latest picture another player sent, for as long as it is not on the screens.
    /// </summary>
    private MapRoomCameraPreview pendingPreview;

    private bool pendingPreviewPixelsApplied;

    /// <summary>
    ///     Stands in for the game's routine that takes a picture of a camera's view and then takes the player out of
    ///     it. The picture is sent between the two: before that the game has not taken it.
    /// </summary>
    public IEnumerator BroadcastAfterCapture(IEnumerator captureRoutine, MapRoomCamera camera) =>
        PublishAfterCapturedFrame(captureRoutine, () => BroadcastPreview(camera));

    /// <summary>
    ///     Handles the picture another player took of what a camera sees. Every screen shows the latest one.
    /// </summary>
    public void ProcessPreview(MapRoomCameraPreview packet)
    {
        if (!MapRoomCameraPreviewImage.TryValidate(packet.JpegBytes, out int width, out int height))
        {
            diagnostics.Record("preview_receive", "reject", packet.CameraId, reason: $"invalid_jpeg_bytes_{packet.JpegBytes?.Length ?? 0}");
            return;
        }
        diagnostics.Record("preview_receive", "ok", packet.CameraId, reason: $"bytes_{packet.JpegBytes.Length}_{width}x{height}");
        pendingPreview = packet;
        pendingPreviewPixelsApplied = false;
        ShowPending();
    }

    /// <summary>
    ///     Shows the latest picture. Its camera may not have spawned on this client yet, as for a player who has just
    ///     joined; the picture is kept until it has. So this is called again whenever a camera spawns, and when one
    ///     gets its number, which the screens show with the picture.
    /// </summary>
    public void ShowPending()
    {
        if (pendingPreview == null)
        {
            return;
        }
        MapRoomCamera camera = FindCamera(pendingPreview.CameraId);
        RenderTexture renderTexture = FindSharedPreviewTexture(camera);
        if (!pendingPreviewPixelsApplied && renderTexture)
        {
            pendingPreviewPixelsApplied = TryApplyPreviewPixels(renderTexture, pendingPreview.JpegBytes);
            if (!pendingPreviewPixelsApplied)
            {
                diagnostics.Record("preview_apply", "reject", pendingPreview.CameraId, reason: "decode_failed");
                pendingPreview = null;
                return;
            }
        }
        if (!pendingPreviewPixelsApplied || !camera)
        {
            diagnostics.Record("preview_apply", "pending", pendingPreview.CameraId, reason: renderTexture ? "camera_missing" : "missing_texture");
            return;
        }
        ShowOnScreens(camera);
        diagnostics.Record("preview_apply", "ok", pendingPreview.CameraId, slot: camera.GetCameraNumber());
        pendingPreview = null;
        pendingPreviewPixelsApplied = false;
    }

    /// <summary>
    ///     Runs a routine of the game that waits for the end of the frame to take a picture, and calls
    ///     <paramref name="publish" /> once, right after that wait.
    /// </summary>
    internal static IEnumerator PublishAfterCapturedFrame(IEnumerator routine, Action publish)
    {
        bool published = false;
        while (routine.MoveNext())
        {
            object yielded = routine.Current;
            yield return yielded;
            if (!published && yielded is WaitForEndOfFrame)
            {
                published = true;
                try
                {
                    publish();
                }
                catch (Exception ex)
                {
                    // A cosmetic preview failure must not prevent the wrapped vanilla coroutine from
                    // removing its command buffer and returning the player from locked camera mode.
                    Log.Error(ex, "Failed to publish Scanner Room camera preview");
                }
            }
        }
    }

    private void BroadcastPreview(MapRoomCamera camera)
    {
        if (!camera || !camera.TryGetNitroxId(out NitroxId cameraId) || !camera.mapRoomRenderTexture)
        {
            return;
        }

        try
        {
            byte[] jpegBytes = CapturePreviewJpeg(camera.mapRoomRenderTexture);
            if (MapRoomCameraPreviewImage.TryValidate(jpegBytes, out int width, out int height))
            {
                packetSender.Send(new MapRoomCameraPreview(cameraId, jpegBytes));
                diagnostics.Record("preview_publish", "ok", cameraId, slot: camera.GetCameraNumber(),
                    reason: $"bytes_{jpegBytes.Length}_{width}x{height}");
            }
            else
            {
                diagnostics.Record("preview_publish", "reject", cameraId, slot: camera.GetCameraNumber(),
                    reason: $"invalid_jpeg_bytes_{jpegBytes?.Length ?? 0}");
                Log.Warn($"[{nameof(MapRoomCameraPreviews)}] Dropped invalid or oversized local camera preview for {cameraId}");
            }
        }
        catch (Exception ex)
        {
            // Preview publication is cosmetic and must never strand the player in camera control mode.
            diagnostics.Record("preview_publish", "reject", cameraId, slot: camera.GetCameraNumber(),
                reason: "capture_exception");
            Log.Error(ex, $"[{nameof(MapRoomCameraPreviews)}] Failed to capture camera preview for {cameraId}");
        }
    }

    private static byte[] CapturePreviewJpeg(RenderTexture source)
    {
        const int SIZE = MapRoomCameraPreviewImage.MAX_DIMENSION;
        RenderTexture temporary = RenderTexture.GetTemporary(SIZE, SIZE, 0, RenderTextureFormat.ARGB32,
            RenderTextureReadWrite.Default);
        RenderTexture previous = RenderTexture.active;
        Texture2D readback = null;
        try
        {
            Graphics.Blit(source, temporary);
            RenderTexture.active = temporary;
            readback = new Texture2D(SIZE, SIZE, TextureFormat.RGB24, mipChain: false);
            readback.ReadPixels(new Rect(0f, 0f, SIZE, SIZE), 0, 0, recalculateMipMaps: false);
            readback.Apply(updateMipmaps: false, makeNoLongerReadable: false);
            return ImageConversion.EncodeToJPG(readback, PREVIEW_JPEG_QUALITY);
        }
        finally
        {
            RenderTexture.active = previous;
            if (readback)
            {
                UnityEngine.Object.Destroy(readback);
            }
            RenderTexture.ReleaseTemporary(temporary);
        }
    }

    private static MapRoomCamera FindCamera(NitroxId cameraId)
    {
        if (NitroxEntity.TryGetObjectFrom(cameraId, out GameObject cameraObject) && cameraObject &&
            cameraObject.TryGetComponent(out MapRoomCamera camera))
        {
            return camera;
        }
        foreach (MapRoomCamera candidate in MapRoomCamera.cameras)
        {
            if (candidate && candidate.TryGetNitroxId(out NitroxId candidateId) && candidateId == cameraId)
            {
                return candidate;
            }
        }
        return null;
    }

    private static RenderTexture FindSharedPreviewTexture(MapRoomCamera camera)
    {
        if (camera && camera.mapRoomRenderTexture)
        {
            return camera.mapRoomRenderTexture;
        }
        foreach (MapRoomCamera candidate in MapRoomCamera.cameras)
        {
            if (candidate && candidate.mapRoomRenderTexture)
            {
                return candidate.mapRoomRenderTexture;
            }
        }
        foreach (MapRoomFunctionality mapRoom in Resources.FindObjectsOfTypeAll<MapRoomFunctionality>())
        {
            if (mapRoom && mapRoom.gameObject.scene.IsValid() && mapRoom.mapRoomRenderTexture)
            {
                return mapRoom.mapRoomRenderTexture;
            }
        }
        return null;
    }

    private static bool TryApplyPreviewPixels(RenderTexture target, byte[] jpegBytes)
    {
        Texture2D decoded = null;
        try
        {
            decoded = new Texture2D(2, 2, TextureFormat.RGB24, mipChain: false);
            if (!ImageConversion.LoadImage(decoded, jpegBytes, markNonReadable: true) ||
                decoded.width > MapRoomCameraPreviewImage.MAX_DIMENSION ||
                decoded.height > MapRoomCameraPreviewImage.MAX_DIMENSION)
            {
                return false;
            }
            Graphics.Blit(decoded, target);
            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"[{nameof(MapRoomCameraPreviews)}] Failed to apply remote camera preview");
            return false;
        }
        finally
        {
            if (decoded)
            {
                UnityEngine.Object.Destroy(decoded);
            }
        }
    }

    /// <summary>
    ///     Makes every screen show the camera a picture is of, as the game does when the local player flies a camera.
    ///     A screen the local player flies a camera with is left alone: the game would take the camera of the picture
    ///     for the one the player is at, and cycle on from there.
    /// </summary>
    private static void ShowOnScreens(MapRoomCamera camera)
    {
        foreach (MapRoomScreen screen in Resources.FindObjectsOfTypeAll<MapRoomScreen>())
        {
            if (screen && screen.gameObject.scene.IsValid() && !screen.currentCamera)
            {
                screen.OnMapRoomCameraChanged(camera);
            }
        }
    }
}
