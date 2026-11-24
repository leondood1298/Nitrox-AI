using dnlib.DotNet;
using dnlib.DotNet.Emit;
using Nitrox.Model;
using Nitrox.Test.Model.Platforms;
using MethodImplAttributes = dnlib.DotNet.MethodImplAttributes;

namespace Nitrox.Launcher.Models.Utils;

[TestClass]
public class NitroxEntryPatchTest
{
    private string gameRoot = null!;
    private string managed = null!;
    private string gameCode = null!;
    private string backup = null!;
    private string patcher = null!;
    private byte[] vanilla = null!;

    [TestInitialize]
    public void Setup()
    {
        gameRoot = Path.Combine(Path.GetTempPath(), $"NitroxEntryPatchTest_{Guid.NewGuid():N}");
        managed = Path.Combine(gameRoot, GameInfo.Subnautica.DataFolder, "Managed");
        Directory.CreateDirectory(managed);
        gameCode = Path.Combine(managed, NitroxEntryPatch.GAME_ASSEMBLY_NAME);
        backup = Path.Combine(managed, NitroxEntryPatch.BACKUP_NAME);
        patcher = Path.Combine(managed, NitroxEntryPatch.NITROX_ASSEMBLY_NAME);

        vanilla = CreateGameCode(2);
        File.WriteAllBytes(gameCode, vanilla);
        File.WriteAllBytes(patcher, CreatePatcher());
    }

    [TestCleanup]
    public void Cleanup()
    {
        Directory.Delete(gameRoot, true);
    }

    [TestMethod]
    public void ApplyInsertsOnlyTheEntryCallAndKeepsTheVanillaFileAsBackup()
    {
        NitroxEntryPatch.Apply(gameRoot, EntryPatchFiles.Default);

        IList<Instruction> body = ReadAwake(File.ReadAllBytes(gameCode));
        body.Should().HaveCount(4);
        body[0].OpCode.Should().Be(OpCodes.Call);
        body[0].Operand.ToString().Should().Be("System.Void NitroxPatcher.Main::Execute()");
        body.Skip(1).Select(i => i.OpCode).Should().Equal(ReadAwake(vanilla).Select(i => i.OpCode));
        File.ReadAllBytes(backup).Should().Equal(vanilla);
        NitroxEntryPatch.IsPatchApplied(gameRoot).Should().BeTrue();
        LeftoverFiles().Should().BeEmpty();
    }

    [TestMethod]
    public void ApplyTwiceLeavesTheFileAsTheFirstApplyWroteIt()
    {
        NitroxEntryPatch.Apply(gameRoot, EntryPatchFiles.Default);
        byte[] first = File.ReadAllBytes(gameCode);

        NitroxEntryPatch.Apply(gameRoot, EntryPatchFiles.Default);

        File.ReadAllBytes(gameCode).Should().Equal(first);
        File.ReadAllBytes(backup).Should().Equal(vanilla);
    }

    [TestMethod]
    public void ApplyRefreshesTheBackupWhenTheGameReplacedItsOwnFile()
    {
        NitroxEntryPatch.Apply(gameRoot, EntryPatchFiles.Default);
        byte[] updatedVanilla = CreateGameCode(5);
        File.WriteAllBytes(gameCode, updatedVanilla);

        NitroxEntryPatch.Apply(gameRoot, EntryPatchFiles.Default);

        File.ReadAllBytes(backup).Should().Equal(updatedVanilla);
        NitroxEntryPatch.IsPatchApplied(gameRoot).Should().BeTrue();
    }

    [TestMethod]
    public void ApplyOnAFilePatchedWithoutBackupMakesABackupWithoutTheEntryCall()
    {
        NitroxEntryPatch.Apply(gameRoot, EntryPatchFiles.Default);
        File.Delete(backup);

        NitroxEntryPatch.Apply(gameRoot, EntryPatchFiles.Default);

        ReadAwake(File.ReadAllBytes(backup)).Select(i => i.OpCode).Should().Equal(ReadAwake(vanilla).Select(i => i.OpCode));
    }

    [TestMethod]
    public void ApplyLeavesTheGameFileUntouchedWhenTheWrittenFileDisappears()
    {
        FaultyFiles files = new() { DeleteAfterWriting = NitroxEntryPatch.GAME_ASSEMBLY_MODIFIED_NAME };

        Invoking(() => NitroxEntryPatch.Apply(gameRoot, files)).Should().Throw<EntryPatchException>().WithMessage("*antivirus*");

        File.ReadAllBytes(gameCode).Should().Equal(vanilla);
        LeftoverFiles().Should().BeEmpty();
    }

