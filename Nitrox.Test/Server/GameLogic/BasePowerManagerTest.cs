using Microsoft.Extensions.Logging.Abstractions;
using Nitrox.Model.DataStructures;
using Nitrox.Model.DataStructures.GameLogic;
using Nitrox.Model.DataStructures.Unity;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Bases;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;
using Nitrox.Server.Subnautica.Models.GameLogic.Entities;

namespace Nitrox.Server.Subnautica.Models.GameLogic;

[TestClass]
public sealed class BasePowerManagerTest
{
    private EntityRegistry entityRegistry;
    private SimulationOwnershipData simulationOwnershipData;
    private BasePowerDiagnostics diagnostics;
    private BasePowerManager manager;
    private Player simulator;
    private Player otherPlayer;

    [TestInitialize]
    public void Init()
    {
        entityRegistry = new EntityRegistry(NullLogger<EntityRegistry>.Instance);
        simulationOwnershipData = new SimulationOwnershipData();
        diagnostics = new BasePowerDiagnostics();
        manager = new BasePowerManager(entityRegistry, simulationOwnershipData, diagnostics, NullLogger<BasePowerManager>.Instance);
        simulator = CreatePlayer(1);
        otherPlayer = CreatePlayer(2);
    }

    [TestMethod]
    [DataRow(BasePowerSourceType.SOLAR, 75f)]
    [DataRow(BasePowerSourceType.THERMAL, 250f)]
    [DataRow(BasePowerSourceType.BIOREACTOR, 500f)]
    [DataRow(BasePowerSourceType.NUCLEAR, 2500f)]
    public void StateFromTheSimulatingPlayerIsStored(BasePowerSourceType sourceType, float capacity)
    {
        Entity source = AddSource(sourceType, simulator);

        Assert.IsTrue(manager.TryAccept(simulator, source, new PowerSourceMetadata(capacity / 2, 0f, sourceType), out PowerSourceMetadata accepted, out string reason), reason);

        Assert.AreEqual(capacity / 2, accepted.Power);
        Assert.AreSame(accepted, source.Metadata);

        Assert.IsTrue(manager.TryAccept(simulator, source, new PowerSourceMetadata(capacity, 0f, sourceType), out accepted, out reason), reason);
        Assert.AreEqual(capacity, accepted.Power, "A source can be filled to its capacity.");
    }

    [TestMethod]
    public void StateFromAPlayerWhoDoesNotSimulateTheSourceIsRefused()
    {
        // Its level is a copy of what the simulating player told it, at best. Stored, it would replace the real one.
        Entity panel = AddSource(BasePowerSourceType.SOLAR, simulator);
        panel.Metadata = new PowerSourceMetadata(30f, 0f, BasePowerSourceType.SOLAR);

        Assert.IsFalse(manager.TryAccept(otherPlayer, panel, new PowerSourceMetadata(70f, 0f, BasePowerSourceType.SOLAR), out _, out string reason));

        StringAssert.Contains(reason, "does not own source simulation");
        Assert.AreEqual(30f, ((PowerSourceMetadata)panel.Metadata).Power);
    }

    [TestMethod]
    public void LateStateFromThePlayerWhoSimulatedTheSourceBeforeIsRefused()
    {
        // The simulation moved on while this update travelled; the new simulating player's level counts now.
        Entity panel = AddSource(BasePowerSourceType.SOLAR, simulator);
        Assert.IsTrue(manager.TryAccept(simulator, panel, new PowerSourceMetadata(30f, 0f, BasePowerSourceType.SOLAR), out _, out _));
        simulationOwnershipData.RevokeIfOwner(panel.Id, simulator);
        Assert.IsTrue(simulationOwnershipData.TryAcquire(panel.Id, otherPlayer, SimulationLockType.TRANSIENT));
        Assert.IsTrue(manager.TryAccept(otherPlayer, panel, new PowerSourceMetadata(40f, 0f, BasePowerSourceType.SOLAR), out _, out _));

        Assert.IsFalse(manager.TryAccept(simulator, panel, new PowerSourceMetadata(31f, 0f, BasePowerSourceType.SOLAR), out _, out _));

        Assert.AreEqual(40f, ((PowerSourceMetadata)panel.Metadata).Power);
    }

