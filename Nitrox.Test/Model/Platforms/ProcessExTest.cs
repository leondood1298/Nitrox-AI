using System.Runtime.Versioning;
using System.Security.Principal;
using Nitrox.Model.Platforms.OS.Shared;

namespace Nitrox.Test.Model.Platforms;

[TestClass]
public sealed class ProcessExTest
{
    [TestMethod]
    [SupportedOSPlatform("windows")]
    public void ElevationMatchesTheTokensAdministratorRole()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("Windows only");
        }
        using WindowsIdentity identity = WindowsIdentity.GetCurrent();
        bool elevated = new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);

        WindowsProcessEx.IsElevated().Should().Be(elevated, "an administrator account that is not elevated is not elevated");
    }
}
