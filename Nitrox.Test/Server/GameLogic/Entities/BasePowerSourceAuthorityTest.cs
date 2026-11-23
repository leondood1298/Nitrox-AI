using Nitrox.Model.Core;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Bases;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;
using Nitrox.Model.Subnautica.Packets;

namespace Nitrox.Server.Subnautica.Models.GameLogic.Entities;

[TestClass]
public sealed class BasePowerSourceAuthorityTest
{
    private readonly SessionId firstOwner = (ushort)1;
    private readonly SessionId secondOwner = (ushort)2;

    [TestMethod]
    [DataRow(BasePowerSourceType.SOLAR, 75f)]
    [DataRow(BasePowerSourceType.THERMAL, 250f)]
    [DataRow(BasePowerSourceType.BIOREACTOR, 500f)]
    [DataRow(BasePowerSourceType.NUCLEAR, 2500f)]
    public void UsesCanonicalCapacityAndIncrementsPersistentRevision(BasePowerSourceType sourceType, float expectedCapacity)
    {
        BasePowerSourceAuthority authority = new();
        Entity entity = CreateCompatibleEntity(sourceType);
        entity.Metadata = new PowerSourceMetadata(1f, expectedCapacity, sourceType, 8);

        Assert.IsTrue(authority.TryApply(entity, firstOwner, Request(entity.Id, sourceType, expectedCapacity / 2, 1), out PowerSourceMetadata accepted, out _));

        Assert.AreEqual(expectedCapacity / 2, accepted.Power);
        Assert.AreEqual(expectedCapacity, accepted.MaxPower);
        Assert.AreEqual(9, accepted.Revision);
        Assert.AreSame(accepted, entity.Metadata);
    }

    [TestMethod]
    public void RevisionKeepsRisingWhenAReactorIsBuiltAgainUnderTheSameId()
    {
        // Deconstructing a reactor to a ghost and finishing it again keeps its id, but the rebuilt piece arrives as
        // a new entity with fresh state. Players who saw revision 41 would ignore everything below it.
        BasePowerSourceAuthority authority = new();
        InteriorPieceEntity reactor = CreateInterior();
        reactor.Metadata = new PowerSourceMetadata(900f, 2500f, BasePowerSourceType.NUCLEAR, 40);
        Assert.IsTrue(authority.TryApply(reactor, firstOwner, Request(reactor.Id, BasePowerSourceType.NUCLEAR, 910f, 1), out PowerSourceMetadata beforeRebuild, out _));
        Assert.AreEqual(41, beforeRebuild.Revision);

        InteriorPieceEntity rebuilt = InteriorPieceEntity.MakeEmpty();
        rebuilt.Id = reactor.Id;
        rebuilt.Metadata = new PowerSourceMetadata(910f, 2500f, BasePowerSourceType.NUCLEAR);
        Assert.IsTrue(authority.TryApply(rebuilt, firstOwner, Request(rebuilt.Id, BasePowerSourceType.NUCLEAR, 915f, 2), out PowerSourceMetadata afterRebuild, out _));

        Assert.AreEqual(42, afterRebuild.Revision);
    }

    [TestMethod]
    public void RevisionKeepsRisingWhenTheServerAppliesConsumptionAfterARebuild()
    {
        BasePowerSourceAuthority authority = new();
        ModuleEntity panel = CreateModule();
        panel.Metadata = new PowerSourceMetadata(30f, 75f, BasePowerSourceType.SOLAR, 12);
        Assert.AreEqual(ConsumptionRoute.APPLIED_BY_SERVER, authority.RouteConsumption(panel, null, secondOwner, 5f, out PowerSourceMetadata first, out _));
        Assert.AreEqual(13, first.Revision);

        panel.Metadata = new PowerSourceMetadata(25f, 75f, BasePowerSourceType.SOLAR);
        Assert.AreEqual(ConsumptionRoute.APPLIED_BY_SERVER, authority.RouteConsumption(panel, null, secondOwner, 5f, out PowerSourceMetadata second, out _));

        Assert.AreEqual(14, second.Revision);
    }

