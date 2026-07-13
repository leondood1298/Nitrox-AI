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

    [DataTestMethod]
    [DataRow(BasePowerSourceType.SOLAR, 75f)]
    [DataRow(BasePowerSourceType.THERMAL, 250f)]
    [DataRow(BasePowerSourceType.BIOREACTOR, 500f)]
    [DataRow(BasePowerSourceType.NUCLEAR, 2500f)]
    public void UsesCanonicalCapacityAndIncrementsPersistentRevision(BasePowerSourceType sourceType, float expectedCapacity)
    {
        BasePowerSourceAuthority authority = new();
        var entity = CreateCompatibleEntity(sourceType);
        entity.Metadata = new PowerSourceMetadata(1f, expectedCapacity, sourceType, 8);

        Assert.IsTrue(authority.TryApply(entity, firstOwner, Request(entity.Id, sourceType, expectedCapacity / 2, 1), out PowerSourceMetadata accepted, out _));

        Assert.AreEqual(expectedCapacity / 2, accepted.Power);
        Assert.AreEqual(expectedCapacity, accepted.MaxPower);
        Assert.AreEqual(9, accepted.Revision);
        Assert.AreSame(accepted, entity.Metadata);
    }

    [TestMethod]
    public void RejectsReplayFromSameOwnerWithoutMutation()
    {
        BasePowerSourceAuthority authority = new();
        ModuleEntity source = CreateModule();
        Assert.IsTrue(authority.TryApply(source, firstOwner, Request(source.Id, BasePowerSourceType.SOLAR, 50f, 4), out PowerSourceMetadata accepted, out _));

        Assert.IsFalse(authority.TryApply(source, firstOwner, Request(source.Id, BasePowerSourceType.SOLAR, 10f, 4), out PowerSourceMetadata replayState, out string reason));

        Assert.AreSame(accepted, replayState);
        Assert.AreEqual(50f, ((PowerSourceMetadata)source.Metadata).Power);
        StringAssert.Contains(reason, "stale client sequence");
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

    [DataTestMethod]
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

	[DataTestMethod]
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

	[DataTestMethod]
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
