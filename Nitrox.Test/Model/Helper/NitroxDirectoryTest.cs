using Nitrox.Test;

namespace Nitrox.Model.Helper;

[TestClass]
public class NitroxDirectoryTest
{
    [TestMethod]
    public void TestsUseTheirOwnDataFolderInsteadOfThePlayersNitroxFolder()
    {
        NitroxDirectory.UserDataPath.Should().Be(SetupAssemblyInitializer.TestDataPath);
        NitroxDirectory.ConfigPath.Should().StartWith(SetupAssemblyInitializer.TestDataPath);
        Nitrox.Model.Logger.Log.LogDirectory.Should().StartWith(SetupAssemblyInitializer.TestDataPath);
        NitroxDirectory.CachePath.Should().NotStartWith(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData));
    }
}
