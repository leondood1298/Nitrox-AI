using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using Newtonsoft.Json;

namespace Nitrox.Server.Subnautica.Models.Serialization.World;

/// <summary>
///     Writes the files of a save as one generation and finds the newest whole generation again.
/// </summary>
/// <remarks>
///     <para>
///         Each file used to be replaced on its own. A save cut short (an exception, a killed process, a console window
///         closed while saving) left new player data next to old entity data, which loads as duplicated or lost items.
///     </para>
///     <para>
///         A generation is first written whole to <see cref="STAGING_FOLDER_NAME" />, each file flushed to disk, with a
///         manifest of names, sizes and SHA-256 hashes written last. Only then is the current generation moved to
///         <see cref="PREVIOUS_FOLDER_NAME" /> and the new one moved into the world folder, its manifest last. Every step
///         is a rename, so wherever the save stops, one whole generation can be found by its manifest: the new one, or the
///         one before it. The world folder keeps its familiar layout for the launcher.
///     </para>
/// </remarks>
internal sealed class SaveGenerationStore
{
    public const string MANIFEST_FILE_NAME = "SaveManifest.json";
    internal const string STAGING_FOLDER_NAME = ".save-new";
    internal const string PREVIOUS_FOLDER_NAME = ".save-previous";
    internal const string PREVIOUS_STAGING_FOLDER_NAME = ".save-previous-new";
    internal const string RESTORE_FOLDER_NAME = ".save-restore";
    private const string RECOVERED_FOLDER_NAME = ".save-recovered";
    private const string TRASH_FOLDER_NAME = ".save-trash";

    private readonly Action<string>? stepReached;

    /// <param name="stepReached">
    ///     Called after each step of a save with the step's name. Tests throw from it to cut a save short at that step.
    /// </param>
    public SaveGenerationStore(Action<string>? stepReached = null)
    {
        this.stepReached = stepReached;
    }

    /// <summary>
    ///     Folders the save keeps for itself; they never belong in a backup.
    /// </summary>
    public static IReadOnlyList<string> WorkingFolderNames { get; } = [STAGING_FOLDER_NAME, PREVIOUS_FOLDER_NAME, PREVIOUS_STAGING_FOLDER_NAME, RESTORE_FOLDER_NAME, RECOVERED_FOLDER_NAME, TRASH_FOLDER_NAME];

    /// <summary>
    ///     Writes <paramref name="files" /> as the world's new current generation.
    /// </summary>
    /// <exception cref="Exception">Anything a file's writer throws, or an I/O error. The previous generation stays loadable.</exception>
    public SaveManifest Write(string saveDir, IReadOnlyList<GenerationFile> files) => Write(saveDir, files, out _);

    /// <param name="changed">False when every file came out the same as in the generation it replaces.</param>
    public SaveManifest Write(string saveDir, IReadOnlyList<GenerationFile> files, out bool changed)
    {
        changed = true;
        Directory.CreateDirectory(saveDir);
        string staging = Path.Combine(saveDir, STAGING_FOLDER_NAME);
        string previous = Path.Combine(saveDir, PREVIOUS_FOLDER_NAME);
        string previousStaging = Path.Combine(saveDir, PREVIOUS_STAGING_FOLDER_NAME);

        KeepRecoveredGeneration(saveDir);
        // Leftovers of a save cut short are older than what is in memory now
        DeleteFolder(saveDir, staging);
        DeleteFolder(saveDir, previousStaging);
        Directory.CreateDirectory(staging);

        List<SaveManifestEntry> entries = [];
        foreach (GenerationFile file in files)
        {
            string path = Path.Combine(staging, file.Name);
            file.WriteTo(path);
            entries.Add(Describe(path, file.Name));
            Step($"staged {file.Name}");
        }
        SaveManifest manifest = new(NextGeneration(saveDir), entries);
        WriteManifest(Path.Combine(staging, MANIFEST_FILE_NAME), manifest);
        Step("staged manifest");

        // The generation being replaced is kept, but only when it is whole: after loading a fallback, the world folder
        // may hold pieces, and the whole generation that was loaded is the one in the previous folder.
        if (TryReadManifest(saveDir) is { } currentManifest && Resolve(currentManifest, [saveDir], GenerationSource.CURRENT) != null)
        {
            changed = !HaveSameFiles(currentManifest, manifest);
            Directory.CreateDirectory(previousStaging);
            // Manifest first: from here on, the world folder no longer claims a whole generation
            File.Move(Path.Combine(saveDir, MANIFEST_FILE_NAME), Path.Combine(previousStaging, MANIFEST_FILE_NAME));
            Step("moved current manifest");
            foreach (SaveManifestEntry entry in currentManifest.Files)
            {
                File.Move(Path.Combine(saveDir, entry.Name), Path.Combine(previousStaging, entry.Name));
                Step($"moved current {entry.Name}");
            }
            DeleteFolder(saveDir, previous);
            Step("removed old previous");
            Directory.Move(previousStaging, previous);
            Step("kept previous");
        }

        foreach (SaveManifestEntry entry in entries)
        {
            File.Move(Path.Combine(staging, entry.Name), Path.Combine(saveDir, entry.Name), true);
            Step($"committed {entry.Name}");
        }
        File.Move(Path.Combine(staging, MANIFEST_FILE_NAME), Path.Combine(saveDir, MANIFEST_FILE_NAME), true);
        Step("committed manifest");
        DeleteFolder(saveDir, staging);
        return manifest;
    }