    [TestMethod]
    public void StateForASourceNobodySimulatesIsRefused()
    {
        Entity panel = AddSource(BasePowerSourceType.SOLAR, null);

        Assert.IsFalse(manager.TryAccept(simulator, panel, new PowerSourceMetadata(70f, 0f, BasePowerSourceType.SOLAR), out _, out _));

        Assert.IsNull(panel.Metadata);
    }

    [TestMethod]
    public void SourceBuiltAgainUnderTheSameIdIsAcceptedLikeANewOne()
    {
        // Deconstructing a reactor to a ghost and finishing it again keeps its id but sends a new entity.
        Entity reactor = AddSource(BasePowerSourceType.NUCLEAR, simulator);
        Assert.IsTrue(manager.TryAccept(simulator, reactor, new PowerSourceMetadata(900f, 5000f, BasePowerSourceType.NUCLEAR), out _, out _));

        InteriorPieceEntity rebuilt = InteriorPieceEntity.MakeEmpty();
        rebuilt.Id = reactor.Id;
        rebuilt.Metadata = new PowerSourceMetadata(900f, 0f, BasePowerSourceType.NUCLEAR);
        Assert.IsTrue(manager.TryAccept(simulator, rebuilt, new PowerSourceMetadata(905f, 12f, BasePowerSourceType.NUCLEAR), out PowerSourceMetadata accepted, out string reason), reason);

        Assert.AreEqual(905f, accepted.Power);
        Assert.AreSame(accepted, rebuilt.Metadata);
    }

    [TestMethod]
    [DataRow(float.NaN)]
    [DataRow(float.PositiveInfinity)]
    [DataRow(-1f)]
    [DataRow(76f)]
    public void LevelASolarPanelCannotHaveIsRefused(float power)
    {
        Entity panel = AddSource(BasePowerSourceType.SOLAR, simulator);

        Assert.IsFalse(manager.TryAccept(simulator, panel, new PowerSourceMetadata(power, 0f, BasePowerSourceType.SOLAR), out _, out string reason));

        StringAssert.Contains(reason, "outside");
        Assert.IsNull(panel.Metadata);
    }

    [TestMethod]
    public void SmallFloatingPointOvershootIsStoredAsFull()
    {
        Entity panel = AddSource(BasePowerSourceType.SOLAR, simulator);

        Assert.IsTrue(manager.TryAccept(simulator, panel, new PowerSourceMetadata(75.005f, 0f, BasePowerSourceType.SOLAR), out PowerSourceMetadata accepted, out _));

        Assert.AreEqual(75f, accepted.Power);
    }

    [TestMethod]
    [DataRow(BasePowerSourceType.BIOREACTOR, 420f)]
    [DataRow(BasePowerSourceType.NUCLEAR, 12345f)]
    public void ReactorFuelProgressIsStored(BasePowerSourceType sourceType, float fuelConsumed)
    {
        Entity reactor = AddSource(sourceType, simulator);

        Assert.IsTrue(manager.TryAccept(simulator, reactor, new PowerSourceMetadata(100f, fuelConsumed, sourceType), out PowerSourceMetadata accepted, out _));

        Assert.AreEqual(fuelConsumed, accepted.FuelConsumed);
        Assert.AreEqual(fuelConsumed, ((PowerSourceMetadata)reactor.Metadata).FuelConsumed);
    }

