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
///     A fire that is sprayed keeps the strength it has left; one that is put out is removed.
/// </summary>
[TestClass]
public sealed class FireDousedProcessorTest
{
    private readonly ConnectedPlayers players = new();
    private readonly IPacketSender packetSender = Substitute.For<IPacketSender>();
    private EntityRegistry entityRegistry;
    private FireDousedProcessor processor;
    private WorldEntity fire;

    [TestInitialize]
    public void Init()
    {
        entityRegistry = new EntityRegistry(NullLogger<EntityRegistry>.Instance);
        SimulationOwnershipData ownership = new();
        // Batch spawning and progress reporting play no part here
        WorldEntityManager worldEntityManager = new(packetSender, entityRegistry, null!, players.PlayerManager, null!, NullLogger<WorldEntityManager>.Instance);
        ScannerRoomManager scannerRoomManager = ScannerRoomScenarioFixture.CreateManager(entityRegistry, ownership, new ScannerRoomDiagnostics(NullLogger<ScannerRoomDiagnostics>.Instance),
            packetSender, players.PlayerManager, worldEntityManager);
        EntitySimulation entitySimulation = new(packetSender, entityRegistry, worldEntityManager, ownership, players.PlayerManager, scannerRoomManager, NullLogger<EntitySimulation>.Instance);
        processor = new FireDousedProcessor(entitySimulation, worldEntityManager, entityRegistry);

        NitroxId fireId = new();
        fire = new WorldEntity(NitroxVector3.Zero, NitroxQuaternion.Identity, NitroxVector3.One, NitroxTechType.None, 0, "aurora-fire", true, fireId, null);
        entityRegistry.AddEntity(fire);
    }

    [TestMethod]
    public async Task SprayedFireKeepsTheStrengthItHasLeft()
    {
        Player sprayer = players.Connect("Sprayer");
        FireDoused packet = new(fire.Id, 42f, sprayer.SessionId, 0f, false);

        await processor.Process(new AuthProcessorContext(sprayer, packetSender), packet);

        fire.Metadata.Should().BeOfType<FireMetadata>().Which.Health.Should().Be(42f);
        await packetSender.Received(1).SendPacketToOthersAsync(packet, sprayer.SessionId);
    }

    [TestMethod]
    public async Task LaterSprayReplacesTheStoredStrength()
    {
        Player sprayer = players.Connect("Sprayer");
        await processor.Process(new AuthProcessorContext(sprayer, packetSender), new FireDoused(fire.Id, 80f, sprayer.SessionId, 10f, false));

        await processor.Process(new AuthProcessorContext(sprayer, packetSender), new FireDoused(fire.Id, 30f, sprayer.SessionId, 0f, false));

        fire.Metadata.Should().BeOfType<FireMetadata>().Which.Health.Should().Be(30f);
    }

    [TestMethod]
    public async Task ExtinguishedFireIsRemoved()
    {
        Player sprayer = players.Connect("Sprayer");
        await processor.Process(new AuthProcessorContext(sprayer, packetSender), new FireDoused(fire.Id, 50f, sprayer.SessionId, 10f, false));

        await processor.Process(new AuthProcessorContext(sprayer, packetSender), new FireDoused(fire.Id, 0f, null, 0f, true));

        entityRegistry.TryGetEntityById(fire.Id, out Entity _).Should().BeFalse();
    }
}
