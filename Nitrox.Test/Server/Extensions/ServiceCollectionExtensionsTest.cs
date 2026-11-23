using Microsoft.Extensions.DependencyInjection;
using Nitrox.Model.DataStructures.GameLogic;
using Nitrox.Server.Subnautica.Extensions;
using Nitrox.Server.Subnautica.Models.Commands;
using Nitrox.Server.Subnautica.Models.Commands.Core;
using Nitrox.Server.Subnautica.Models.GameLogic;
using Nitrox.Server.Subnautica.Models.GameLogic.Entities;
using Nitrox.Test.Server.GameLogic;

namespace Nitrox.Test.Server.Extensions;

[TestClass]
public sealed class ServiceCollectionExtensionsTest
{
    [TestMethod]
    public void ServiceContainerCanBuildTheCommandsAndGatesThemForAdmins()
    {
        ServiceCollection services = new();
        services.AddLogging();
        services.AddWorld();
        services.AddCommands();
        // The commands read Scanner Rooms through their manager. What the manager needs beyond the world state
        // (network delivery, the player list, loading the world) is not part of this, so it is put together here.
        services.AddSingleton(provider => ScannerRoomScenarioFixture.CreateManager(provider.GetRequiredService<EntityRegistry>(),
            provider.GetRequiredService<SimulationOwnershipData>(), provider.GetRequiredService<ScannerRoomDiagnostics>()));
        using ServiceProvider provider = services.BuildServiceProvider();

        // The same entries the command registry is built from, without resolving every other command of the server.
        CommandHandlerEntry[] entries =
        [
            new(provider.GetRequiredService<ScannerRoomCommand>(), typeof(ICommandHandler)),
            new(provider.GetRequiredService<ScannerMarkCommand>(), typeof(ICommandHandler<string>)),
            new(provider.GetRequiredService<BasePowerCommand>(), typeof(ICommandHandler)),
            new(provider.GetRequiredService<BasePowerCommand>(), typeof(ICommandHandler<bool>))
        ];

        entries.Select(entry => entry.Name).Should().Equal("scannerroom", "scannermark", "basepower", "basepower");
        entries.Should().OnlyContain(entry => entry.MinimumPermissions == Perms.ADMIN, "these commands print and mark server state");
        entries.Should().OnlyContain(entry => entry.Description.Length > 0, "the help command lists them by this text");
    }
}
