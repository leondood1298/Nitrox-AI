using System.Collections.Generic;
using System.IO;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.Packets;
using NitroxClient.Communication.Abstract;
using NitroxClient.MonoBehaviours;
using UnityEngine;

namespace NitroxClient.GameLogic;

/// <summary>
///     Shares the pictures players put into picture frames. The player who picks a screenshot for a frame sends it to the
///     server, which keeps it with the world and tells the others which picture the frame shows. Their game asks for the
///     picture itself only once it wants to show it in full (the player came near the frame), and keeps it for other
///     frames showing the same picture.
/// </summary>
/// <remarks>
///     A frame showing a shared picture has a file name of its own (<see cref="SHARED_PREFIX" /> and the picture's hash),
///     which never names a local screenshot; <see cref="SetState" /> then stands in for the game's loading of the file.
/// </remarks>
public class PictureFrames(IPacketSender packetSender)
{
    internal const string SHARED_PREFIX = "nitrox-shared:";

    /// <summary>
    ///     Longest side, in pixels, of a picture as it is sent. A larger screenshot is scaled down before it is sent.
    /// </summary>
    private const int MAX_SIDE_PIXELS = 1920;

    private const int JPEG_QUALITY = 90;

    private readonly IPacketSender packetSender = packetSender;
    private readonly Dictionary<string, Texture2D> texturesByHash = [];
    private readonly HashSet<string> requestedHashes = [];
    private readonly Dictionary<string, List<PictureFrame>> framesWaitingByHash = [];

    /// <summary>
    ///     What each frame showed before this player put a picture into it, for a picture the server refuses.
    /// </summary>
    private readonly Dictionary<NitroxId, string?> previousFileNameByFrameId = [];

    /// <summary>
    ///     How large the last picture this player sent was, for the message of a refusal.
    /// </summary>
    private int lastSentSize;

    internal static string? SharedHashOf(string? fileName) =>
        fileName != null && fileName.StartsWith(SHARED_PREFIX) ? fileName[SHARED_PREFIX.Length..] : null;

    /// <summary>
    ///     This player picked a screenshot for a frame.
    /// </summary>
    public void OnPictureSelected(PictureFrame frame, string? previousFileName)
    {
        string fileName = frame.fileName;
        if (string.IsNullOrEmpty(fileName) || fileName == previousFileName || SharedHashOf(fileName) != null || !TryGetFrameId(frame, out NitroxId frameId))
        {
            return;
        }
        byte[]? picture = ReadPicture(fileName);
        if (picture == null)
        {
            Log.Warn($"Could not read the screenshot {fileName} put into picture frame {frameId}");
            ErrorMessage.AddMessage(Language.main.Get("Nitrox_PictureFrameNotShared"));
            return;
        }
        previousFileNameByFrameId[frameId] = previousFileName;
        packetSender.Send(new PictureFrameUpload(frameId, picture));
    }

    /// <summary>
    ///     The frame shows the given shared picture, or none.
    /// </summary>
    public void ShowSharedPicture(PictureFrame frame, string? pictureHash)
    {
        // Lets go of what it showed first (the game's own request for a local screenshot, say); the game's routine then
        // sets the state the frame should have from here on
        frame.SetState(PictureFrame.State.None);
        frame.fileName = pictureHash != null ? SHARED_PREFIX + pictureHash : null;
    }

    /// <summary>
    ///     Stands in for <see cref="PictureFrame.SetState" /> while the frame shows a shared picture.
    /// </summary>
    /// <returns>Whether the game's own code is to run (the frame shows a local screenshot).</returns>
    public bool SetState(PictureFrame frame, PictureFrame.State newState)
    {
        string? hash = SharedHashOf(frame.fileName);
        if (hash == null)
        {
            return true;
        }
        if (newState == frame.current)
        {
            return false;
        }

        switch (newState)
        {
            case PictureFrame.State.Full:
                if (texturesByHash.TryGetValue(hash, out Texture2D texture) && texture)
                {
                    frame.SetTexture(texture);
                    frame.current = PictureFrame.State.Full;
                    break;
                }
                // Shown once it has arrived. The frame stays at what it showed, so that the game does not take the
                // missing texture for a missing file.
                WaitFor(frame, hash);
                break;
            case PictureFrame.State.Thumbnail:
                frame.SetTexture(texturesByHash.TryGetValue(hash, out Texture2D thumbnail) && thumbnail ? thumbnail : null);
                frame.current = PictureFrame.State.Thumbnail;
                break;
            default:
                frame.SetTexture(null);
                frame.current = newState;
                break;
        }
        return false;
    }

