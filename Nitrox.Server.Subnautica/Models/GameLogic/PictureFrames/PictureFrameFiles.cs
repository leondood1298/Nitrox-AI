using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace Nitrox.Server.Subnautica.Models.GameLogic.PictureFrames;

/// <summary>
///     The pictures of a world, one file each next to the world's other files (so that a backup of the world, which
///     copies those files, has them too), named by the SHA-256 of their bytes.
/// </summary>
internal sealed partial class PictureFrameFiles
{
    private const string FILE_PREFIX = "PictureFrame-";
    private const string FILE_EXTENSION = ".jpg";

    private readonly Func<string> directory;

    public PictureFrameFiles(IOptions<ServerStartOptions> startOptions) : this(() => startOptions.Value.GetServerSavePath())
    {
    }

    internal PictureFrameFiles(Func<string> directory)
    {
        this.directory = directory;
    }

    public static bool IsHash(string? hash) => hash != null && HashRegex().IsMatch(hash);

    public bool Exists(string hash) => File.Exists(PathOf(hash));

    public long SizeOf(string hash)
    {
        FileInfo file = new(PathOf(hash));
        return file.Exists ? file.Length : 0;
    }

    public byte[]? Read(string hash)
    {
        string path = PathOf(hash);
        return File.Exists(path) ? File.ReadAllBytes(path) : null;
    }

    public void Write(string hash, byte[] picture)
    {
        string path = PathOf(hash);
        if (File.Exists(path))
        {
            return;
        }
        Directory.CreateDirectory(directory());
        // Written aside first: a picture half written when the server stops would otherwise be kept as it is
        string partPath = $"{path}.part";
        File.WriteAllBytes(partPath, picture);
        File.Move(partPath, path, true);
    }

    public void Delete(string hash)
    {
        File.Delete(PathOf(hash));
    }

    /// <summary>
    ///     The hashes of all the pictures the world's folder holds.
    /// </summary>
    public IEnumerable<string> All()
    {
        if (!Directory.Exists(directory()))
        {
            yield break;
        }
        foreach (string path in Directory.EnumerateFiles(directory(), $"{FILE_PREFIX}*{FILE_EXTENSION}"))
        {
            string name = Path.GetFileNameWithoutExtension(path);
            string hash = name[FILE_PREFIX.Length..];
            if (IsHash(hash))
            {
                yield return hash;
            }
        }
    }

    private string PathOf(string hash)
    {
        if (!IsHash(hash))
        {
            throw new ArgumentException($"Not a picture hash: {hash}", nameof(hash));
        }
        return Path.Combine(directory(), $"{FILE_PREFIX}{hash}{FILE_EXTENSION}");
    }

    [GeneratedRegex("^[0-9a-f]{64}$")]
    private static partial Regex HashRegex();
}
