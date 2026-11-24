using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using Nitrox.Model.Logger;
using FileAttributes = System.IO.FileAttributes;

namespace Nitrox.Launcher.Models.Utils;

/// <summary>
///     Puts the Nitrox entry point into Subnautica's Assembly-CSharp.dll and takes it out again.
/// </summary>
/// <remarks>
///     The game's own file is only ever swapped in one atomic replace, after the new file is fully written and checked.
///     The vanilla file is kept next to it (<see cref="BACKUP_NAME" />) and refreshed whenever the game's own file
///     changes (a game update or a file check by the store), so "restore game files" can always put it back.
/// </remarks>
public static class NitroxEntryPatch
{
    public const string GAME_ASSEMBLY_NAME = "Assembly-CSharp.dll";
    public const string NITROX_ASSEMBLY_NAME = "NitroxPatcher.dll";
    public const string GAME_ASSEMBLY_MODIFIED_NAME = "Assembly-CSharp-Nitrox.dll";
    public const string BACKUP_NAME = "Assembly-CSharp.dll.nitrox-backup";
    private const string BACKUP_TEMP_NAME = "Assembly-CSharp.dll.nitrox-backup.tmp";

    private const string NITROX_ENTRY_TYPE_NAME = "Main";
    private const string NITROX_ENTRY_METHOD_NAME = "Execute";

    private const string TARGET_TYPE_NAME = "PlatformUtils";
    private const string TARGET_METHOD_NAME = "Awake";

    private const string NITROX_EXECUTE_INSTRUCTION = "System.Void NitroxPatcher.Main::Execute()";

    private const string VERIFY_FILES_ADVICE = "Let your game store check the game files (Steam: Properties > Installed Files > Verify integrity of game files), then try again.";

    public static string GetManagedPath(string subnauticaBasePath) => Path.Combine(subnauticaBasePath, GameInfo.Subnautica.DataFolder, "Managed");

    /// <summary>
    ///     Inject Nitrox entry point into Subnautica's Assembly-CSharp.dll
    /// </summary>
    /// <exception cref="EntryPatchException">With a message for the player when the game's files can't be prepared.</exception>
    public static Task Apply(string subnauticaBasePath) => Task.Run(() => Apply(subnauticaBasePath, EntryPatchFiles.Default));

    internal static void Apply(string subnauticaBasePath, EntryPatchFiles files)
    {
        ArgumentException.ThrowIfNullOrEmpty(subnauticaBasePath);

        string managedPath = GetManagedPath(subnauticaBasePath);
        string assemblyCSharp = Path.Combine(managedPath, GAME_ASSEMBLY_NAME);
        string nitroxPatcherPath = Path.Combine(managedPath, NITROX_ASSEMBLY_NAME);
        string modifiedAssemblyCSharp = Path.Combine(managedPath, GAME_ASSEMBLY_MODIFIED_NAME);
        string backup = Path.Combine(managedPath, BACKUP_NAME);

        // Leftovers of an earlier run that ended between steps. The game's own file is never one of them.
        DeleteLeftover(files, modifiedAssemblyCSharp);
        DeleteLeftover(files, Path.Combine(managedPath, BACKUP_TEMP_NAME));

        byte[] current = ReadGameCode(files, assemblyCSharp);
        if (!files.Exists(nitroxPatcherPath))
        {
            throw new EntryPatchException($"{NITROX_ASSEMBLY_NAME} is missing from the game folder '{managedPath}'.");
        }

        using ModuleDefMD module = LoadGameCode(current, assemblyCSharp);
        MethodDef awakeMethod = FindEntryMethod(module);
        if (IsNitroxEntryCall(awakeMethod.Body.Instructions.FirstOrDefault()))
        {
            Log.Info("Subnautica already has Nitrox entry patch");
            if (!files.Exists(backup))
            {
                // Patched by a Nitrox version that kept no backup: the closest thing to vanilla is the file without our call.
                Log.Info($"No {BACKUP_NAME} exists, making one from the patched file");
                RemoveNitroxEntryCalls(awakeMethod);
                WriteBackup(files, managedPath, ToBytes(module));
            }
            return;
        }

        // The game's file has no entry point, so it is vanilla: new, updated by the game, or checked by the store.
        WriteBackup(files, managedPath, current);

        byte[] patched;
        using (ModuleDefMD nitroxPatcherAssembly = ModuleDefMD.Load(files.ReadAllBytes(nitroxPatcherPath)))
        {
            TypeDef nitroxMainDefinition = nitroxPatcherAssembly.GetTypes().FirstOrDefault(x => x.Name == NITROX_ENTRY_TYPE_NAME);
            MethodDef? executeMethodDefinition = nitroxMainDefinition?.Methods.FirstOrDefault(x => x.Name == NITROX_ENTRY_METHOD_NAME);
            if (executeMethodDefinition == null)
            {
                throw new EntryPatchException($"{NITROX_ASSEMBLY_NAME} in the game folder has no entry point. Reinstall Nitrox.");
            }

            /*
             * private void Awake()
             * {
             *     NitroxPatcher.Main.Execute(); <--- [INSERTED LINE]
             *     if (PlatformUtils._main != null)
             *     ...
             */
            MemberRef executeMethodReference = module.Import(executeMethodDefinition);
            awakeMethod.Body.Instructions.Insert(0, OpCodes.Call.ToInstruction(executeMethodReference));
            patched = ToBytes(module);
        }

        files.WriteAllBytes(modifiedAssemblyCSharp, patched);
        if (!files.Exists(modifiedAssemblyCSharp) || !files.ReadAllBytes(modifiedAssemblyCSharp).AsSpan().SequenceEqual(patched))
        {
            DeleteLeftover(files, modifiedAssemblyCSharp);
            throw new EntryPatchException($"The patched game code could not be written to '{managedPath}' (it was removed or changed right after writing, often by antivirus software). The game's files were not changed.");
        }

        try
        {
            files.Replace(modifiedAssemblyCSharp, assemblyCSharp);
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"Replacing {GAME_ASSEMBLY_NAME} failed");
            DeleteLeftover(files, modifiedAssemblyCSharp);
            EnsureGameCodePresent(files, assemblyCSharp, backup);
            throw new EntryPatchException($"Nitrox could not update the game's code file ({ex.Message}). Close every running Subnautica and try again.", ex);
        }

