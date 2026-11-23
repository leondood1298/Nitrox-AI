using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Nitrox.Model.DataStructures;
using Nitrox.Model.DataStructures.Unity;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;
using Nitrox.Model.Subnautica.Packets;
using Nitrox.Server.Subnautica.Models.GameLogic.Entities;
using Nitrox.Server.Subnautica.Models.Packets.Core;
using Nitrox.Test.Server.Helper;
using NSubstitute;

namespace Nitrox.Server.Subnautica.Models.Packets.Processors;

/// <summary>
///     A player whose game showed another rocket's time capsule gets the opened rocket's capsule as it is now.
/// </summary>
[TestClass]
public sealed class PlayerTimeCapsuleContentRequestProcessorTest
{
    private readonly ConnectedPlayers players = new();
    private readonly IPacketSender packetSender = Substitute.For<IPacketSender>();
    private EntityRegistry entityRegistry;
    private PlayerTimeCapsuleContentRequestProcessor processor;

    [TestInitialize]
    public void Init()
    {
        entityRegistry = new EntityRegistry(NullLogger<EntityRegistry>.Instance);
        processor = new PlayerTimeCapsuleContentRequestProcessor(entityRegistry, NullLogger<PlayerTimeCapsuleContentRequestProcessor>.Instance);
    }

    [TestMethod]
    public async Task OpenerGetsTheCapsulesTextAndItems()
    {
        Player opener = players.Connect("Opener");
        PlayerTimeCapsuleEntity capsule = AddRocketCapsule(new PlayerTimeCapsuleMetadata("Hello from rocket two", ""));
        InventoryItemEntity item = new(new NitroxId(), "titanium-class-id", new NitroxTechType("Titanium"), null, capsule.Id, []);
        capsule.ChildEntities.Add(item);

        await processor.Process(new AuthProcessorContext(opener, packetSender), new PlayerTimeCapsuleContentRequest(capsule.Id));

        PlayerTimeCapsuleContent content = ConnectedPlayers.SentTo<PlayerTimeCapsuleContent>(packetSender, opener).Single();
        Assert.AreEqual(capsule.Id, content.CapsuleId);
        Assert.AreEqual("Hello from rocket two", content.Metadata.Text);
        Assert.AreSame(item, content.Items.Single());
        Assert.AreEqual(1, packetSender.ReceivedCalls().Count(), "Only the player who opens the capsule is told.");
    }

    [TestMethod]
    public async Task CapsuleWithoutTextIsSentWithEmptyText()
    {
        Player opener = players.Connect("Opener");
        PlayerTimeCapsuleEntity capsule = AddRocketCapsule(null);

        await processor.Process(new AuthProcessorContext(opener, packetSender), new PlayerTimeCapsuleContentRequest(capsule.Id));

        PlayerTimeCapsuleContent content = ConnectedPlayers.SentTo<PlayerTimeCapsuleContent>(packetSender, opener).Single();
        Assert.AreEqual("", content.Metadata.Text);
        Assert.AreEqual(0, content.Items.Count);
    }

    [TestMethod]
    public async Task UnknownCapsuleGetsNoAnswer()
    {
        Player opener = players.Connect("Opener");

        await processor.Process(new AuthProcessorContext(opener, packetSender), new PlayerTimeCapsuleContentRequest(new NitroxId()));

        Assert.AreEqual(0, packetSender.ReceivedCalls().Count());
    }

    [TestMethod]
    public async Task ContentListIsACopyOfTheCapsulesChildren()
    {
        Player opener = players.Connect("Opener");
        PlayerTimeCapsuleEntity capsule = AddRocketCapsule(null);

        await processor.Process(new AuthProcessorContext(opener, packetSender), new PlayerTimeCapsuleContentRequest(capsule.Id));

        PlayerTimeCapsuleContent content = ConnectedPlayers.SentTo<PlayerTimeCapsuleContent>(packetSender, opener).Single();
        Assert.AreNotSame(capsule.ChildEntities, content.Items, "Items moved in or out later must not change a packet on its way.");
    }

    private PlayerTimeCapsuleEntity AddRocketCapsule(PlayerTimeCapsuleMetadata text)
    {
        VehicleEntity rocket = new(null, 0f, new NitroxTransform(), "", false, new NitroxId(), new NitroxTechType("RocketBase"), null);
        PlayerTimeCapsuleEntity capsule = new(new NitroxId(), rocket.Id, text);
        rocket.ChildEntities.Add(capsule);
        entityRegistry.AddOrUpdate(rocket);
        return capsule;
    }
}