    [TestMethod]
    [DataRow(BasePowerSourceType.SOLAR, 1f)]
    [DataRow(BasePowerSourceType.BIOREACTOR, 841f)]
    [DataRow(BasePowerSourceType.NUCLEAR, 20001f)]
    [DataRow(BasePowerSourceType.NUCLEAR, -1f)]
    [DataRow(BasePowerSourceType.NUCLEAR, float.NaN)]
    public void FuelProgressTheSourceCannotHaveIsRefused(BasePowerSourceType sourceType, float fuelConsumed)
    {
        Entity source = AddSource(sourceType, simulator);

        Assert.IsFalse(manager.TryAccept(simulator, source, new PowerSourceMetadata(10f, fuelConsumed, sourceType), out _, out string reason));

        StringAssert.Contains(reason, "fuel progress");
    }

    [TestMethod]
    public void SourceTypeThatDoesNotFitTheEntityIsRefused()
    {
        Entity exteriorModule = AddSource(BasePowerSourceType.SOLAR, simulator);
        Entity bioreactor = AddSource(BasePowerSourceType.BIOREACTOR, simulator);
        bioreactor.Metadata = new PowerSourceMetadata(400f, 0f, BasePowerSourceType.BIOREACTOR);
        // Without a tech type only the stored state says what the source is
        bioreactor.TechType = NitroxTechType.None;
        Entity thermalPlant = AddSource(BasePowerSourceType.THERMAL, simulator);
        thermalPlant.TechType = new NitroxTechType("ThermalPlant");
        Entity withoutTechType = AddSource(BasePowerSourceType.SOLAR, simulator);
        withoutTechType.TechType = NitroxTechType.None;

        Assert.IsFalse(manager.TryAccept(simulator, exteriorModule, new PowerSourceMetadata(10f, 0f, BasePowerSourceType.NUCLEAR), out _, out string incompatible));
        Assert.IsFalse(manager.TryAccept(simulator, bioreactor, new PowerSourceMetadata(10f, 0f, BasePowerSourceType.NUCLEAR), out _, out string changed));
        Assert.IsFalse(manager.TryAccept(simulator, thermalPlant, new PowerSourceMetadata(20f, 0f, BasePowerSourceType.SOLAR), out _, out string wrongTechType));
        Assert.IsFalse(manager.TryAccept(simulator, exteriorModule, new PowerSourceMetadata(0f, 0f, BasePowerSourceType.UNKNOWN), out _, out _));
        Assert.IsTrue(manager.TryAccept(simulator, withoutTechType, new PowerSourceMetadata(20f, 0f, BasePowerSourceType.SOLAR), out _, out _));
        StringAssert.Contains(incompatible, "incompatible");
        StringAssert.Contains(changed, "changed");
        StringAssert.Contains(wrongTechType, "tech type");
    }

    [TestMethod]
    public void LevelUpdateKeepsTheStoredTemperature()
    {
        // The temperature comes from reports any player sends; the simulating player's state must not reset it.
        Entity plant = AddSource(BasePowerSourceType.THERMAL, simulator);
        plant.Metadata = new PowerSourceMetadata(120f, 0f, BasePowerSourceType.THERMAL, 63f);

        Assert.IsTrue(manager.TryAccept(simulator, plant, new PowerSourceMetadata(121f, 0f, BasePowerSourceType.THERMAL), out PowerSourceMetadata accepted, out string reason), reason);

        Assert.AreEqual(121f, accepted.Power);
        Assert.AreEqual(63f, accepted.Temperature);
    }

    [TestMethod]
    public void FirstLevelUpdateCarriesOverATemperatureStoredOnItsOwn()
    {
        // A newly built plant can get its temperature report before its first level update.
        Entity plant = AddSource(BasePowerSourceType.THERMAL, simulator);
        plant.Metadata = new ThermalPlantMetadata(48f);

        Assert.IsTrue(manager.TryAccept(simulator, plant, new PowerSourceMetadata(5f, 0f, BasePowerSourceType.THERMAL), out PowerSourceMetadata accepted, out string reason), reason);

        Assert.AreEqual(48f, accepted.Temperature);
    }

