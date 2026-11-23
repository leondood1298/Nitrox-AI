using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Nitrox.Model.Constants;

public static class LauncherConstants
{
    public const string GRPC_LISTEN_PORT_TEMP_FILE_NAME = "nitrox-launcher-grpc-port.txt";
    public const string GRPC_NAMED_PIPE_NAME = "nitrox-launcher";

    /// <summary>
    ///     The pipe a launcher listens on for its servers.
    /// </summary>
    /// <remarks>
    ///     The name carries the installation folder, shared by the launcher and the servers it starts: two builds in
    ///     different folders, or upstream Nitrox next to this fork, otherwise met on one pipe and took each other's servers.
    /// </remarks>
    public static string GetNamedPipeName(string installationFolder) => $"{GRPC_NAMED_PIPE_NAME}-{GetInstallationId(installationFolder)}";

    /// <summary>
    ///     The temporary file through which the servers find their launcher's pipe or port.
    /// </summary>
    public static string GetConnectionFileName(string installationFolder) => $"{Path.GetFileNameWithoutExtension(GRPC_LISTEN_PORT_TEMP_FILE_NAME)}-{GetInstallationId(installationFolder)}.txt";

    /// <summary>
    ///     A short id that is the same for every program started from one installation folder.
    /// </summary>
    public static string GetInstallationId(string installationFolder)
    {
        string normalized = Path.GetFullPath(installationFolder).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).ToUpperInvariant();
        using SHA256 sha = SHA256.Create();
        byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(normalized));
        return BitConverter.ToString(hash, 0, 4).Replace("-", "").ToLowerInvariant();
    }
}
