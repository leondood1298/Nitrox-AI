using Nitrox.Model.Configuration;
using Nitrox.Server.Subnautica.Extensions;

namespace Nitrox.Test.Server.Extensions;

[TestClass]
public sealed class ServerStartOptionsExtensionsTest
{
    [TestMethod]
    public void WorldLiesInTheWorldsFolderTheLauncherGave()
    {
        string savesPath = Path.Combine(Path.GetTempPath(), "nitrox-worlds");
        ServerStartOptions options = new() { SaveName = "My World", SavesPath = savesPath };

        options.GetServerSavePath().Should().Be(Path.Combine(savesPath, "My World"));
    }
}