        if (!files.Exists(assemblyCSharp) || !files.ReadAllBytes(assemblyCSharp).AsSpan().SequenceEqual(patched))
        {
            EnsureGameCodePresent(files, assemblyCSharp, backup);
            throw new EntryPatchException($"The game's code file did not hold the patched code after replacing it. {VERIFY_FILES_ADVICE}");
        }
        Log.Info("Added Nitrox entry point to Subnautica");
    }

    /// <summary>
    ///     Copies this launcher's NitroxPatcher.dll into the game folder and makes sure the game will load that very file.
    /// </summary>
    /// <remarks>
    ///     A copy refused because a running game holds the old file is fine only if the old file is this version: an older
    ///     patcher would run against this launcher's newer dependencies.
    /// </remarks>
    /// <exception cref="EntryPatchException">When the game folder keeps a different NitroxPatcher.dll.</exception>
    public static void InstallPatcher(string sourcePatcherPath, string subnauticaBasePath)
    {
        string target = Path.Combine(GetManagedPath(subnauticaBasePath), NITROX_ASSEMBLY_NAME);
        if (!File.Exists(sourcePatcherPath))
        {
            throw new EntryPatchException($"The launcher's own {NITROX_ASSEMBLY_NAME} is missing at '{sourcePatcherPath}'. Reinstall Nitrox.");
        }

        Exception? copyError = null;
        try
        {
            if (File.Exists(target))
            {
                File.SetAttributes(target, FileAttributes.Normal);
            }
            File.Copy(sourcePatcherPath, target, true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            copyError = ex;
            Log.Warn($"Unable to copy {NITROX_ASSEMBLY_NAME} into the game folder: {ex.Message}");
        }

        byte[] expected = File.ReadAllBytes(sourcePatcherPath);
        byte[]? actual = null;
        try
        {
            actual = File.Exists(target) ? File.ReadAllBytes(target) : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            copyError ??= ex;
        }
        if (actual == null || !actual.AsSpan().SequenceEqual(expected))
        {
            throw new EntryPatchException($"{NITROX_ASSEMBLY_NAME} in the game folder could not be updated to this Nitrox version ({copyError?.Message ?? "the copied file differs"}). Close every running Subnautica and try again.", copyError);
        }
    }

    /// <summary>
    ///     Puts the vanilla game code back from the backup and removes the Nitrox files from the game folder.
    /// </summary>
    /// <returns>A sentence for the player saying what was done.</returns>
    public static Task<string> RestoreGameFiles(string subnauticaBasePath) => Task.Run(() => RestoreGameFiles(subnauticaBasePath, EntryPatchFiles.Default));

    internal static string RestoreGameFiles(string subnauticaBasePath, EntryPatchFiles files)
    {
        ArgumentException.ThrowIfNullOrEmpty(subnauticaBasePath);

        string managedPath = GetManagedPath(subnauticaBasePath);
        string assemblyCSharp = Path.Combine(managedPath, GAME_ASSEMBLY_NAME);
        string modifiedAssemblyCSharp = Path.Combine(managedPath, GAME_ASSEMBLY_MODIFIED_NAME);
        string backup = Path.Combine(managedPath, BACKUP_NAME);

        DeleteLeftover(files, modifiedAssemblyCSharp);
        DeleteLeftover(files, Path.Combine(managedPath, BACKUP_TEMP_NAME));

        string result;
        if (files.Exists(backup))
        {
            byte[] vanilla = files.ReadAllBytes(backup);
            using (ModuleDefMD backupModule = LoadGameCode(vanilla, backup))
            {
                if (IsNitroxEntryCall(FindEntryMethod(backupModule).Body.Instructions.FirstOrDefault()))
                {
                    throw new EntryPatchException($"The backup '{backup}' holds patched code, so it can't be restored. {VERIFY_FILES_ADVICE}");
                }
            }
            ReplaceGameCode(files, assemblyCSharp, modifiedAssemblyCSharp, vanilla);
            files.Delete(backup);
            result = "The game's original code file was put back";
        }
        else
        {
            byte[] current = ReadGameCode(files, assemblyCSharp);
            using ModuleDefMD module = LoadGameCode(current, assemblyCSharp);
            MethodDef awakeMethod = FindEntryMethod(module);
            if (!IsNitroxEntryCall(awakeMethod.Body.Instructions.FirstOrDefault()))
            {
                result = "The game's code file was already unmodified";
            }
            else
            {
                RemoveNitroxEntryCalls(awakeMethod);
                ReplaceGameCode(files, assemblyCSharp, modifiedAssemblyCSharp, ToBytes(module));
                result = "No backup existed, so the Nitrox entry point was taken out of the game's code file";
            }
        }

        foreach (string nitroxFile in new[] { NITROX_ASSEMBLY_NAME, Path.ChangeExtension(NITROX_ASSEMBLY_NAME, ".pdb") })
        {
            string path = Path.Combine(managedPath, nitroxFile);
            if (files.Exists(path))
            {
                try
                {
                    files.Delete(path);
                }
                catch (Exception ex)
                {
                    throw new EntryPatchException($"{result}, but '{path}' could not be removed ({ex.Message}). Close every running Subnautica and try again.", ex);
                }
            }
        }
        Log.Info($"Restored game files: {result}");
        return $"{result} and the Nitrox files were removed from the game folder.";
    }

    public static bool IsPatchApplied(string subnauticaBasePath)
    {
        string assemblyCSharp = Path.Combine(GetManagedPath(subnauticaBasePath), GAME_ASSEMBLY_NAME);
        using ModuleDefMD module = ModuleDefMD.Load(File.ReadAllBytes(assemblyCSharp));
        return IsNitroxEntryCall(FindEntryMethod(module).Body.Instructions.FirstOrDefault());
    }

    private static void ReplaceGameCode(EntryPatchFiles files, string assemblyCSharp, string temporaryPath, byte[] content)
    {
        files.WriteAllBytes(temporaryPath, content);
        if (!files.Exists(temporaryPath) || !files.ReadAllBytes(temporaryPath).AsSpan().SequenceEqual(content))
        {
            DeleteLeftover(files, temporaryPath);
            throw new EntryPatchException("The game's code file could not be written (it was removed or changed right after writing, often by antivirus software). The game's files were not changed.");
        }
        try
        {
            if (files.Exists(assemblyCSharp))
            {
                files.Replace(temporaryPath, assemblyCSharp);
            }
            else
            {
                files.Move(temporaryPath, assemblyCSharp);
            }
        }
        catch (Exception ex)
        {
            DeleteLeftover(files, temporaryPath);
            throw new EntryPatchException($"Nitrox could not update the game's code file ({ex.Message}). Close every running Subnautica and try again.", ex);
        }
        if (!files.Exists(assemblyCSharp) || !files.ReadAllBytes(assemblyCSharp).AsSpan().SequenceEqual(content))
        {
            throw new EntryPatchException($"The game's code file did not hold the expected code after replacing it. {VERIFY_FILES_ADVICE}");
        }
    }

    /// <summary>
    ///     After a failed replace the game's file must still be there; put the vanilla backup back if it is not.
    /// </summary>
    private static void EnsureGameCodePresent(EntryPatchFiles files, string assemblyCSharp, string backup)
    {
        try
        {
            if (!files.Exists(assemblyCSharp) && files.Exists(backup))
            {
                Log.Warn($"{GAME_ASSEMBLY_NAME} is missing after a failed replace, putting the backup back");
                files.WriteAllBytes(assemblyCSharp, files.ReadAllBytes(backup));
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"Could not put the backup of {GAME_ASSEMBLY_NAME} back");
        }
    }

    private static void WriteBackup(EntryPatchFiles files, string managedPath, byte[] vanilla)
    {
        string backup = Path.Combine(managedPath, BACKUP_NAME);
        if (files.Exists(backup) && files.ReadAllBytes(backup).AsSpan().SequenceEqual(vanilla))
        {
            return;
        }
        string temporary = Path.Combine(managedPath, BACKUP_TEMP_NAME);
        files.WriteAllBytes(temporary, vanilla);
        if (!files.Exists(temporary) || !files.ReadAllBytes(temporary).AsSpan().SequenceEqual(vanilla))
        {
            DeleteLeftover(files, temporary);
            throw new EntryPatchException($"The backup of the game's code could not be written to '{managedPath}' (it was removed or changed right after writing, often by antivirus software). The game's files were not changed.");
        }
        files.Move(temporary, backup);
        Log.Info($"Stored the game's vanilla code as {BACKUP_NAME}");
    }

    private static byte[] ReadGameCode(EntryPatchFiles files, string assemblyCSharp)
    {
        if (!files.Exists(assemblyCSharp))
        {
            throw new EntryPatchException($"The game's code file '{assemblyCSharp}' is missing. {VERIFY_FILES_ADVICE}");
        }
        byte[] content;
        try
        {
            content = files.ReadAllBytes(assemblyCSharp);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new EntryPatchException($"The game's code file '{assemblyCSharp}' can't be read ({ex.Message}). Close every running Subnautica and try again.", ex);
        }
        if (content.Length == 0)
        {
            throw new EntryPatchException($"The game's code file '{assemblyCSharp}' is empty. {VERIFY_FILES_ADVICE}");
        }
        return content;
    }

    private static ModuleDefMD LoadGameCode(byte[] content, string path)
    {
        try
        {
            return ModuleDefMD.Load(content);
        }
        catch (Exception ex) when (ex is BadImageFormatException or IOException)
        {
            throw new EntryPatchException($"The game's code file '{path}' is damaged. {VERIFY_FILES_ADVICE}", ex);
        }
    }

    private static MethodDef FindEntryMethod(ModuleDefMD module)
    {
        MethodDef? method = module.GetTypes().FirstOrDefault(x => x.FullName == TARGET_TYPE_NAME)?.Methods.FirstOrDefault(x => x.Name == TARGET_METHOD_NAME);
        if (method?.Body == null)
        {
            throw new EntryPatchException($"The game's code has no {TARGET_TYPE_NAME}.{TARGET_METHOD_NAME}, so this game version is not supported by Nitrox.");
        }
        return method;
    }

    private static bool IsNitroxEntryCall(Instruction? instruction) => instruction?.Operand?.ToString() == NITROX_EXECUTE_INSTRUCTION;

    private static void RemoveNitroxEntryCalls(MethodDef method)
    {
        IList<Instruction> instructions = method.Body.Instructions;
        for (int i = instructions.Count - 1; i >= 0; i--)
        {
            if (IsNitroxEntryCall(instructions[i]))
            {
                instructions.RemoveAt(i);
            }
        }
    }

    private static byte[] ToBytes(ModuleDefMD module)
    {
        using MemoryStream stream = new();
        module.Write(stream);
        return stream.ToArray();
    }

    private static void DeleteLeftover(EntryPatchFiles files, string path)
    {
        try
        {
            if (files.Exists(path))
            {
                files.Delete(path);
            }
        }
        catch (Exception ex)
        {
            Log.Warn($"Could not delete leftover file '{path}': {ex.Message}");
        }
    }
}

/// <summary>
///     The file operations the entry patch uses, so tests can make each one fail.
/// </summary>
internal class EntryPatchFiles
{
    public static readonly EntryPatchFiles Default = new();

    public virtual bool Exists(string path) => File.Exists(path);

    public virtual byte[] ReadAllBytes(string path) => File.ReadAllBytes(path);

    public virtual void WriteAllBytes(string path, byte[] content)
    {
        using FileStream stream = new(path, FileMode.Create, FileAccess.Write, FileShare.None);
        stream.Write(content);
        stream.Flush(true);
    }

    /// <summary>
    ///     Swaps the target for the source in one step: the target is either the old or the new file, never missing.
    /// </summary>
    public virtual void Replace(string source, string target)
    {
        // Store-installed game files can be read-only, which File.Replace refuses.
        File.SetAttributes(target, FileAttributes.Normal);
        File.Replace(source, target, null, true);
    }

    public virtual void Move(string source, string target) => File.Move(source, target, true);

    public virtual void Delete(string path)
    {
        File.SetAttributes(path, FileAttributes.Normal);
        File.Delete(path);
    }
}

public sealed class EntryPatchException(string message, Exception? innerException = null) : Exception(message, innerException);
