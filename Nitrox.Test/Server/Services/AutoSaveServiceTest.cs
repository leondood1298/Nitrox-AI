using Nitrox.Model.Configuration;
using Nitrox.Server.Subnautica.Services;

namespace Nitrox.Test.Server.Services;

/// <summary>
///     A server started with saving off ended its save loop for good, so "autosave on" changed nothing until a restart.
/// </summary>
[TestClass]
public sealed class AutoSaveServiceTest
{
    [TestMethod]
    public void WhileSavingIsOffTheSettingIsStillLookedAtSoon()
    {
        SubnauticaServerOptions options = new() { AutoSave = false };

        AutoSaveService.GetTickPeriod(options).Should().Be(AutoSaveService.DISABLED_CHECK_PERIOD);
    }

    [TestMethod]
    public void WhileSavingIsOnTicksFollowTheInterval()
    {
        SubnauticaServerOptions options = new() { AutoSave = true, SaveInterval = 60000 };

        AutoSaveService.GetTickPeriod(options).Should().Be(TimeSpan.FromMinutes(1));
    }
}