    [TestMethod]
    public void ApplyLeavesTheGameFileUntouchedWhenTheReplaceIsRefused()
    {
        FaultyFiles files = new() { RefuseReplace = true };

        Invoking(() => NitroxEntryPatch.Apply(gameRoot, files)).Should().Throw<EntryPatchException>().WithMessage("*could not update*");

        File.ReadAllBytes(gameCode).Should().Equal(vanilla);
        LeftoverFiles().Should().BeEmpty();
    }

    [TestMethod]
    public void ApplyPutsTheBackupBackWhenAFailedReplaceLostTheGameFile()
    {
        FaultyFiles files = new() { RefuseReplace = true, DeleteTargetOnRefusedReplace = true };

        Invoking(() => NitroxEntryPatch.Apply(gameRoot, files)).Should().Throw<EntryPatchException>();

        File.ReadAllBytes(gameCode).Should().Equal(vanilla);
    }

    [TestMethod]
    public void ApplyRecoversFromARunThatEndedBetweenSteps()
    {
        File.WriteAllBytes(Path.Combine(managed, NitroxEntryPatch.GAME_ASSEMBLY_MODIFIED_NAME), [1, 2, 3]);
        File.WriteAllBytes(Path.Combine(managed, $"{NitroxEntryPatch.BACKUP_NAME}.tmp"), [4, 5]);

        NitroxEntryPatch.Apply(gameRoot, EntryPatchFiles.Default);

        NitroxEntryPatch.IsPatchApplied(gameRoot).Should().BeTrue();
        File.ReadAllBytes(backup).Should().Equal(vanilla);
        LeftoverFiles().Should().BeEmpty();
    }

    [TestMethod]
    public void ApplyNamesAMissingGameCodeFileAndDoesNotCountItAsPatched()
    {
        File.Delete(gameCode);

        Invoking(() => NitroxEntryPatch.Apply(gameRoot, EntryPatchFiles.Default)).Should().Throw<EntryPatchException>().WithMessage("*is missing*Verify*");
        File.Exists(gameCode).Should().BeFalse();
    }

    [TestMethod]
    public void ApplyNamesAnEmptyGameCodeFile()
    {
        File.WriteAllBytes(gameCode, []);

        Invoking(() => NitroxEntryPatch.Apply(gameRoot, EntryPatchFiles.Default)).Should().Throw<EntryPatchException>().WithMessage("*is empty*");
    }

    [TestMethod]
    public void ApplyNamesAMissingPatcherFileWithoutTouchingTheGame()
    {
        File.Delete(patcher);

        Invoking(() => NitroxEntryPatch.Apply(gameRoot, EntryPatchFiles.Default)).Should().Throw<EntryPatchException>().WithMessage("*NitroxPatcher.dll*");
        File.ReadAllBytes(gameCode).Should().Equal(vanilla);
    }

    [TestMethod]
    public void RestorePutsTheVanillaFileBackAndRemovesTheNitroxFiles()
    {
        NitroxEntryPatch.Apply(gameRoot, EntryPatchFiles.Default);

        NitroxEntryPatch.RestoreGameFiles(gameRoot, EntryPatchFiles.Default);

        File.ReadAllBytes(gameCode).Should().Equal(vanilla);
        File.Exists(backup).Should().BeFalse();
        File.Exists(patcher).Should().BeFalse();
    }

    [TestMethod]
    public void RestoreWithoutBackupTakesTheEntryCallOut()
    {
        NitroxEntryPatch.Apply(gameRoot, EntryPatchFiles.Default);
        File.Delete(backup);

        NitroxEntryPatch.RestoreGameFiles(gameRoot, EntryPatchFiles.Default);

        NitroxEntryPatch.IsPatchApplied(gameRoot).Should().BeFalse();
        File.Exists(patcher).Should().BeFalse();
    }

    [TestMethod]
    public void RestoreKeepsThePatchedFileWhenTheReplaceIsRefused()
    {
        NitroxEntryPatch.Apply(gameRoot, EntryPatchFiles.Default);
        byte[] patched = File.ReadAllBytes(gameCode);

        Invoking(() => NitroxEntryPatch.RestoreGameFiles(gameRoot, new FaultyFiles { RefuseReplace = true })).Should().Throw<EntryPatchException>();

        File.ReadAllBytes(gameCode).Should().Equal(patched);
        File.ReadAllBytes(backup).Should().Equal(vanilla);
    }

    [TestMethod]
    public void InstallPatcherCopiesTheLaunchersPatcherIntoTheGame()
    {
        string source = Path.Combine(gameRoot, "NewPatcher.dll");
        File.WriteAllBytes(source, [9, 9, 9]);

        NitroxEntryPatch.InstallPatcher(source, gameRoot);

        File.ReadAllBytes(patcher).Should().Equal(9, 9, 9);
    }

