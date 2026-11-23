using Nitrox.Server.Subnautica.Models.Serialization.World;

namespace Nitrox.Test.Server.Serialization;

[TestClass]
public sealed class WorldFolderLockTest
{
    private string saveDir = null!;

    [TestInitialize]
    public void Setup() => saveDir = Path.Combine(Path.GetTempPath(), $"nitrox-lock-test-{Guid.NewGuid():N}");

    [TestCleanup]
    public void Cleanup() => Directory.Delete(saveDir, true);

    [TestMethod]
    public void SecondServerCannotTakeAWorldInUseAndLearnsWhoHasIt()
    {
        using WorldFolderLock? first = WorldFolderLock.TryAcquire(saveDir, out _);

        WorldFolderLock? second = WorldFolderLock.TryAcquire(saveDir, out int? holder);

        first.Should().NotBeNull();
        second.Should().BeNull();
        holder.Should().Be(Environment.ProcessId);
    }

    [TestMethod]
    public void ReleasedWorldCanBeTakenAgainAndLeavesNoFile()
    {
        WorldFolderLock.TryAcquire(saveDir, out _)!.Dispose();

        File.Exists(Path.Combine(saveDir, WorldFolderLock.LOCK_FILE_NAME)).Should().BeFalse();
        using WorldFolderLock? again = WorldFolderLock.TryAcquire(saveDir, out _);
        again.Should().NotBeNull();
    }
}