    [TestMethod]
    public void TemperatureReportIsKeptInsideThePowerState()
    {
        // An entity has one metadata slot. A report taking it would erase the level.
        Entity plant = AddSource(BasePowerSourceType.THERMAL, simulator);
        plant.Metadata = new PowerSourceMetadata(120f, 0f, BasePowerSourceType.THERMAL, 40f);

        Assert.IsTrue(manager.TryApplyTemperature(plant, new ThermalPlantMetadata(63f)));

        PowerSourceMetadata stored = (PowerSourceMetadata)plant.Metadata;
        Assert.AreEqual(63f, stored.Temperature);
        Assert.AreEqual(120f, stored.Power);
    }

    [TestMethod]
    public void TemperatureOnlyRises()
    {
        Entity plant = AddSource(BasePowerSourceType.THERMAL, simulator);
        plant.Metadata = new PowerSourceMetadata(10f, 0f, BasePowerSourceType.THERMAL, 70f);

        Assert.IsFalse(manager.TryApplyTemperature(plant, new ThermalPlantMetadata(70f)));
        Assert.IsFalse(manager.TryApplyTemperature(plant, new ThermalPlantMetadata(55f)));
        Assert.IsFalse(manager.TryApplyTemperature(plant, new ThermalPlantMetadata(float.NaN)));
        Assert.AreEqual(70f, ((PowerSourceMetadata)plant.Metadata).Temperature);
    }

    [TestMethod]
    public void TemperatureIsStoredNoHigherThanAThermalPlantCanUse()
    {
        // Any player may report a temperature. Above 100 a plant's output and gauge no longer change.
        Entity plant = AddSource(BasePowerSourceType.THERMAL, simulator);
        plant.Metadata = new PowerSourceMetadata(10f, 0f, BasePowerSourceType.THERMAL, 70f);

        Assert.IsTrue(manager.TryApplyTemperature(plant, new ThermalPlantMetadata(1e9f)));

        Assert.AreEqual(BasePowerManager.MAX_THERMAL_TEMPERATURE, ((PowerSourceMetadata)plant.Metadata).Temperature);
        Assert.IsFalse(manager.TryApplyTemperature(plant, new ThermalPlantMetadata(5e9f)), "Nothing above the limit is news.");
    }

    [TestMethod]
    public void TemperatureForAPlantWithoutPowerStateIsStoredOnItsOwn()
    {
        Entity plant = AddSource(BasePowerSourceType.THERMAL, simulator);

        Assert.IsFalse(manager.TryApplyTemperature(plant, new ThermalPlantMetadata(float.NaN)));
        Assert.IsFalse(manager.TryApplyTemperature(plant, new ThermalPlantMetadata(float.PositiveInfinity)));
        Assert.IsNull(plant.Metadata);

        Assert.IsTrue(manager.TryApplyTemperature(plant, new ThermalPlantMetadata(250f)));
        Assert.AreEqual(BasePowerManager.MAX_THERMAL_TEMPERATURE, ((ThermalPlantMetadata)plant.Metadata).Temperature);

        plant.Metadata = new ThermalPlantMetadata(48f);
        Assert.IsFalse(manager.TryApplyTemperature(plant, new ThermalPlantMetadata(48f)));
        Assert.IsTrue(manager.TryApplyTemperature(plant, new ThermalPlantMetadata(49f)));
        Assert.AreEqual(49f, ((ThermalPlantMetadata)plant.Metadata).Temperature);
    }

    [TestMethod]
    public void AcceptedStateIsForTheOtherPlayersOnly()
    {
        Entity panel = AddSource(BasePowerSourceType.SOLAR, simulator);

        MetadataUpdateResult result = manager.AcceptState(simulator, panel, new PowerSourceMetadata(40f, 0f, BasePowerSourceType.SOLAR));

        Assert.AreEqual(MetadataUpdateOutcome.ACCEPT, result.Outcome);
        Assert.AreSame(panel.Metadata, result.Metadata);
    }