    [TestMethod]
    public void RejoinedPlayerOnReusedSessionIdCanRestartClientSequence()
    {
        // A client restarts its counter at 1 on every join, and the server hands the same session id out again
        // after the reuse delay. The returning player's updates must not be treated as stale.
        BasePowerSourceAuthority authority = new();
        ModuleEntity source = CreateModule();
        Assert.IsTrue(authority.TryApply(source, firstOwner, Request(source.Id, BasePowerSourceType.SOLAR, 50f, 3600), out _, out _));

        Assert.IsTrue(authority.TryApply(source, firstOwner, Request(source.Id, BasePowerSourceType.SOLAR, 10f, 1), out PowerSourceMetadata accepted, out string reason), reason);

        Assert.AreEqual(10f, accepted.Power);
        Assert.AreEqual(2, accepted.Revision);
        Assert.AreEqual(10f, ((PowerSourceMetadata)source.Metadata).Power);
    }

    [TestMethod]
    public void NewOwnerCanRestartClientSequence()
    {
        BasePowerSourceAuthority authority = new();
        ModuleEntity source = CreateModule();
        Assert.IsTrue(authority.TryApply(source, firstOwner, Request(source.Id, BasePowerSourceType.THERMAL, 200f, 20), out _, out _));

        Assert.IsTrue(authority.TryApply(source, secondOwner, Request(source.Id, BasePowerSourceType.THERMAL, 150f, 1), out PowerSourceMetadata accepted, out _));

        Assert.AreEqual(150f, accepted.Power);
        Assert.AreEqual(1, authority.GetLastClientSequence(source.Id));
    }

    [TestMethod]
    [DataRow(float.NaN)]
    [DataRow(float.PositiveInfinity)]
    [DataRow(-1f)]
    [DataRow(76f)]
    public void RejectsInvalidSolarPower(float power)
    {
        BasePowerSourceAuthority authority = new();
        ModuleEntity source = CreateModule();

        Assert.IsFalse(authority.TryApply(source, firstOwner, Request(source.Id, BasePowerSourceType.SOLAR, power, 1), out _, out string reason));

        StringAssert.Contains(reason, "outside");
        Assert.IsNull(source.Metadata);
    }

    [TestMethod]
    public void ToleranceClampsSmallFloatingPointOvershoot()
    {
        BasePowerSourceAuthority authority = new();
        ModuleEntity source = CreateModule();

        Assert.IsTrue(authority.TryApply(source, firstOwner, Request(source.Id, BasePowerSourceType.SOLAR, 75.005f, 1), out PowerSourceMetadata accepted, out _));

        Assert.AreEqual(75f, accepted.Power);
    }

    [TestMethod]
    [DataRow(BasePowerSourceType.BIOREACTOR, 420f)]
    [DataRow(BasePowerSourceType.NUCLEAR, 12345f)]
    public void AcceptsAndPersistsReactorFuelProgress(BasePowerSourceType sourceType, float fuelConsumed)
    {
        BasePowerSourceAuthority authority = new();
        InteriorPieceEntity source = CreateInterior();

        Assert.IsTrue(authority.TryApply(source, firstOwner, Request(source.Id, sourceType, 100f, 1, fuelConsumed), out PowerSourceMetadata accepted, out _));

        Assert.AreEqual(fuelConsumed, accepted.FuelConsumed);
        Assert.AreEqual(fuelConsumed, ((PowerSourceMetadata)source.Metadata).FuelConsumed);
    }

    [TestMethod]
    [DataRow(BasePowerSourceType.SOLAR, 1f)]
    [DataRow(BasePowerSourceType.BIOREACTOR, 841f)]
    [DataRow(BasePowerSourceType.NUCLEAR, 20001f)]
    [DataRow(BasePowerSourceType.NUCLEAR, -1f)]
    [DataRow(BasePowerSourceType.NUCLEAR, float.NaN)]
    public void RejectsInvalidFuelProgress(BasePowerSourceType sourceType, float fuelConsumed)
    {
        BasePowerSourceAuthority authority = new();
        Entity source = CreateCompatibleEntity(sourceType);

        Assert.IsFalse(authority.TryApply(source, firstOwner, Request(source.Id, sourceType, 10f, 1, fuelConsumed), out _, out string reason));

        StringAssert.Contains(reason, "fuel progress");
    }