    public void OnPictureReceived(string pictureHash, byte[] picture)
    {
        requestedHashes.Remove(pictureHash);
        Texture2D texture = new(2, 2);
        if (!texture.LoadImage(picture))
        {
            Log.Warn($"Could not read the shared picture {pictureHash}");
            Object.Destroy(texture);
            return;
        }
        texturesByHash[pictureHash] = texture;
        if (!framesWaitingByHash.TryGetValue(pictureHash, out List<PictureFrame> frames))
        {
            return;
        }
        framesWaitingByHash.Remove(pictureHash);
        foreach (PictureFrame frame in frames)
        {
            if (frame && SharedHashOf(frame.fileName) == pictureHash)
            {
                frame.SetTexture(texture);
                frame.current = PictureFrame.State.Full;
            }
        }
    }

    public void OnPictureRefused(NitroxId frameId, PictureFrameRefusal reason, long limit)
    {
        int size = lastSentSize;
        string message = reason switch
        {
            PictureFrameRefusal.TOO_LARGE => Language.main.Get("Nitrox_PictureFrameTooLarge"),
            PictureFrameRefusal.WORLD_FULL => Language.main.Get("Nitrox_PictureFrameWorldFull"),
            _ => Language.main.Get("Nitrox_PictureFrameNotShared")
        };
        ErrorMessage.AddMessage(message.Replace("{SIZE}", (size / 1024).ToString()).Replace("{LIMIT}", (limit / 1024).ToString()));

        // The frame goes back to what everybody else still sees in it
        if (previousFileNameByFrameId.TryGetValue(frameId, out string? previousFileName) && NitroxEntity.TryGetObjectFrom(frameId, out GameObject? frameObject))
        {
            PictureFrame frame = frameObject.GetComponentInChildren<PictureFrame>(true);
            if (frame)
            {
                frame.SetState(PictureFrame.State.None);
                frame.fileName = previousFileName;
            }
        }
        previousFileNameByFrameId.Remove(frameId);
    }

    private void WaitFor(PictureFrame frame, string hash)
    {
        if (!framesWaitingByHash.TryGetValue(hash, out List<PictureFrame> frames))
        {
            framesWaitingByHash[hash] = frames = [];
        }
        if (!frames.Contains(frame))
        {
            frames.Add(frame);
        }
        if (requestedHashes.Add(hash) && TryGetFrameId(frame, out NitroxId frameId))
        {
            packetSender.Send(new PictureFrameImageRequest(frameId, hash));
        }
    }

    private static bool TryGetFrameId(PictureFrame frame, out NitroxId frameId)
    {
        PrefabIdentifier identifier = frame.gameObject.FindAncestor<PrefabIdentifier>();
        if (identifier && identifier.TryGetNitroxId(out frameId))
        {
            return true;
        }
        frameId = null;
        return false;
    }

    /// <summary>
    ///     The screenshot as it is sent: its own file, or a smaller copy of it if it is large.
    /// </summary>
    private byte[]? ReadPicture(string fileName)
    {
        string path = ScreenshotManager.Combine(ScreenshotManager.savePath, fileName);
        if (!File.Exists(path))
        {
            return null;
        }
        byte[] bytes = File.ReadAllBytes(path);
        Texture2D texture = new(2, 2);
        try
        {
            if (!texture.LoadImage(bytes))
            {
                return null;
            }
            int longestSide = Mathf.Max(texture.width, texture.height);
            if (longestSide <= MAX_SIDE_PIXELS && IsJpeg(bytes))
            {
                lastSentSize = bytes.Length;
                return bytes;
            }
            float scale = Mathf.Min(1f, (float)MAX_SIDE_PIXELS / longestSide);
            Texture2D scaled = Scale(texture, Mathf.RoundToInt(texture.width * scale), Mathf.RoundToInt(texture.height * scale));
            byte[] encoded = scaled.EncodeToJPG(JPEG_QUALITY);
            Object.Destroy(scaled);
            lastSentSize = encoded.Length;
            return encoded;
        }
        finally
        {
            Object.Destroy(texture);
        }
    }

    private static bool IsJpeg(byte[] bytes) => bytes.Length > 2 && bytes[0] == 0xFF && bytes[1] == 0xD8;

    private static Texture2D Scale(Texture2D source, int width, int height)
    {
        RenderTexture renderTexture = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32);
        RenderTexture previous = RenderTexture.active;
        try
        {
            Graphics.Blit(source, renderTexture);
            RenderTexture.active = renderTexture;
            Texture2D scaled = new(width, height, TextureFormat.RGB24, false);
            scaled.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            scaled.Apply();
            return scaled;
        }
        finally
        {
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(renderTexture);
        }
    }
}