    [TestMethod]
    public void RefusedSenderIsGivenTheStoredState()
    {
        // It applied its own level locally and would keep showing it.
        Entity panel = AddSource(BasePowerSourceType.SOLAR, simulator);
        PowerSourceMetadata stored = new(30f, 0f, BasePowerSourceType.SOLAR);
        panel.Metadata = stored;

        MetadataUpdateResult result = manager.AcceptState(otherPlayer, panel, new PowerSourceMetadata(70f, 0f, BasePowerSourceType.SOLAR));

        Assert.AreEqual(MetadataUpdateOutcome.REJECT_AND_CORRECT, result.Outcome);
        Assert.AreSame(stored, result.Metadata);
        Assert.AreSame(stored, panel.Metadata);
    }

    [TestMethod]
    public void RefusedSenderOfASourceWithoutStoredStateGetsNothing()
    {
        Entity panel = AddSource(BasePowerSourceType.SOLAR, simulator);

        MetadataUpdateResult result = manager.AcceptState(otherPlayer, panel, new PowerSourceMetadata(70f, 0f, BasePowerSourceType.SOLAR));

        Assert.AreEqual(MetadataUpdateOutcome.REJECT, result.Outcome);
    }

    [TestMethod]
    public void AcceptedTemperatureReportIsPassedOnWithinTheLimit()
    {
        Entity plant = AddSource(BasePowerSourceType.THERMAL, simulator);
        plant.Metadata = new PowerSourceMetadata(10f, 0f, BasePowerSourceType.THERMAL, 70f);

        MetadataUpdateResult risen = manager.AcceptTemperature(plant, new ThermalPlantMetadata(1e9f));
        MetadataUpdateResult stale = manager.AcceptTemperature(plant, new ThermalPlantMetadata(80f));

        Assert.AreEqual(MetadataUpdateOutcome.ACCEPT, risen.Outcome);
        Assert.AreEqual(BasePowerManager.MAX_THERMAL_TEMPERATURE, ((ThermalPlantMetadata)risen.Metadata).Temperature);
        Assert.AreEqual(MetadataUpdateOutcome.REJECT, stale.Outcome);
    }

    [TestMethod]
    public void OtherMetadataNeverTakesThePlaceOfPowerState()
    {
        Entity panel = AddSource(BasePowerSourceType.SOLAR, simulator);
        PowerSourceMetadata stored = new(30f, 0f, BasePowerSourceType.SOLAR);
        panel.Metadata = stored;

        MetadataUpdateResult result = manager.RejectReplacement(simulator, panel, new BatteryMetadata(100f));

        Assert.AreEqual(MetadataUpdateOutcome.REJECT, result.Outcome);
        Assert.AreSame(stored, panel.Metadata);
    }

    [TestMethod]
    public void ConsumptionByAnotherPlayerGoesToTheSimulatingPlayerAndLeavesTheStoredStateAlone()
    {
        // The simulating player keeps the level. Changing the stored copy here would be overwritten by its next state.
        Entity reactor = AddSource(BasePowerSourceType.NUCLEAR, simulator);
        reactor.Metadata = new PowerSourceMetadata(900f, 0f, BasePowerSourceType.NUCLEAR);

        ConsumptionRoute route = manager.RouteConsumption(otherPlayer, reactor, 12.5f, out Player routedTo, out PowerSourceMetadata accepted);

        Assert.AreEqual(ConsumptionRoute.FORWARD_TO_SIMULATOR, route);
        Assert.AreSame(simulator, routedTo);
        Assert.IsNull(accepted);
        Assert.AreEqual(900f, ((PowerSourceMetadata)reactor.Metadata).Power);
    }

    [TestMethod]
    public void ConsumptionReportedByTheSimulatingPlayerItselfIsRefused()
    {
        // It is already part of the level that player simulates.
        Entity reactor = AddSource(BasePowerSourceType.NUCLEAR, simulator);
        reactor.Metadata = new PowerSourceMetadata(900f, 0f, BasePowerSourceType.NUCLEAR);

        Assert.AreEqual(ConsumptionRoute.REJECTED, manager.RouteConsumption(simulator, reactor, 5f, out _, out _));
        Assert.AreEqual(900f, ((PowerSourceMetadata)reactor.Metadata).Power);
    }