    [TestMethod]
    public void RejectsIncompatibleEntityKindsAndSourceTypeChanges()
    {
        BasePowerSourceAuthority authority = new();
        ModuleEntity exteriorModule = CreateModule();
        InteriorPieceEntity interiorPiece = CreateInterior();
        interiorPiece.Metadata = new PowerSourceMetadata(400f, 500f, BasePowerSourceType.BIOREACTOR, 3);

        Assert.IsFalse(authority.TryApply(exteriorModule, firstOwner, Request(exteriorModule.Id, BasePowerSourceType.NUCLEAR, 10f, 1), out _, out string incompatible));
        Assert.IsFalse(authority.TryApply(interiorPiece, firstOwner, Request(interiorPiece.Id, BasePowerSourceType.NUCLEAR, 10f, 1), out _, out string changed));
        StringAssert.Contains(incompatible, "incompatible");
        StringAssert.Contains(changed, "changed");
    }

    [TestMethod]
    public void RejectsMismatchedPersistedTechTypeButAllowsLegacyNone()
    {
        BasePowerSourceAuthority authority = new();
        ModuleEntity mismatched = CreateModule();
        mismatched.TechType = new("ThermalPlant");
        ModuleEntity legacy = CreateModule();
        legacy.TechType = NitroxTechType.None;

        Assert.IsFalse(authority.TryApply(mismatched, firstOwner, Request(mismatched.Id, BasePowerSourceType.SOLAR, 20f, 1), out _, out string reason));
        Assert.IsTrue(authority.TryApply(legacy, firstOwner, Request(legacy.Id, BasePowerSourceType.SOLAR, 20f, 1), out _, out _));
        StringAssert.Contains(reason, "tech type");
    }

    [TestMethod]
    public void RejectsServerResponsesUnknownKindsAndNonPositiveSequences()
    {
        BasePowerSourceAuthority authority = new();
        ModuleEntity source = CreateModule();

        Assert.IsFalse(authority.TryApply(source, firstOwner, new BasePowerSourceUpdate(source.Id, BasePowerSourceType.SOLAR, 20f, 1, isServerResponse: true), out _, out _));
        Assert.IsFalse(authority.TryApply(source, firstOwner, Request(source.Id, BasePowerSourceType.UNKNOWN, 0f, 1), out _, out _));
        Assert.IsFalse(authority.TryApply(source, firstOwner, Request(source.Id, BasePowerSourceType.SOLAR, 20f, 0), out _, out _));
    }

    [TestMethod]
    public void TemperatureReportIsKeptAlongsidePowerStateWithoutResettingRevision()
    {
        // A thermal plant reports its temperature through generic metadata while its power state lives in the same
        // slot. Replacing that state would restart the revision clients order power updates by.
        BasePowerSourceAuthority authority = new();
        ModuleEntity plant = CreateModule();
        plant.Metadata = new PowerSourceMetadata(120f, 250f, BasePowerSourceType.THERMAL, 500);

        Assert.IsTrue(authority.TryApplyTemperature(plant, new ThermalPlantMetadata(63f)));

        PowerSourceMetadata afterReport = (PowerSourceMetadata)plant.Metadata;
        Assert.AreEqual(63f, afterReport.Temperature);
        Assert.AreEqual(120f, afterReport.Power);
        Assert.AreEqual(500, afterReport.Revision);

        Assert.IsTrue(authority.TryApply(plant, firstOwner, Request(plant.Id, BasePowerSourceType.THERMAL, 121f, 1), out PowerSourceMetadata afterPower, out string reason), reason);
        Assert.AreEqual(501, afterPower.Revision);
        Assert.AreEqual(63f, afterPower.Temperature, "A power update must not drop the stored temperature.");
    }

    [TestMethod]
    public void TemperatureOnlyRisesAndNeedsExistingPowerState()
    {
        BasePowerSourceAuthority authority = new();
        ModuleEntity plant = CreateModule();
        Assert.IsFalse(authority.TryApplyTemperature(plant, new ThermalPlantMetadata(40f)), "Without power state the report is stored as ordinary metadata instead.");

        plant.Metadata = new PowerSourceMetadata(10f, 250f, BasePowerSourceType.THERMAL, 3, temperature: 70f);
        Assert.IsFalse(authority.TryApplyTemperature(plant, new ThermalPlantMetadata(70f)));
        Assert.IsFalse(authority.TryApplyTemperature(plant, new ThermalPlantMetadata(55f)));
        Assert.IsFalse(authority.TryApplyTemperature(plant, new ThermalPlantMetadata(float.NaN)));
        Assert.AreEqual(70f, ((PowerSourceMetadata)plant.Metadata).Temperature);
    }

