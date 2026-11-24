using System.IO;
using Microsoft.Extensions.Logging.Abstractions;
using Nitrox.Server.Subnautica.Models.GameLogic;

namespace Nitrox.Test.Server.GameLogic;

/// <summary>
///     The world's identity is made once, kept in the save folder and comes back with it, also from a backup.
/// </summary>
[TestClass]
public sealed class WorldIdentityTest
{
    private string saveDirectory = null!;

    [TestInitialize]
    public void CreateSaveDirectory()
    {
        saveDirectory = Path.Combine(Path.GetTempPath(), $"nitrox-world-identity-{Guid.NewGuid():N}");
    }

    [TestCleanup]
    public void DeleteSaveDirectory()
    {
        if (Directory.Exists(saveDirectory))
        {
            Directory.Delete(saveDirectory, true);
        }
    }

    [TestMethod]
    public void ANewWorldGetsAnIdentityItKeeps()
    {
        WorldIdentity first = new(() => saveDirectory, NullLogger<WorldIdentity>.Instance);

        string identity = first.Value;

        identity.Should().NotBeNullOrWhiteSpace();
        first.CreatedThisRun.Should().BeTrue();
        File.ReadAllText(Path.Combine(saveDirectory, WorldIdentity.FILE_NAME)).Should().Be(identity);

        WorldIdentity afterRestart = new(() => saveDirectory, NullLogger<WorldIdentity>.Instance);
        afterRestart.Value.Should().Be(identity);
        afterRestart.CreatedThisRun.Should().BeFalse();
    }

    [TestMethod]
    public void AWorldRestoredFromABackupKeepsItsIdentity()
    {
        string identity = new WorldIdentity(() => saveDirectory, NullLogger<WorldIdentity>.Instance).Value;
        // A backup copies every file of the save folder, and restoring copies them back
        string restored = Path.Combine(saveDirectory, "restored");
        Directory.CreateDirectory(restored);
        File.Copy(Path.Combine(saveDirectory, WorldIdentity.FILE_NAME), Path.Combine(restored, WorldIdentity.FILE_NAME));

        new WorldIdentity(() => restored, NullLogger<WorldIdentity>.Instance).Value.Should().Be(identity);
    }

    [TestMethod]
    public void WorldsGetDifferentIdentities()
    {
        string other = Path.Combine(saveDirectory, "other");

        new WorldIdentity(() => saveDirectory, NullLogger<WorldIdentity>.Instance).Value
            .Should().NotBe(new WorldIdentity(() => other, NullLogger<WorldIdentity>.Instance).Value);
    }
}
