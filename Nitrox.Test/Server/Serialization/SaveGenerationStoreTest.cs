using System.IO.Compression;
using Nitrox.Server.Subnautica.Models.Serialization.World;

namespace Nitrox.Test.Server.Serialization;

[TestClass]
public sealed class SaveGenerationStoreTest
{
    private static readonly string[] fileNames = ["Version.json", "PlayerData.json", "WorldData.json", "GlobalRootData.json", "EntityData.json"];
    private string saveDir = null!;

    [TestInitialize]
    public void Setup()
    {
        saveDir = Path.Combine(Path.GetTempPath(), $"nitrox-save-test-{Guid.NewGuid():N}", "My World");
        Directory.CreateDirectory(saveDir);
    }

    [TestCleanup]
    public void Cleanup()
    {
        Directory.Delete(Path.GetDirectoryName(saveDir)!, true);
    }

    [TestMethod]
    public void SavedGenerationIsFoundWhole()
    {
        new SaveGenerationStore().Write(saveDir, Files("one"));

        IReadOnlyList<ResolvedGeneration> generations = new SaveGenerationStore().FindGenerations(saveDir);

        generations.Should().ContainSingle();
        generations[0].Source.Should().Be(GenerationSource.CURRENT);
        AssertWholeGeneration(generations[0], "one");
        Directory.Exists(Path.Combine(saveDir, SaveGenerationStore.STAGING_FOLDER_NAME)).Should().BeFalse();
    }

    [TestMethod]
    public void NextSaveKeepsThePreviousGeneration()
    {
        SaveGenerationStore store = new();
        store.Write(saveDir, Files("one"));
        store.Write(saveDir, Files("two"));

        IReadOnlyList<ResolvedGeneration> generations = store.FindGenerations(saveDir);

        generations.Select(generation => generation.Source).Should().Equal(GenerationSource.CURRENT, GenerationSource.PREVIOUS);
        AssertWholeGeneration(generations[0], "two");
        AssertWholeGeneration(generations[1], "one");
        generations[0].Manifest.Generation.Should().BeGreaterThan(generations[1].Manifest.Generation);
    }

    [TestMethod]
    public void SaveOfTheSameWorldIsReportedUnchanged()
    {
        SaveGenerationStore store = new();
        store.Write(saveDir, Files("one"), out bool firstChanged);
        store.Write(saveDir, Files("one"), out bool sameChanged);
        store.Write(saveDir, Files("two"), out bool otherChanged);

        firstChanged.Should().BeTrue();
        sameChanged.Should().BeFalse();
        otherChanged.Should().BeTrue();
    }

    [TestMethod]
    public void SaveCutShortAtAnyStepLeavesAWholeGeneration()
    {
        List<string> steps = [];
        string probeDir = Path.Combine(Path.GetDirectoryName(saveDir)!, "probe");
        new SaveGenerationStore().Write(probeDir, Files("one"));
        new SaveGenerationStore(steps.Add).Write(probeDir, Files("two"));
        steps.Should().Contain(["staged EntityData.json", "moved current manifest", "kept previous", "committed EntityData.json", "committed manifest"]);

        foreach (string step in steps)
        {
            string dir = Path.Combine(Path.GetDirectoryName(saveDir)!, $"cut at {step}");
            new SaveGenerationStore().Write(dir, Files("one"));

            Action save = () => new SaveGenerationStore(reached => ThrowAt(step, reached)).Write(dir, Files("two"));

            save.Should().Throw<IOException>($"the save is cut short at \"{step}\"");
            IReadOnlyList<ResolvedGeneration> generations = new SaveGenerationStore().FindGenerations(dir);
            generations.Should().NotBeEmpty($"a whole generation must remain after a save cut short at \"{step}\"");
            AssertWholeGeneration(generations[0], ContentsOf(generations[0]));

            // The next save must work on whatever was left behind
            new SaveGenerationStore().Write(dir, Files("three"));
            IReadOnlyList<ResolvedGeneration> afterNextSave = new SaveGenerationStore().FindGenerations(dir);
            afterNextSave[0].Source.Should().Be(GenerationSource.CURRENT);
            AssertWholeGeneration(afterNextSave[0], "three");
            afterNextSave.Should().HaveCount(2, $"the generation loaded after \"{step}\" is kept as the previous one");
        }
    }

