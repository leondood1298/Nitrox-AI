using Microsoft.Extensions.Logging.Abstractions;
using Nitrox.Model.Serialization;
using Nitrox.Server.Subnautica.Models.Serialization;
using Nitrox.Server.Subnautica.Models.Serialization.World;

namespace Nitrox.Test.Server.Serialization;

[TestClass]
public sealed class WorldServiceTest
{
    private string folder = null!;

    [TestInitialize]
    public void Setup()
    {
        folder = Path.Combine(Path.GetTempPath(), $"nitrox-version-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(folder);
    }

    [TestCleanup]
    public void Cleanup() => Directory.Delete(folder, true);

    [TestMethod]
    public void VersionFileOfThisBuildHasTheCurrentSaveFormat()
    {
        string path = Path.Combine(folder, "Version.json");
        new ServerJsonSerializer(NullLogger<ServerJsonSerializer>.Instance).Serialize(path, new SaveFileVersion());

        WorldService.TryReadSaveFormat(path).Should().Be(NitroxSaveFormat.CURRENT);
    }

    [TestMethod]
    public void VersionFileWithoutSaveFormatIsFromAnOlderBuild()
    {
        string path = Path.Combine(folder, "Version.json");
        File.WriteAllText(path, """{"Major":1,"Minor":9,"Build":0,"Revision":0}""");

        WorldService.TryReadSaveFormat(path).Should().Be(NitroxSaveFormat.UNKNOWN);
    }

    [TestMethod]
    public void UnreadableVersionFileHasNoFormat()
    {
        string path = Path.Combine(folder, "Version.json");
        File.WriteAllText(path, "{ cut");

        WorldService.TryReadSaveFormat(path).Should().BeNull();
    }
}