    // File locks only bind on Windows.
    [OSTestMethod(OperatingSystems.Windows)]
    public void InstallPatcherStopsWhenALockedOlderPatcherStaysInTheGame()
    {
        string source = Path.Combine(gameRoot, "NewPatcher.dll");
        File.WriteAllBytes(source, [9, 9, 9]);

        using (new FileStream(patcher, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            Invoking(() => NitroxEntryPatch.InstallPatcher(source, gameRoot)).Should().Throw<EntryPatchException>().WithMessage("*could not be updated*");
        }
    }

    [OSTestMethod(OperatingSystems.Windows)]
    public void InstallPatcherAcceptsALockedPatcherThatIsAlreadyThisVersion()
    {
        string source = Path.Combine(gameRoot, "SamePatcher.dll");
        File.Copy(patcher, source);

        using (new FileStream(patcher, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            Invoking(() => NitroxEntryPatch.InstallPatcher(source, gameRoot)).Should().NotThrow();
        }
    }

    private IEnumerable<string> LeftoverFiles() =>
        Directory.EnumerateFiles(managed).Select(Path.GetFileName).Where(name => name is NitroxEntryPatch.GAME_ASSEMBLY_MODIFIED_NAME || name!.EndsWith(".tmp"));

    private static IList<Instruction> ReadAwake(byte[] code)
    {
        ModuleDefMD module = ModuleDefMD.Load(code);
        return module.GetTypes().First(t => t.FullName == "PlatformUtils").Methods.First(m => m.Name == "Awake").Body.Instructions;
    }

    private static byte[] CreateGameCode(int nopCount)
    {
        ModuleDefUser module = new("Assembly-CSharp.dll") { Kind = ModuleKind.Dll };
        AssemblyDefUser assembly = new("Assembly-CSharp", new Version(0, 0, 0, 0));
        assembly.Modules.Add(module);
        TypeDefUser type = new("", "PlatformUtils", module.CorLibTypes.Object.TypeDefOrRef) { Attributes = dnlib.DotNet.TypeAttributes.Public | dnlib.DotNet.TypeAttributes.Class };
        module.Types.Add(type);
        MethodDefUser awake = new("Awake", MethodSig.CreateInstance(module.CorLibTypes.Void), MethodImplAttributes.IL | MethodImplAttributes.Managed, dnlib.DotNet.MethodAttributes.Private | dnlib.DotNet.MethodAttributes.HideBySig)
        {
            Body = new CilBody()
        };
        for (int i = 0; i < nopCount; i++)
        {
            awake.Body.Instructions.Add(OpCodes.Nop.ToInstruction());
        }
        awake.Body.Instructions.Add(OpCodes.Ret.ToInstruction());
        type.Methods.Add(awake);
        using MemoryStream stream = new();
        module.Write(stream);
        return stream.ToArray();
    }

    private static byte[] CreatePatcher()
    {
        ModuleDefUser module = new("NitroxPatcher.dll") { Kind = ModuleKind.Dll };
        AssemblyDefUser assembly = new("NitroxPatcher", new Version(1, 0, 0, 0));
        assembly.Modules.Add(module);
        TypeDefUser type = new("NitroxPatcher", "Main", module.CorLibTypes.Object.TypeDefOrRef) { Attributes = dnlib.DotNet.TypeAttributes.Public | dnlib.DotNet.TypeAttributes.Abstract | dnlib.DotNet.TypeAttributes.Sealed };
        module.Types.Add(type);
        MethodDefUser execute = new("Execute", MethodSig.CreateStatic(module.CorLibTypes.Void), MethodImplAttributes.IL | MethodImplAttributes.Managed, dnlib.DotNet.MethodAttributes.Public | dnlib.DotNet.MethodAttributes.Static)
        {
            Body = new CilBody()
        };
        execute.Body.Instructions.Add(OpCodes.Ret.ToInstruction());
        type.Methods.Add(execute);
        using MemoryStream stream = new();
        module.Write(stream);
        return stream.ToArray();
    }

    private sealed class FaultyFiles : EntryPatchFiles
    {
        public string? DeleteAfterWriting { get; init; }
        public bool RefuseReplace { get; init; }
        public bool DeleteTargetOnRefusedReplace { get; init; }

        public override void WriteAllBytes(string path, byte[] content)
        {
            base.WriteAllBytes(path, content);
            if (Path.GetFileName(path) == DeleteAfterWriting)
            {
                File.Delete(path);
            }
        }

        public override void Replace(string source, string target)
        {
            if (!RefuseReplace)
            {
                base.Replace(source, target);
                return;
            }
            if (DeleteTargetOnRefusedReplace)
            {
                File.Delete(target);
            }
            throw new IOException("The process cannot access the file because it is being used by another process.");
        }
    }
}