    [TestMethod]
    public void ConsumptionFromASourceNobodySimulatesIsTakenFromTheStoredState()
    {
        Entity plant = AddSource(BasePowerSourceType.THERMAL, null);
        plant.Metadata = new PowerSourceMetadata(30f, 0f, BasePowerSourceType.THERMAL, 61f);

        Assert.AreEqual(ConsumptionRoute.APPLIED_BY_SERVER, manager.RouteConsumption(otherPlayer, plant, 10f, out Player routedTo, out PowerSourceMetadata accepted));
        Assert.IsNull(routedTo);
        Assert.AreEqual(20f, accepted.Power);
        Assert.AreEqual(61f, accepted.Temperature);
        Assert.AreSame(accepted, plant.Metadata);

        Assert.AreEqual(ConsumptionRoute.APPLIED_BY_SERVER, manager.RouteConsumption(otherPlayer, plant, 500f, out _, out accepted));
        Assert.AreEqual(0f, accepted.Power, "A source cannot go below empty.");
    }

    [TestMethod]
    [DataRow(0f)]
    [DataRow(-3f)]
    [DataRow(float.NaN)]
    [DataRow(float.PositiveInfinity)]
    [DataRow(2500.5f)]
    public void ConsumptionOfAnImpossibleAmountIsRefused(float amount)
    {
        Entity reactor = AddSource(BasePowerSourceType.NUCLEAR, null);
        reactor.Metadata = new PowerSourceMetadata(900f, 0f, BasePowerSourceType.NUCLEAR);

        Assert.AreEqual(ConsumptionRoute.REJECTED, manager.RouteConsumption(otherPlayer, reactor, amount, out _, out _));
        Assert.AreEqual(900f, ((PowerSourceMetadata)reactor.Metadata).Power);
    }

    [TestMethod]
    public void ConsumptionFromSomethingThatIsNoPowerSourceIsRefused()
    {
        Entity withoutState = AddSource(BasePowerSourceType.SOLAR, null);
        WorldEntity vehicle = new(NitroxVector3.Zero, NitroxQuaternion.Identity, NitroxVector3.One, new NitroxTechType("Seamoth"), 0, "seamoth-class-id", false, new NitroxId(), null);

        Assert.AreEqual(ConsumptionRoute.REJECTED, manager.RouteConsumption(otherPlayer, withoutState, 5f, out _, out _));
        Assert.AreEqual(ConsumptionRoute.REJECTED, manager.RouteConsumption(otherPlayer, vehicle, 5f, out _, out _));
    }

    [TestMethod]
    public void ConsumptionFromABasePieceThatIsNoPowerSourceIsNotPassedToItsSimulatingPlayer()
    {
        // That player would find no power source on it and give its simulation up.
        Entity locker = AddLocker(simulator);

        Assert.AreEqual(ConsumptionRoute.REJECTED, manager.RouteConsumption(otherPlayer, locker, 5f, out _, out _));
    }

    [TestMethod]
    public void ConsumptionFromASourceThatHasNotReportedYetIsPassedToItsSimulatingPlayer()
    {
        Entity justBuilt = AddSource(BasePowerSourceType.NUCLEAR, simulator);

        Assert.AreEqual(ConsumptionRoute.FORWARD_TO_SIMULATOR, manager.RouteConsumption(otherPlayer, justBuilt, 5f, out Player routedTo, out _));
        Assert.AreSame(simulator, routedTo);
    }