    [TestMethod]
    public void TemperatureIsStoredNoHigherThanAThermalPlantCanUse()
    {
        // Any player may report a temperature. Above 100 a plant's output and gauge no longer change.
        BasePowerSourceAuthority authority = new();
        ModuleEntity plant = CreateModule();
        plant.Metadata = new PowerSourceMetadata(10f, 250f, BasePowerSourceType.THERMAL, 3, temperature: 70f);

        Assert.IsTrue(authority.TryAcceptTemperature(plant, new ThermalPlantMetadata(1e9f), out ThermalPlantMetadata accepted));

        Assert.AreEqual(100f, accepted.Temperature, "What is relayed to the other players is limited as well.");
        Assert.AreEqual(100f, ((PowerSourceMetadata)plant.Metadata).Temperature);
        Assert.IsFalse(authority.TryAcceptTemperature(plant, new ThermalPlantMetadata(5e9f), out _), "Nothing above the limit is news.");
    }

    [TestMethod]
    public void TemperatureForAPlantWithoutPowerStateIsCheckedTheSameWay()
    {
        BasePowerSourceAuthority authority = new();
        ModuleEntity plant = CreateModule();

        Assert.IsTrue(authority.TryAcceptTemperature(plant, new ThermalPlantMetadata(250f), out ThermalPlantMetadata accepted));
        Assert.AreEqual(100f, accepted.Temperature);
        Assert.IsNull(plant.Metadata, "The caller stores the accepted report.");
        Assert.IsFalse(authority.TryAcceptTemperature(plant, new ThermalPlantMetadata(float.NaN), out _));
        Assert.IsFalse(authority.TryAcceptTemperature(plant, new ThermalPlantMetadata(float.PositiveInfinity), out _));

        plant.Metadata = new ThermalPlantMetadata(48f);
        Assert.IsFalse(authority.TryAcceptTemperature(plant, new ThermalPlantMetadata(48f), out _));
        Assert.IsTrue(authority.TryAcceptTemperature(plant, new ThermalPlantMetadata(49f), out _));
    }

    [TestMethod]
    public void OnlyTheSimulatingPlayerOrATemperatureReportMayTouchPowerState()
    {
        Assert.IsFalse(BasePowerSourceAuthority.MayReplacePowerState(new BatteryMetadata(100f), senderSimulatesSource: false));
        Assert.IsTrue(BasePowerSourceAuthority.MayReplacePowerState(new BatteryMetadata(100f), senderSimulatesSource: true));
        Assert.IsTrue(BasePowerSourceAuthority.MayReplacePowerState(new ThermalPlantMetadata(60f), senderSimulatesSource: false));
    }

    [TestMethod]
    public void FirstPowerUpdateCarriesOverTemperatureStoredAsPlainThermalMetadata()
    {
        // A newly built plant can get its temperature report before its first power update.
        BasePowerSourceAuthority authority = new();
        ModuleEntity plant = CreateModule();
        plant.Metadata = new ThermalPlantMetadata(48f);

        Assert.IsTrue(authority.TryApply(plant, firstOwner, Request(plant.Id, BasePowerSourceType.THERMAL, 5f, 1), out PowerSourceMetadata accepted, out string reason), reason);

        Assert.AreEqual(48f, accepted.Temperature);
        Assert.AreEqual(1, accepted.Revision);
    }

