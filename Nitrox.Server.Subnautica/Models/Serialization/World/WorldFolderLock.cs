using System.Globalization;
using System.IO;
using System.Text;

namespace Nitrox.Server.Subnautica.Models.Serialization.World;

/// <summary>
///     Keeps a second server off a world folder for as long as the first one runs.
/// </summary>
/// <remarks>
///     Two servers on one world each save their own state over the other's. The lock is an open file the operating system
///     releases when the process ends, so a crashed or killed server never leaves the world locked.
/// </remarks>
internal sealed class WorldFolderLock : IDisposable
{
    public const string LOCK_FILE_NAME = "server.lock";

    private readonly FileStream stream;

    private WorldFolderLock(FileStream stream)
    {
        this.stream = stream;
    }

    /// <param name="holderProcessId">The process holding the world when it is taken, if it could be read.</param>
    public static WorldFolderLock? TryAcquire(string saveDir, out int? holderProcessId)
    {
        Directory.CreateDirectory(saveDir);
        string path = Path.Combine(saveDir, LOCK_FILE_NAME);
        try
        {
            // Others may read who holds it, but not open it to take it
            FileStream stream = new(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read | FileShare.Delete, 4096, FileOptions.DeleteOnClose);
            stream.SetLength(0);
            byte[] processId = Encoding.ASCII.GetBytes(Environment.ProcessId.ToString(CultureInfo.InvariantCulture));
            stream.Write(processId);
            stream.Flush();
            holderProcessId = null;
            return new WorldFolderLock(stream);
        }
        catch (IOException)
        {
            holderProcessId = ReadHolder(path);
            return null;
        }
    }

    public void Dispose() => stream.Dispose();

    private static int? ReadHolder(string path)
    {
        try
        {
            using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using StreamReader reader = new(stream, Encoding.ASCII);
            return int.TryParse(reader.ReadToEnd(), NumberStyles.None, CultureInfo.InvariantCulture, out int processId) ? processId : null;
        }
        catch (IOException)
        {
            return null;
        }
    }
}