    [TestMethod]
    public void TemperatureReportForSomethingThatIsNoThermalPlantIsRefused()
    {
        // Any player may report a temperature. It must not take the place of what another entity holds.
        Entity locker = AddLocker(simulator);
        Entity panel = AddSource(BasePowerSourceType.SOLAR, simulator);
        PowerSourceMetadata panelState = new(40f, 0f, BasePowerSourceType.SOLAR);
        panel.Metadata = panelState;
        Entity reactor = AddSource(BasePowerSourceType.NUCLEAR, simulator);
        WorldEntity vehicle = new(NitroxVector3.Zero, NitroxQuaternion.Identity, NitroxVector3.One, new NitroxTechType("Seamoth"), 0, "seamoth-class-id", false, new NitroxId(), null);
        CyclopsMetadata vehicleState = new(false, false, false, false, 0, 100f, false);
        vehicle.Metadata = vehicleState;

        Assert.IsFalse(manager.TryApplyTemperature(locker, new ThermalPlantMetadata(50f)));
        Assert.IsFalse(manager.TryApplyTemperature(panel, new ThermalPlantMetadata(50f)));
        Assert.IsFalse(manager.TryApplyTemperature(reactor, new ThermalPlantMetadata(50f)));
        Assert.IsFalse(manager.TryApplyTemperature(vehicle, new ThermalPlantMetadata(50f)));

        Assert.IsNull(locker.Metadata);
        Assert.AreSame(panelState, panel.Metadata);
        Assert.IsNull(reactor.Metadata);
        Assert.AreSame(vehicleState, vehicle.Metadata);
    }

    [TestMethod]
    public void TemperatureReportForAThermalPlantThatHasNotReportedYetIsStored()
    {
        Entity plant = AddSource(BasePowerSourceType.THERMAL, simulator);

        Assert.IsTrue(manager.TryApplyTemperature(plant, new ThermalPlantMetadata(50f)));

        Assert.AreEqual(50f, ((ThermalPlantMetadata)plant.Metadata).Temperature);
    }

    [TestMethod]
    public void AcceptedAndRefusedStatesAreRecordedForTheSource()
    {
        // The basepower command reads from these records who reported a source last and what was refused for it.
        Entity panel = AddSource(BasePowerSourceType.SOLAR, simulator);

        manager.AcceptState(simulator, panel, new PowerSourceMetadata(40f, 0f, BasePowerSourceType.SOLAR));
        manager.AcceptState(otherPlayer, panel, new PowerSourceMetadata(70f, 0f, BasePowerSourceType.SOLAR));

        BasePowerSourceDiagnostics recorded = diagnostics.GetSources()[panel.Id];
        Assert.AreEqual(simulator.SessionId, recorded.LastOwner);
        Assert.AreEqual(1L, recorded.AcceptedUpdates);
        Assert.AreEqual(1L, recorded.RejectedUpdates);
        Assert.AreEqual("sender_does_not_own_source_simulation", recorded.LastRejection);
    }

    [TestMethod]
    public void RefusedStateForASourceWithoutStoredStateIsRecordedAsThatKindOfSource()
    {
        Entity panel = AddSource(BasePowerSourceType.SOLAR, simulator);

        manager.AcceptState(otherPlayer, panel, new PowerSourceMetadata(70f, 0f, BasePowerSourceType.SOLAR));

        Assert.AreEqual(1L, diagnostics.GetSources()[panel.Id].RejectedUpdates);
        Assert.IsNull(panel.Metadata);
    }

    [TestMethod]
    public void StateForASourceTheServerDoesNotHaveIsCountedButNotKept()
    {
        manager.RejectUnknownSource(otherPlayer, new NitroxId());

        Assert.AreEqual(1L, diagnostics.RejectedUpdates);
        Assert.AreEqual(0, diagnostics.GetSources().Count, "A client can make up as many ids as it likes.");
    }

