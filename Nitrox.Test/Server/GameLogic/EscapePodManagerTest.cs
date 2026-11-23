using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Nitrox.Model.Configuration;
using Nitrox.Model.Core;
using Nitrox.Model.DataStructures;
using Nitrox.Model.DataStructures.Unity;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;
using Nitrox.Server.Subnautica.Models.Factories;
using Nitrox.Server.Subnautica.Models.GameLogic.Entities;
using Nitrox.Server.Subnautica.Models.Packets.Core;
using NSubstitute;

namespace Nitrox.Server.Subnautica.Models.GameLogic;

/// <summary>
///     Which Lifepod a player starts in. A pod handed back as newly created is sent as a whole to every player in the
///     game, so only a pod that was really made for the player may come back that way.
/// </summary>
[TestClass]
public sealed class EscapePodManagerTest
{
    private EscapePodManager manager;
    private EscapePodEntity knownPod;

    [TestInitialize]
    public async Task Init()
    {
        IOptions<SubnauticaServerOptions> options = Options.Create(new SubnauticaServerOptions { Seed = "TESTSEED" });
        // The start positions come from the game's files, which a pod with a free place does not need.
        manager = new EscapePodManager(new RandomFactory(options), new EntityRegistry(NullLogger<EntityRegistry>.Instance), null, options, Substitute.For<IPacketSender>());
        knownPod = new EscapePodEntity(NitroxVector3.Zero, new NitroxId(), new EscapePodMetadata(true, true));
        knownPod.Players.Add(new PeerId(1));
        await manager.AddKnownPodsAsync([knownPod]);
    }

    [TestMethod]
    public async Task ANewPlayerPutInAPodThatWasThereGetsNoNewlyCreatedPod()
    {
        (NitroxId podId, EscapePodEntity newlyCreatedPod) = await manager.AssignPlayerToEscapePodAsync(new PeerId(2));

        podId.Should().Be(knownPod.Id);
        newlyCreatedPod.Should().BeNull();
        knownPod.Players.Should().Contain(new PeerId(2));
    }

    [TestMethod]
    public async Task APlayerWhoHasAPodGetsTheSamePodAndNoNewlyCreatedOne()
    {
        (NitroxId podId, EscapePodEntity newlyCreatedPod) = await manager.AssignPlayerToEscapePodAsync(new PeerId(1));

        podId.Should().Be(knownPod.Id);
        newlyCreatedPod.Should().BeNull();
        knownPod.Players.Should().HaveCount(1);
    }
}