    [TestMethod]
    public void ConsumptionByAnotherPlayerGoesToTheSourceOwnerAndLeavesStoredStateAlone()
    {
        // The owner simulates the level. Changing the stored copy here would be overwritten by its next broadcast.
        BasePowerSourceAuthority authority = new();
        InteriorPieceEntity reactor = CreateInterior();
        reactor.Metadata = new PowerSourceMetadata(900f, 2500f, BasePowerSourceType.NUCLEAR, 40);

        ConsumptionRoute route = authority.RouteConsumption(reactor, firstOwner, secondOwner, 12.5f, out _, out string reason);

        Assert.AreEqual(ConsumptionRoute.FORWARD_TO_OWNER, route, reason);
        Assert.AreEqual(900f, ((PowerSourceMetadata)reactor.Metadata).Power);
        Assert.AreEqual(40, ((PowerSourceMetadata)reactor.Metadata).Revision);
    }

    [TestMethod]
    public void ConsumptionReportedByTheOwnerItselfIsRejected()
    {
        BasePowerSourceAuthority authority = new();
        InteriorPieceEntity reactor = CreateInterior();
        reactor.Metadata = new PowerSourceMetadata(900f, 2500f, BasePowerSourceType.NUCLEAR, 40);

        Assert.AreEqual(ConsumptionRoute.REJECTED, authority.RouteConsumption(reactor, firstOwner, firstOwner, 5f, out _, out string reason));
        StringAssert.Contains(reason, "simulates");
    }

    [TestMethod]
    public void ConsumptionWithNoSimulationOwnerIsAppliedToStoredState()
    {
        BasePowerSourceAuthority authority = new();
        ModuleEntity plant = CreateModule();
        plant.Metadata = new PowerSourceMetadata(30f, 250f, BasePowerSourceType.THERMAL, 7, temperature: 61f);

        Assert.AreEqual(ConsumptionRoute.APPLIED_BY_SERVER, authority.RouteConsumption(plant, null, secondOwner, 10f, out PowerSourceMetadata accepted, out _));
        Assert.AreEqual(20f, accepted.Power);
        Assert.AreEqual(8, accepted.Revision);
        Assert.AreEqual(61f, accepted.Temperature);
        Assert.AreSame(accepted, plant.Metadata);

        Assert.AreEqual(ConsumptionRoute.APPLIED_BY_SERVER, authority.RouteConsumption(plant, null, secondOwner, 500f, out accepted, out _));
        Assert.AreEqual(0f, accepted.Power, "A source cannot go below empty.");
    }

    [TestMethod]
    [DataRow(0f)]
    [DataRow(-3f)]
    [DataRow(float.NaN)]
    [DataRow(float.PositiveInfinity)]
    [DataRow(2500.5f)]
    public void InvalidConsumptionAmountsAreRejected(float amount)
    {
        BasePowerSourceAuthority authority = new();
        InteriorPieceEntity reactor = CreateInterior();
        reactor.Metadata = new PowerSourceMetadata(900f, 2500f, BasePowerSourceType.NUCLEAR, 40);

        Assert.AreEqual(ConsumptionRoute.REJECTED, authority.RouteConsumption(reactor, firstOwner, secondOwner, amount, out _, out string reason));
        StringAssert.Contains(reason, "amount");
        Assert.AreEqual(900f, ((PowerSourceMetadata)reactor.Metadata).Power);
    }

    [TestMethod]
    public void ConsumptionFromSourceWithoutOwnerOrStoredStateIsRejected()
    {
        BasePowerSourceAuthority authority = new();

        Assert.AreEqual(ConsumptionRoute.REJECTED, authority.RouteConsumption(CreateModule(), null, secondOwner, 5f, out _, out string reason));
        StringAssert.Contains(reason, "no stored power state");
    }

    private static BasePowerSourceUpdate Request(NitroxId id, BasePowerSourceType sourceType, float power, long sequence, float fuelConsumed = 0f) => new(id, sourceType, power, sequence, fuelConsumed: fuelConsumed);

    private static Entity CreateCompatibleEntity(BasePowerSourceType sourceType) => sourceType is BasePowerSourceType.SOLAR or BasePowerSourceType.THERMAL ? CreateModule() : CreateInterior();

    private static ModuleEntity CreateModule()
    {
        ModuleEntity entity = ModuleEntity.MakeEmpty();
        entity.Id = new NitroxId();
        return entity;
    }

    private static InteriorPieceEntity CreateInterior()
    {
        InteriorPieceEntity entity = InteriorPieceEntity.MakeEmpty();
        entity.Id = new NitroxId();
        return entity;
    }
}