    [TestMethod]
    public void FirstSaveCutShortLeavesNoGeneration()
    {
        Action save = () => new SaveGenerationStore(reached => ThrowAt("staged WorldData.json", reached)).Write(saveDir, Files("one"));

        save.Should().Throw<IOException>();
        new SaveGenerationStore().FindGenerations(saveDir).Should().BeEmpty();
        File.Exists(Path.Combine(saveDir, "PlayerData.json")).Should().BeFalse("nothing is put in place before every file is written");
    }

    [TestMethod]
    public void WriterThatThrowsLeavesTheCurrentGenerationAlone()
    {
        SaveGenerationStore store = new();
        store.Write(saveDir, Files("one"));
        List<GenerationFile> files = Files("two");
        files[^1] = new GenerationFile("EntityData.json", _ => throw new InvalidOperationException("Collection was modified"));

        Action save = () => store.Write(saveDir, files);

        save.Should().Throw<InvalidOperationException>();
        IReadOnlyList<ResolvedGeneration> generations = store.FindGenerations(saveDir);
        generations[0].Source.Should().Be(GenerationSource.CURRENT);
        AssertWholeGeneration(generations[0], "one");
    }

    [TestMethod]
    public void DamagedCurrentFileFallsBackToThePreviousGeneration()
    {
        SaveGenerationStore store = new();
        store.Write(saveDir, Files("one"));
        store.Write(saveDir, Files("two"));
        File.WriteAllText(Path.Combine(saveDir, "EntityData.json"), "two-EntityData.json but cut");

        IReadOnlyList<ResolvedGeneration> generations = store.FindGenerations(saveDir);

        generations.Should().ContainSingle();
        generations[0].Source.Should().Be(GenerationSource.PREVIOUS);
        AssertWholeGeneration(generations[0], "one");
    }

    [TestMethod]
    public void NewestWholeBackupIsUnpacked()
    {
        string backupsDir = Path.Combine(saveDir, "backups");
        Directory.CreateDirectory(backupsDir);
        string sourceDir = Path.Combine(Path.GetDirectoryName(saveDir)!, "source");
        new SaveGenerationStore().Write(sourceDir, Files("backed up"));
        string backup = Path.Combine(backupsDir, "Backup - 2026-01-01 10.00.00.zip");
        using (ZipArchive zip = ZipFile.Open(backup, ZipArchiveMode.Create))
        {
            foreach (string file in fileNames.Append(SaveGenerationStore.MANIFEST_FILE_NAME))
            {
                zip.CreateEntryFromFile(Path.Combine(sourceDir, file), file);
            }
        }
        string broken = Path.Combine(backupsDir, "Backup - 2026-01-01 11.00.00.zip");
        File.WriteAllText(broken, "not a zip");
        File.SetLastWriteTimeUtc(broken, DateTime.UtcNow.AddMinutes(1));

        ResolvedGeneration? restored = new SaveGenerationStore().ExtractNewestBackup(saveDir, backupsDir, new HashSet<string>());

        restored.Should().NotBeNull();
        restored!.Source.Should().Be(GenerationSource.BACKUP);
        AssertWholeGeneration(restored, "backed up");
        SaveGenerationStore.RemoveRestoreFolder(saveDir);
        Directory.Exists(Path.Combine(saveDir, SaveGenerationStore.RESTORE_FOLDER_NAME)).Should().BeFalse();
    }

    [TestMethod]
    public void RemovingWorkingFoldersLeavesOnlyTheWorldFolderFiles()
    {
        SaveGenerationStore store = new();
        store.Write(saveDir, Files("one"));
        store.Write(saveDir, Files("two"));

        SaveGenerationStore.RemoveWorkingFolders(saveDir);

        store.FindGenerations(saveDir).Should().ContainSingle().Which.Source.Should().Be(GenerationSource.CURRENT);
    }

    private static void ThrowAt(string step, string reached)
    {
        if (reached == step)
        {
            throw new IOException($"Save cut short at {step}");
        }
    }

    private static List<GenerationFile> Files(string content) =>
        [..fileNames.Select(name => new GenerationFile(name, path => File.WriteAllText(path, $"{content}-{name}")))];

    private static string ContentsOf(ResolvedGeneration generation)
    {
        string version = File.ReadAllText(generation.PathOf("Version.json"));
        return version[..^"-Version.json".Length];
    }

    private static void AssertWholeGeneration(ResolvedGeneration generation, string content)
    {
        foreach (string name in fileNames)
        {
            File.ReadAllText(generation.PathOf(name)).Should().Be($"{content}-{name}", $"{name} must belong to the same save as the other files");
        }
    }
}
