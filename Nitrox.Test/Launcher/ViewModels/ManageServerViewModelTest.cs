using System.ComponentModel.DataAnnotations;
using Nitrox.Launcher.ViewModels;
using Nitrox.Model.Configuration;

namespace Nitrox.Test.Launcher.ViewModels;

[TestClass]
public sealed class ManageServerViewModelTest
{
    [TestMethod]
    public void MaxPlayersCannotExceedWhatTheServerCanStore()
    {
        RangeAttribute launcher = typeof(ManageServerViewModel).GetProperty(nameof(ManageServerViewModel.ServerMaxPlayers))!.GetCustomAttribute<RangeAttribute>()!;
        RangeAttribute server = typeof(SubnauticaServerOptions).GetProperty(nameof(SubnauticaServerOptions.MaxConnections))!.GetCustomAttribute<RangeAttribute>()!;

        launcher.Minimum.Should().Be(server.Minimum);
        launcher.Maximum.Should().Be(server.Maximum);
    }

    [TestMethod]
    public void ThePortRangeMatchesTheServers()
    {
        RangeAttribute launcher = typeof(ManageServerViewModel).GetProperty(nameof(ManageServerViewModel.ServerPort))!.GetCustomAttribute<RangeAttribute>()!;
        RangeAttribute server = typeof(SubnauticaServerOptions).GetProperty(nameof(SubnauticaServerOptions.ServerPort))!.GetCustomAttribute<RangeAttribute>()!;

        launcher.Minimum.Should().Be(server.Minimum);
        launcher.Maximum.Should().Be(server.Maximum);
    }
}
