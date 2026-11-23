using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;
using Nitrox.Server.Subnautica.Models.GameLogic.Entities;

namespace Nitrox.Server.Subnautica.Models.GameLogic.PictureFrames;

/// <summary>
///     Keeps the pictures players put into picture frames with the world. A frame's metadata names its picture; a
///     picture no frame names any more (its frame was deconstructed, or shows another one now) no longer counts towards
///     the world's room for pictures, and its file is removed the next time pictures are stored.
/// </summary>
internal sealed class PictureFrameStore(EntityRegistry entityRegistry, PictureFrameFiles files, IOptions<SubnauticaServerOptions> options, ILogger<PictureFrameStore> logger)
{
    private const string PICTURE_FRAME_TECH_TYPE = "PictureFrame";

    private readonly EntityRegistry entityRegistry = entityRegistry;
    private readonly PictureFrameFiles files = files;
    private readonly IOptions<SubnauticaServerOptions> options = options;
    private readonly ILogger<PictureFrameStore> logger = logger;
    private readonly Lock storeLock = new();

    public long MaxPictureBytes => options.Value.PictureFrameMaxKilobytes * 1024L;

    public long MaxTotalBytes => options.Value.PictureFramesMaxTotalMegabytes * 1024L * 1024L;

    public static string HashOf(byte[] picture) => Convert.ToHexStringLower(SHA256.HashData(picture));

    /// <summary>
    ///     Keeps <paramref name="picture" /> as what the frame shows, if it fits.
    /// </summary>
    /// <param name="limit">For a refusal, the limit that was exceeded, in bytes.</param>
    /// <returns>Null when the picture was kept: the frame's metadata names it now.</returns>
    public PictureFrameRefusal? TryStore(NitroxId frameId, byte[] picture, out string hash, out long limit)
    {
        hash = HashOf(picture);
        limit = 0;
        lock (storeLock)
        {
            if (!entityRegistry.TryGetEntityById(frameId, out Entity frame) || frame.TechType?.Name != PICTURE_FRAME_TECH_TYPE)
            {
                return PictureFrameRefusal.NO_FRAME;
            }
            if (picture.Length == 0 || picture.Length > MaxPictureBytes)
            {
                limit = MaxPictureBytes;
                return PictureFrameRefusal.TOO_LARGE;
            }

            HashSet<string> referenced = ReferencedHashes(frameId);
            if (!referenced.Contains(hash))
            {
                long used = referenced.Sum(files.SizeOf);
                if (used + picture.Length > MaxTotalBytes)
                {
                    limit = Math.Max(0, MaxTotalBytes - used);
                    return PictureFrameRefusal.WORLD_FULL;
                }
                files.Write(hash, picture);
            }
            frame.Metadata = new PictureFrameMetadata(hash);
            referenced.Add(hash);
            RemoveUnreferenced(referenced);
            return null;
        }
    }

    /// <summary>
    ///     The bytes of the picture a frame shows, for a player who asks for that picture.
    /// </summary>
    public byte[]? GetPictureOf(NitroxId frameId, string hash)
    {
        if (!entityRegistry.TryGetEntityById(frameId, out Entity frame) || frame.Metadata is not PictureFrameMetadata metadata || metadata.PictureHash != hash ||
            !PictureFrameFiles.IsHash(hash))
        {
            return null;
        }
        lock (storeLock)
        {
            return files.Read(hash);
        }
    }

    /// <summary>
    ///     The room the pictures that frames show take, in bytes.
    /// </summary>
    public long UsedBytes()
    {
        lock (storeLock)
        {
            return ReferencedHashes(null).Sum(files.SizeOf);
        }
    }

    /// <param name="exceptFrameId">A frame whose picture is about to be replaced, which no longer counts.</param>
    private HashSet<string> ReferencedHashes(NitroxId? exceptFrameId)
    {
        HashSet<string> hashes = [];
        foreach (Entity entity in entityRegistry.GetAllEntities())
        {
            if (entity.Metadata is PictureFrameMetadata { PictureHash: { } hash } && entity.Id != exceptFrameId && PictureFrameFiles.IsHash(hash))
            {
                hashes.Add(hash);
            }
        }
        return hashes;
    }

    private void RemoveUnreferenced(HashSet<string> referenced)
    {
        foreach (string hash in files.All().ToList())
        {
            if (referenced.Contains(hash))
            {
                continue;
            }
            try
            {
                files.Delete(hash);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                logger.ZLogWarning($"Could not remove the unused picture {hash}: {exception.Message}");
            }
        }
    }
}
