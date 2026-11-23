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
using ServerPlayer = Nitrox.Server.Subnautica.Models.Player;

namespace Nitrox.Test.Server.Subnautica;

/// <summary>
///     Generic metadata updates are accepted from any player. A base power source's state is the exception: only the
///     player simulating the source decides it, through its own packet.
/// </summary>
[TestClass]
public sealed class EntityMetadataUpdateProcessorPowerSourceTest
{
    [TestMethod]
    public async Task PowerStateCannotBeSetThroughGenericMetadata()
    {
        ScannerRoomScenarioFixture scenario = new();
        ModuleEntity panel = AddSource(scenario, new PowerSourceMetadata(30f, 75f, BasePowerSourceType.SOLAR, 5));
        Assert.IsTrue(scenario.Ownership.TryAcquire(panel.Id, scenario.PlayerA, SimulationLockType.TRANSIENT));

        await Send(scenario, scenario.PlayerA, panel, new PowerSourceMetadata(75f, 75f, BasePowerSourceType.SOLAR, 99));

        PowerSourceMetadata stored = (PowerSourceMetadata)panel.Metadata;
        Assert.AreEqual(30f, stored.Power);
        Assert.AreEqual(5, stored.Revision);
    }

    [TestMethod]
    public async Task PlayerNotSimulatingASourceCannotReplaceItsPowerState()
    {
        // Any other kind of metadata would erase level, fuel progress and revision; the source would then come back
        // full for the next player who joins.
        ScannerRoomScenarioFixture scenario = new();
        ModuleEntity panel = AddSource(scenario, new PowerSourceMetadata(30f, 75f, BasePowerSourceType.SOLAR, 5));
        Assert.IsTrue(scenario.Ownership.TryAcquire(panel.Id, scenario.PlayerA, SimulationLockType.TRANSIENT));

        await Send(scenario, scenario.PlayerB, panel, new BatteryMetadata(100f));

        Assert.IsInstanceOfType<PowerSourceMetadata>(panel.Metadata);
        Assert.AreEqual(30f, ((PowerSourceMetadata)panel.Metadata).Power);
    }

    [TestMethod]
    public async Task SourceNobodySimulatesKeepsItsPowerStateToo()
    {
        ScannerRoomScenarioFixture scenario = new();
        ModuleEntity panel = AddSource(scenario, new PowerSourceMetadata(30f, 75f, BasePowerSourceType.SOLAR, 5));

        await Send(scenario, scenario.PlayerB, panel, new BatteryMetadata(100f));

        Assert.IsInstanceOfType<PowerSourceMetadata>(panel.Metadata);
    }

    [TestMethod]
    public async Task TemperatureFromAnyPlayerIsMergedIntoThePowerState()
    {
        // Every player near a thermal plant measures the water; none of them needs to simulate the plant for that.
        ScannerRoomScenarioFixture scenario = new();
        ModuleEntity plant = AddSource(scenario, new PowerSourceMetadata(120f, 250f, BasePowerSourceType.THERMAL, 500, temperature: 40f));
        Assert.IsTrue(scenario.Ownership.TryAcquire(plant.Id, scenario.PlayerA, SimulationLockType.TRANSIENT));

        await Send(scenario, scenario.PlayerB, plant, new ThermalPlantMetadata(63f));

        PowerSourceMetadata stored = (PowerSourceMetadata)plant.Metadata;
        Assert.AreEqual(63f, stored.Temperature);
        Assert.AreEqual(120f, stored.Power);
        Assert.AreEqual(500, stored.Revision);
    }

    [TestMethod]
    public async Task ImpossibleTemperatureIsNotStoredAsReported()
    {
        ScannerRoomScenarioFixture scenario = new();
        ModuleEntity plant = AddSource(scenario, new PowerSourceMetadata(120f, 250f, BasePowerSourceType.THERMAL, 500, temperature: 40f));
        ModuleEntity newPlant = AddSource(scenario, null);

        await Send(scenario, scenario.PlayerB, plant, new ThermalPlantMetadata(1e9f));
        await Send(scenario, scenario.PlayerB, newPlant, new ThermalPlantMetadata(1e9f));

        Assert.AreEqual(BasePowerSourceAuthority.MAX_THERMAL_TEMPERATURE, ((PowerSourceMetadata)plant.Metadata).Temperature);
        Assert.AreEqual(BasePowerSourceAuthority.MAX_THERMAL_TEMPERATURE, ((ThermalPlantMetadata)newPlant.Metadata).Temperature,
            "A plant that has no power state yet stores the report itself.");
    }

    [TestMethod]
    public async Task TemperatureThatIsNotANumberIsDropped()
    {
        ScannerRoomScenarioFixture scenario = new();
        ModuleEntity newPlant = AddSource(scenario, null);

        await Send(scenario, scenario.PlayerB, newPlant, new ThermalPlantMetadata(float.NaN));

        Assert.IsNull(newPlant.Metadata);
    }

    private static Task Send(ScannerRoomScenarioFixture scenario, ServerPlayer sender, ModuleEntity entity, EntityMetadata metadata)
    {
        EntityMetadataUpdateProcessor processor = new(new PlayerManager(null!, null!, null!, NullLogger<PlayerManager>.Instance), scenario.EntityRegistry,
            scenario.Ownership, scenario.Diagnostics, new BasePowerSourceAuthority(), NullLogger<EntityMetadataUpdateProcessor>.Instance);
        return processor.Process(new AuthProcessorContext(sender, new SilentSender()), new EntityMetadataUpdate(entity.Id, metadata));
    }

    private static ModuleEntity AddSource(ScannerRoomScenarioFixture scenario, EntityMetadata? metadata)
    {
        ModuleEntity source = ModuleEntity.MakeEmpty();
        source.Id = new NitroxId();
        source.Metadata = metadata;
        scenario.EntityRegistry.AddEntity(source);
        return source;
    }

    private sealed class SilentSender : IPacketSender
    {
        public ValueTask SendPacketAsync<T>(T packet, SessionId sessionId) where T : Packet => ValueTask.CompletedTask;

        public ValueTask SendPacketToAllAsync<T>(T packet) where T : Packet => ValueTask.CompletedTask;

        public ValueTask SendPacketToOthersAsync<T>(T packet, SessionId excludedSessionId) where T : Packet => ValueTask.CompletedTask;
    }
}
