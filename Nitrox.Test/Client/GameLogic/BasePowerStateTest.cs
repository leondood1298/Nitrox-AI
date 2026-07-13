using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;
using Nitrox.Model.Subnautica.Packets;

namespace NitroxClient.GameLogic;

[TestClass]
public sealed class BasePowerStateTest
{
    [TestMethod]
    public void ClientSequencesAreMonotonicAndIndependentPerSource()
    {
        BasePowerState state = new();
        NitroxId first = new();
        NitroxId second = new();

        Assert.AreEqual(1, state.CreateUpdate(first, BasePowerSourceType.SOLAR, 1f).ClientSequence);
        Assert.AreEqual(2, state.CreateUpdate(first, BasePowerSourceType.SOLAR, 2f).ClientSequence);
        Assert.AreEqual(1, state.CreateUpdate(second, BasePowerSourceType.THERMAL, 3f).ClientSequence);
    }

	[TestMethod]
	public void ReactorUpdateIncludesPartialFuelProgress()
	{
		BasePowerState state = new();
		BasePowerSourceUpdate update = state.CreateUpdate(new NitroxId(), BasePowerSourceType.NUCLEAR, 100f, 1234.5f);

		Assert.AreEqual(1234.5f, update.FuelConsumed);
	}

    [TestMethod]
    public void StaleServerRevisionCannotReplaceNewerCanonicalState()
    {
        BasePowerState state = new();
        NitroxId id = new();
        PowerSourceMetadata newer = new(50f, 75f, BasePowerSourceType.SOLAR, 5);
        PowerSourceMetadata stale = new(10f, 75f, BasePowerSourceType.SOLAR, 4);

        Assert.IsTrue(state.TryApply(id, newer, out _));
        Assert.IsFalse(state.TryApply(id, stale, out PowerSourceMetadata retained));

        Assert.AreSame(newer, retained);
        Assert.IsTrue(state.TryGet(id, out PowerSourceMetadata current));
        Assert.AreSame(newer, current);
    }

    [TestMethod]
    public void EqualRevisionResponseCanRestoreCanonicalPowerAfterRejectedLocalChange()
    {
        BasePowerState state = new();
        NitroxId id = new();
        state.TryApply(id, new PowerSourceMetadata(50f, 75f, BasePowerSourceType.SOLAR, 5), out _);

        Assert.IsTrue(state.TryApply(id, new PowerSourceMetadata(45f, 75f, BasePowerSourceType.SOLAR, 5), out PowerSourceMetadata restored));

        Assert.AreEqual(45f, restored.Power);
    }
}
