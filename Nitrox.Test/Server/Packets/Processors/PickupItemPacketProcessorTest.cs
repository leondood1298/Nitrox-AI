using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Nitrox.Model.DataStructures;
using Nitrox.Model.DataStructures.Unity;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;
using Nitrox.Model.Subnautica.Packets;
using Nitrox.Server.Subnautica.Models.GameLogic;
using Nitrox.Server.Subnautica.Models.GameLogic.Entities;
using Nitrox.Server.Subnautica.Models.Packets.Core;
using Nitrox.Test.Server.GameLogic;
using Nitrox.Test.Server.Helper;
using NSubstitute;

namespace Nitrox.Server.Subnautica.Models.Packets.Processors;

/// <summary>
///     A supply crate is a placeholder group whose item is a child entity of the crate. The item has to leave the crate
///     on the server when it is taken, or the next player to load the crate, and everyone after a restart, would find
///     it there again.
/// </summary>
[TestClass]
public sealed class PickupItemPacketProcessorTest
{
    private const string BATTERY_CRATE_CLASS_ID = "580154dd-b2a3-4da1-be14-9a22e20385c8";
    private const string BATTERY_CLASS_ID = "d4bfebc0-a5e6-47d3-b4a7-d5e47f614ed6";

    [TestMethod]
    public async Task AnItemTakenFromACrateIsNoLongerInTheCrate()
    {
        ConnectedPlayers players = new();
        Player taker = players.Connect("Taker");
        players.Connect("Other");
        EntityRegistry entityRegistry = new(NullLogger<EntityRegistry>.Instance);
        IPacketSender packetSender = Substitute.For<IPacketSender>();
        SimulationOwnershipData ownership = new();
        WorldEntityManager worldEntityManager = new(packetSender, entityRegistry, null!, players.PlayerManager, null!, NullLogger<WorldEntityManager>.Instance);
        ScannerRoomManager scannerRoomManager = ScannerRoomScenarioFixture.CreateManager(entityRegistry, ownership, new ScannerRoomDiagnostics(NullLogger<ScannerRoomDiagnostics>.Instance),
            packetSender, players.PlayerManager, worldEntityManager);
        PickupItemPacketProcessor processor = new(entityRegistry, worldEntityManager, ownership, scannerRoomManager);

        PlaceholderGroupWorldEntity crate = new(new NitroxTransform(new NitroxVector3(10, -20, 30), NitroxQuaternion.Identity, NitroxVector3.One), 0, BATTERY_CRATE_CLASS_ID, true,
            new NitroxId(), new NitroxTechType("None"), new SupplyCrateMetadata(true, false, 0), null, [], 0);
        PrefabPlaceholderEntity item = new(new NitroxTransform(new NitroxVector3(0, 0.3f, 0), NitroxQuaternion.Identity, NitroxVector3.One), 0, BATTERY_CLASS_ID, true,
            new NitroxId(), new NitroxTechType("Battery"), null, crate.Id, [], 0);
        crate.ChildEntities.Add(item);
        entityRegistry.AddOrUpdate(crate);
        entityRegistry.AddOrUpdate(item);

        InventoryItemEntity taken = new(item.Id, BATTERY_CLASS_ID, new NitroxTechType("Battery"), null, taker.GameObjectId, []);
        await processor.Process(new AuthProcessorContext(taker, packetSender), new PickupItem(taken));

        crate.ChildEntities.Should().BeEmpty();
        crate.Metadata.Should().BeOfType<SupplyCrateMetadata>().Which.Open.Should().BeTrue();
        entityRegistry.GetEntityById(item.Id).Value.Should().BeSameAs(taken);
    }
}
