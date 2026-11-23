using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Nitrox.Model.Core;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Packets;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Bases;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;
using Nitrox.Model.Subnautica.Packets;
using Nitrox.Server.Subnautica.Models.GameLogic;
using Nitrox.Server.Subnautica.Models.GameLogic.Entities;
using Nitrox.Server.Subnautica.Models.Packets.Core;
using Nitrox.Server.Subnautica.Models.Packets.Processors;

namespace Nitrox.Test.Server.Subnautica;

[TestClass]
public sealed class BasePowerSourceUpdateProcessorTest
{
    [TestMethod]
    public async Task AcceptedUpdateIsNotEchoedToTheOwner()
    {
        ScannerRoomScenarioFixture scenario = new();
        ModuleEntity source = AddSolarPanel(scenario);
        Assert.IsTrue(scenario.Ownership.TryToAcquire(source.Id, scenario.PlayerA, SimulationLockType.TRANSIENT));
        RecordingSender sender = new();

        await CreateProcessor(scenario).Process(new AuthProcessorContext(scenario.PlayerA, sender),
            new BasePowerSourceUpdate(source.Id, BasePowerSourceType.SOLAR, 40f, 1));

        BasePowerSourceUpdate broadcast = sender.ToOthers.Single();
        Assert.IsTrue(broadcast.Granted);
        Assert.AreEqual(40f, broadcast.Power);
        Assert.AreEqual(1, broadcast.Revision);
        Assert.AreEqual(0, sender.ToAll.Count, "The owner already simulates this state; an echo would roll it back.");
        Assert.AreEqual(0, sender.Replies.Count);
    }

    [TestMethod]
    public async Task UpdateFromNonOwnerIsRejectedToTheSenderOnly()
    {
        ScannerRoomScenarioFixture scenario = new();
        ModuleEntity source = AddSolarPanel(scenario);
        source.Metadata = new PowerSourceMetadata(30f, 75f, BasePowerSourceType.SOLAR, 5);
        Assert.IsTrue(scenario.Ownership.TryToAcquire(source.Id, scenario.PlayerA, SimulationLockType.TRANSIENT));
        RecordingSender sender = new();

        await CreateProcessor(scenario).Process(new AuthProcessorContext(scenario.PlayerB, sender),
            new BasePowerSourceUpdate(source.Id, BasePowerSourceType.SOLAR, 70f, 1));

        BasePowerSourceUpdate reply = sender.Replies.Single();
        Assert.IsFalse(reply.Granted);
        Assert.AreEqual(30f, reply.Power, "The rejection must carry the canonical state.");
        Assert.AreEqual(5, reply.Revision);
        Assert.AreEqual(0, sender.ToOthers.Count);
        Assert.AreEqual(0, sender.ToAll.Count);
        Assert.AreEqual(30f, ((PowerSourceMetadata)source.Metadata).Power);
    }

    private static BasePowerSourceUpdateProcessor CreateProcessor(ScannerRoomScenarioFixture scenario) => new(scenario.EntityRegistry,
        scenario.Ownership, new BasePowerSourceAuthority(), new BasePowerDiagnostics(), NullLogger<BasePowerSourceUpdateProcessor>.Instance);

    private static ModuleEntity AddSolarPanel(ScannerRoomScenarioFixture scenario)
    {
        ModuleEntity source = ModuleEntity.MakeEmpty();
        source.Id = new NitroxId();
        scenario.EntityRegistry.AddEntity(source);
        return source;
    }

    private sealed class RecordingSender : IPacketSender
    {
        internal List<BasePowerSourceUpdate> Replies { get; } = [];
        internal List<BasePowerSourceUpdate> ToAll { get; } = [];
        internal List<BasePowerSourceUpdate> ToOthers { get; } = [];

        public ValueTask SendPacketAsync<T>(T packet, SessionId sessionId) where T : Packet
        {
            Replies.Add((BasePowerSourceUpdate)(Packet)packet);
            return ValueTask.CompletedTask;
        }

        public ValueTask SendPacketToAllAsync<T>(T packet) where T : Packet
        {
            ToAll.Add((BasePowerSourceUpdate)(Packet)packet);
            return ValueTask.CompletedTask;
        }

        public ValueTask SendPacketToOthersAsync<T>(T packet, SessionId excludedSessionId) where T : Packet
        {
            ToOthers.Add((BasePowerSourceUpdate)(Packet)packet);
            return ValueTask.CompletedTask;
        }
    }
}