    /// <summary>
    ///     After a save was cut short, the newest whole generation (the one a load used) can lie spread over the world
    ///     folder and the working folders that the next save clears. It is gathered into the previous folder first, so it
    ///     stays as the fallback until the new save is whole.
    /// </summary>
    private static void KeepRecoveredGeneration(string saveDir)
    {
        if (TryReadManifest(saveDir) is { } currentManifest && Resolve(currentManifest, [saveDir], GenerationSource.CURRENT) != null)
        {
            // The usual case: the world folder holds a whole generation, which the save moves to the previous folder
            return;
        }
        IReadOnlyList<ResolvedGeneration> generations = new SaveGenerationStore().FindGenerations(saveDir);
        if (generations.Count == 0)
        {
            return;
        }
        ResolvedGeneration newest = generations[0];
        string previous = Path.GetFullPath(Path.Combine(saveDir, PREVIOUS_FOLDER_NAME));
        if (newest.PathsByName.Values.All(path => string.Equals(Path.GetDirectoryName(Path.GetFullPath(path)), previous, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        string recovered = Path.Combine(saveDir, RECOVERED_FOLDER_NAME);
        DeleteFolder(saveDir, recovered);
        Directory.CreateDirectory(recovered);
        foreach ((string name, string path) in newest.PathsByName)
        {
            File.Copy(path, Path.Combine(recovered, name));
            Describe(Path.Combine(recovered, name), name);
        }
        WriteManifest(Path.Combine(recovered, MANIFEST_FILE_NAME), newest.Manifest);
        DeleteFolder(saveDir, previous);
        Directory.Move(recovered, previous);
    }

    /// <summary>
    ///     Finds the newest whole generation in the world folder, in the order a load should try them: the current one
    ///     (or a save that was finished in staging but not yet moved in), then the previous one.
    /// </summary>
    public IReadOnlyList<ResolvedGeneration> FindGenerations(string saveDir)
    {
        string staging = Path.Combine(saveDir, STAGING_FOLDER_NAME);
        List<ResolvedGeneration> latest = [];
        if (TryReadManifest(saveDir) is { } current && Resolve(current, [saveDir, staging], GenerationSource.CURRENT) is { } resolvedCurrent)
        {
            latest.Add(resolvedCurrent);
        }
        if (TryReadManifest(staging) is { } staged && Resolve(staged, [staging, saveDir], GenerationSource.UNFINISHED_SAVE) is { } resolvedStaged)
        {
            latest.Add(resolvedStaged);
        }

        List<ResolvedGeneration> older = [];
        foreach (string folder in new[] { Path.Combine(saveDir, PREVIOUS_STAGING_FOLDER_NAME), Path.Combine(saveDir, PREVIOUS_FOLDER_NAME) })
        {
            if (TryReadManifest(folder) is { } previous && Resolve(previous, [folder], GenerationSource.PREVIOUS) is { } resolvedPrevious)
            {
                older.Add(resolvedPrevious);
            }
        }

        return [..latest.OrderByDescending(generation => generation.Manifest.Generation), ..older.OrderByDescending(generation => generation.Manifest.Generation)];
    }

    /// <summary>
    ///     Unpacks the newest backup that holds a whole generation into the world's restore folder.
    /// </summary>
    /// <param name="skip">Backups already tried.</param>
    public ResolvedGeneration? ExtractNewestBackup(string saveDir, string backupsDir, ISet<string> skip)
    {
        if (!Directory.Exists(backupsDir))
        {
            return null;
        }
        string restore = Path.Combine(saveDir, RESTORE_FOLDER_NAME);
        foreach (FileInfo backup in new DirectoryInfo(backupsDir).EnumerateFiles("*.zip").OrderByDescending(file => file.LastWriteTimeUtc))
        {
            if (!skip.Add(backup.FullName))
            {
                continue;
            }
            try
            {
                DeleteFolder(saveDir, restore);
                ZipFile.ExtractToDirectory(backup.FullName, restore);
                if (TryReadManifest(restore) is { } manifest && Resolve(manifest, [restore], GenerationSource.BACKUP) is { } resolved)
                {
                    return resolved with { Description = $"backup \"{backup.Name}\"" };
                }
            }
            catch (Exception)
            {
                // A damaged archive is just not a candidate
            }
        }
        DeleteFolder(saveDir, restore);
        return null;
    }

    /// <summary>
    ///     Removes the folder a backup was unpacked into, once its data is loaded.
    /// </summary>
    public static void RemoveRestoreFolder(string saveDir) => DeleteFolder(saveDir, Path.Combine(saveDir, RESTORE_FOLDER_NAME));

    /// <summary>
    ///     Removes the save's own working folders, for when the world folder's files are replaced from outside (a restored
    ///     backup), so that nothing from before can be taken for a newer or a fallback generation.
    /// </summary>
    public static void RemoveWorkingFolders(string saveDir)
    {
        foreach (string folder in WorkingFolderNames)
        {
            DeleteFolder(saveDir, Path.Combine(saveDir, folder));
        }
    }

    internal static SaveManifest? TryReadManifest(string folder)
    {
        string path = Path.Combine(folder, MANIFEST_FILE_NAME);
        if (!File.Exists(path))
        {
            return null;
        }
        try
        {
            SaveManifest? manifest = JsonConvert.DeserializeObject<SaveManifest>(File.ReadAllText(path));
            return manifest is { Files: not null } ? manifest : null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static bool HaveSameFiles(SaveManifest a, SaveManifest b) =>
        a.Files.Count == b.Files.Count &&
        a.Files.All(entry => b.Files.Any(other => string.Equals(other.Name, entry.Name, StringComparison.OrdinalIgnoreCase) &&
                                                 string.Equals(other.Sha256, entry.Sha256, StringComparison.OrdinalIgnoreCase)));

    private static ResolvedGeneration? Resolve(SaveManifest manifest, IReadOnlyList<string> folders, GenerationSource source)
    {
        Dictionary<string, string> paths = new(StringComparer.OrdinalIgnoreCase);
        foreach (SaveManifestEntry entry in manifest.Files)
        {
            string? found = folders.Select(folder => Path.Combine(folder, entry.Name)).FirstOrDefault(path => Matches(path, entry));
            if (found == null)
            {
                return null;
            }
            paths[entry.Name] = found;
        }
        return new ResolvedGeneration(manifest, paths, source, DescribeSource(source));
    }

    private static string DescribeSource(GenerationSource source) => source switch
    {
        GenerationSource.CURRENT => "last save",
        GenerationSource.UNFINISHED_SAVE => "last save (finished writing but not yet put in place)",
        GenerationSource.PREVIOUS => "save before the last one",
        _ => "backup"
    };

    private static bool Matches(string path, SaveManifestEntry entry)
    {
        try
        {
            FileInfo info = new(path);
            if (!info.Exists || info.Length != entry.Length)
            {
                return false;
            }
            using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            return string.Equals(Convert.ToHexString(SHA256.HashData(stream)), entry.Sha256, StringComparison.OrdinalIgnoreCase);
        }
        catch (IOException)
        {
            return false;
        }
    }

    /// <summary>
    ///     Flushes a written file to disk and records what it must look like. A file only renamed into place while its data
    ///     still sits in the cache can come back empty after a power loss.
    /// </summary>
    private static SaveManifestEntry Describe(string path, string name)
    {
        using FileStream stream = new(path, FileMode.Open, FileAccess.ReadWrite, FileShare.Read);
        stream.Flush(true);
        string hash = Convert.ToHexString(SHA256.HashData(stream));
        return new SaveManifestEntry(name, stream.Length, hash);
    }

    private static void WriteManifest(string path, SaveManifest manifest)
    {
        using (FileStream stream = new(path, FileMode.Create, FileAccess.Write, FileShare.None))
        using (StreamWriter writer = new(stream))
        {
            writer.Write(JsonConvert.SerializeObject(manifest, Formatting.Indented));
            writer.Flush();
            stream.Flush(true);
        }
    }

    private static long NextGeneration(string saveDir)
    {
        long highest = new[] { saveDir, Path.Combine(saveDir, PREVIOUS_FOLDER_NAME), Path.Combine(saveDir, PREVIOUS_STAGING_FOLDER_NAME) }
                       .Select(TryReadManifest)
                       .Where(manifest => manifest != null)
                       .Select(manifest => manifest!.Generation)
                       .DefaultIfEmpty(0)
                       .Max();
        return highest + 1;
    }

    /// <summary>
    ///     Deletes a folder by renaming it away first, so that a delete cut short never leaves a half-empty folder that
    ///     could be read as a generation.
    /// </summary>
    private static void DeleteFolder(string saveDir, string folder)
    {
        if (!Directory.Exists(folder))
        {
            return;
        }
        string trash = Path.Combine(saveDir, TRASH_FOLDER_NAME);
        if (Directory.Exists(trash))
        {
            Directory.Delete(trash, true);
        }
        Directory.Move(folder, trash);
        Directory.Delete(trash, true);
    }

    private void Step(string name) => stepReached?.Invoke(name);
}

/// <summary>
///     A file of a save and how to write it to a given path.
/// </summary>
internal sealed record GenerationFile(string Name, Action<string> WriteTo);

internal sealed record SaveManifest(long Generation, IReadOnlyList<SaveManifestEntry> Files);

internal sealed record SaveManifestEntry(string Name, long Length, string Sha256);

internal enum GenerationSource
{
    CURRENT,
    UNFINISHED_SAVE,
    PREVIOUS,
    BACKUP
}

/// <summary>
///     A whole generation and where each of its files is.
/// </summary>
internal sealed record ResolvedGeneration(SaveManifest Manifest, IReadOnlyDictionary<string, string> PathsByName, GenerationSource Source, string Description)
{
    public string PathOf(string fileName) => PathsByName[fileName];
}
