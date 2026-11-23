using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Nitrox.Model.DataStructures;
using Nitrox.Model.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Bases;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;
using Nitrox.Model.Subnautica.Packets;
using Nitrox.Server.Subnautica.Models.GameLogic.Entities;
using Nitrox.Server.Subnautica.Models.Packets.Core;
using Nitrox.Test.Server.Helper;
using NSubstitute;

namespace Nitrox.Server.Subnautica.Models.Packets.Processors;

/// <summary>
///     A medical cabinet runs on one timer for all players: the first a game reports for a cabinet without a stored state
///     becomes the cabinet's, and a game that reports one later takes the stored one.
/// </summary>
[TestClass]
public sealed class MedicalCabinetTimerStartedProcessorTest
{
    private readonly ConnectedPlayers players = new();
    private readonly IPacketSender packetSender = Substitute.For<IPacketSender>();
    private EntityRegistry entityRegistry;
    private MedicalCabinetTimerStartedProcessor processor;
    private Player first;
    private Player second;

    [TestInitialize]
    public void Init()
    {
        entityRegistry = new EntityRegistry(NullLogger<EntityRegistry>.Instance);
        processor = new MedicalCabinetTimerStartedProcessor(players.PlayerManager, entityRegistry, NullLogger<MedicalCabinetTimerStartedProcessor>.Instance);
        first = players.Connect("First");
        second = players.Connect("Second");
    }

    [TestMethod]
    public async Task TheFirstTimerReportedForACabinetWithoutStateIsStoredAndGivenToTheOthers()
    {
        ModuleEntity cabinet = AddEntity("MedicalCabinet", null);
        MedicalCabinetMetadata firstTimer = new(false, false, 1500f);

        await Report(first, cabinet, firstTimer);

        Assert.AreSame(firstTimer, cabinet.Metadata);
        Assert.AreSame(firstTimer, ConnectedPlayers.SentTo<EntityMetadataUpdate>(packetSender, second).Single().NewValue);
        Assert.AreEqual(0, ConnectedPlayers.SentTo<EntityMetadataUpdate>(packetSender, first).Count, "Its game runs on this timer already.");
    }

    [TestMethod]
    public async Task ATimerReportedForACabinetWithStateIsAnsweredWithTheStoredOne()
    {
        MedicalCabinetMetadata stored = new(false, false, 1500f);
        ModuleEntity cabinet = AddEntity("MedicalCabinet", stored);

        await Report(second, cabinet, new MedicalCabinetMetadata(false, false, 1730f));

        Assert.AreSame(stored, cabinet.Metadata);
        Assert.AreSame(stored, ConnectedPlayers.SentTo<EntityMetadataUpdate>(packetSender, second).Single().NewValue);
        Assert.AreEqual(0, ConnectedPlayers.SentTo<EntityMetadataUpdate>(packetSender, first).Count);
    }

    [TestMethod]
    public async Task ATimerForSomethingThatIsNoCabinetIsIgnored()
    {
        ModuleEntity locker = AddEntity("Locker", null);

        await Report(first, locker, new MedicalCabinetMetadata(false, false, 1500f));

        Assert.IsNull(locker.Metadata);
        Assert.AreEqual(0, packetSender.ReceivedCalls().Count());
    }

    private ModuleEntity AddEntity(string techType, EntityMetadata metadata)
    {
        ModuleEntity entity = ModuleEntity.MakeEmpty();
        entity.Id = new NitroxId();
        entity.TechType = new NitroxTechType(techType);
        entity.Metadata = metadata;
        entityRegistry.AddEntity(entity);
        return entity;
    }

    private Task Report(Player sender, Entity cabinet, MedicalCabinetMetadata timer)
    {
        return processor.Process(new AuthProcessorContext(sender, packetSender), new MedicalCabinetTimerStarted(cabinet.Id, timer));
    }
}