    [TestMethod]
    public void ConsumptionIsRecordedWithWhatBecameOfIt()
    {
        Entity reactor = AddSource(BasePowerSourceType.NUCLEAR, simulator);
        reactor.Metadata = new PowerSourceMetadata(900f, 0f, BasePowerSourceType.NUCLEAR);
        Entity plant = AddSource(BasePowerSourceType.THERMAL, null);
        plant.Metadata = new PowerSourceMetadata(30f, 0f, BasePowerSourceType.THERMAL);

        manager.RouteConsumption(otherPlayer, reactor, 12.5f, out _, out _);
        manager.RouteConsumption(otherPlayer, plant, 10f, out _, out _);
        manager.RouteConsumption(otherPlayer, plant, float.NaN, out _, out _);

        BasePowerDiagnosticTotals totals = diagnostics.GetTotals();
        Assert.AreEqual(1L, totals.ConsumptionForwarded);
        Assert.AreEqual(12.5f, totals.ConsumptionForwardedAmount);
        Assert.AreEqual(1L, totals.ConsumptionAppliedByServer);
        Assert.AreEqual(10f, totals.ConsumptionAppliedByServerAmount);
        Assert.AreEqual(1L, totals.ConsumptionRejected);
        Assert.AreEqual(1L, diagnostics.RejectedUpdates);
    }

    [TestMethod]
    public void SourcesAreListedWithTheirSimulatingPlayer()
    {
        Entity simulated = AddSource(BasePowerSourceType.SOLAR, simulator);
        simulated.Metadata = new PowerSourceMetadata(30f, 0f, BasePowerSourceType.SOLAR);
        Entity unsimulated = AddSource(BasePowerSourceType.NUCLEAR, null);
        unsimulated.Metadata = new PowerSourceMetadata(900f, 0f, BasePowerSourceType.NUCLEAR);
        AddSource(BasePowerSourceType.THERMAL, simulator);

        List<BasePowerManager.SourceSnapshot> sources = manager.GetSources();

        Assert.AreEqual(2, sources.Count, "A piece without power state is not listed.");
        Assert.AreSame(simulator, sources.Single(source => source.Source == simulated).Simulator);
        Assert.IsNull(sources.Single(source => source.Source == unsimulated).Simulator);
        Assert.AreEqual(900f, sources.Single(source => source.Source == unsimulated).State.Power);
    }

    private Entity AddSource(BasePowerSourceType sourceType, Player simulatedBy)
    {
        Entity source;
        if (sourceType is BasePowerSourceType.SOLAR or BasePowerSourceType.THERMAL)
        {
            source = ModuleEntity.MakeEmpty();
        }
        else
        {
            source = InteriorPieceEntity.MakeEmpty();
        }
        source.TechType = new NitroxTechType(sourceType switch
        {
            BasePowerSourceType.SOLAR => "SolarPanel",
            BasePowerSourceType.THERMAL => "ThermalPlant",
            BasePowerSourceType.BIOREACTOR => "BaseBioReactor",
            _ => "BaseNuclearReactor"
        });
        return Add(source, simulatedBy);
    }

    private Entity Add(Entity entity, Player simulatedBy)
    {
        entity.Id = new NitroxId();
        entityRegistry.AddEntity(entity);
        if (simulatedBy != null)
        {
            Assert.IsTrue(simulationOwnershipData.TryAcquire(entity.Id, simulatedBy, SimulationLockType.TRANSIENT));
        }
        return entity;
    }

    private Entity AddLocker(Player simulatedBy)
    {
        ModuleEntity locker = ModuleEntity.MakeEmpty();
        locker.TechType = new NitroxTechType("Locker");
        return Add(locker, simulatedBy);
    }

    private static Player CreatePlayer(ushort sessionId)
    {
        return new Player(sessionId, sessionId, $"Player{sessionId}", false, null, NitroxVector3.Zero, NitroxQuaternion.Identity, new NitroxId(), Optional.Empty, Perms.PLAYER,
                          new PlayerStatsData(100, 100, 100, 100, 100, 0), SubnauticaGameMode.SURVIVAL, [], [], new Dictionary<string, NitroxId>(), new Dictionary<string, float>(),
                          new Dictionary<string, PingInstancePreference>(), [], false, true);
    }
}
