using Nitrox.Model.DataStructures;

namespace NitroxClient.GameLogic;

/// <summary>
///     The player simulating a source tells everyone its state. These cover when that is worth a packet: a base can
///     have dozens of sources, and each packet goes to every other player.
/// </summary>
[TestClass]
public sealed class BasePowerBroadcasterTest
{
    private const float NUCLEAR_CAPACITY = 2500f;

    [TestMethod]
    public void FirstStateOfASourceIsAlwaysSent()
    {
        Assert.IsTrue(BasePowerBroadcaster.ShouldBroadcast(null, now: 5f, power: 0f, NUCLEAR_CAPACITY, fuelConsumed: 0f));
    }

    [TestMethod]
    public void NothingIsSentTwiceWithinASecond()
    {
        BasePowerState.Broadcast last = new(Time: 100f, Power: 1000f, FuelConsumed: 0f);

        Assert.IsFalse(BasePowerBroadcaster.ShouldBroadcast(last, now: 100.9f, power: 0f, NUCLEAR_CAPACITY, fuelConsumed: 0f));
    }

    [TestMethod]
    public void ChargingReactorIsNotSentEverySecond()
    {
        // A nuclear reactor makes about 4 power a second. One percent of its capacity is 25.
        BasePowerState.Broadcast last = new(Time: 100f, Power: 1000f, FuelConsumed: 500f);

        Assert.IsFalse(BasePowerBroadcaster.ShouldBroadcast(last, now: 102f, power: 1008f, NUCLEAR_CAPACITY, fuelConsumed: 508f));
        Assert.IsTrue(BasePowerBroadcaster.ShouldBroadcast(last, now: 106f, power: 1025f, NUCLEAR_CAPACITY, fuelConsumed: 525f));
    }

    [TestMethod]
    public void DrawOfAFabricatorOnASolarPanelIsSentAtOnce()
    {
        BasePowerState.Broadcast last = new(Time: 100f, Power: 40f, FuelConsumed: 0f);

        Assert.IsTrue(BasePowerBroadcaster.ShouldBroadcast(last, now: 101f, power: 35f, maxPower: 75f, fuelConsumed: 0f));
    }

    [TestMethod]
    public void SmallChangeStillArrivesWithTheHeartbeat()
    {
        BasePowerState.Broadcast last = new(Time: 100f, Power: 1000f, FuelConsumed: 0f);

        Assert.IsFalse(BasePowerBroadcaster.ShouldBroadcast(last, now: 129f, power: 1003f, NUCLEAR_CAPACITY, fuelConsumed: 0f));
        Assert.IsTrue(BasePowerBroadcaster.ShouldBroadcast(last, now: 130f, power: 1003f, NUCLEAR_CAPACITY, fuelConsumed: 0f));
        Assert.IsTrue(BasePowerBroadcaster.ShouldBroadcast(last, now: 130f, power: 1000f, NUCLEAR_CAPACITY, fuelConsumed: 0f), "Also when nothing changed.");
    }

    [TestMethod]
    [DataRow(0.4f, 0f)]
    [DataRow(0f, 0.4f)]
    public void RunningEmptyAndGettingPowerBackAreNeverHeldBack(float lastPower, float power)
    {
        // Whether the base has power at all hangs on this, however small the step.
        BasePowerState.Broadcast last = new(Time: 100f, lastPower, FuelConsumed: 0f);

        Assert.IsTrue(BasePowerBroadcaster.ShouldBroadcast(last, now: 101f, power, NUCLEAR_CAPACITY, fuelConsumed: 0f));
    }

    [TestMethod]
    [DataRow(2499.5f, 2500f)]
    [DataRow(2500f, 2499.5f)]
    public void FillingUpAndLeavingFullAreNeverHeldBack(float lastPower, float power)
    {
        BasePowerState.Broadcast last = new(Time: 100f, lastPower, FuelConsumed: 0f);

        Assert.IsTrue(BasePowerBroadcaster.ShouldBroadcast(last, now: 101f, power, NUCLEAR_CAPACITY, fuelConsumed: 0f));
    }

    [TestMethod]
    public void FuelProgressAloneIsSentWhenItMovedEnough()
    {
        // A full reactor that is drawn from as fast as it generates keeps its level while its fuel is used up.
        BasePowerState.Broadcast last = new(Time: 100f, Power: 1000f, FuelConsumed: 500f);

        Assert.IsFalse(BasePowerBroadcaster.ShouldBroadcast(last, now: 102f, power: 1000f, NUCLEAR_CAPACITY, fuelConsumed: 508f));
        Assert.IsTrue(BasePowerBroadcaster.ShouldBroadcast(last, now: 106f, power: 1000f, NUCLEAR_CAPACITY, fuelConsumed: 525f));
        Assert.IsTrue(BasePowerBroadcaster.ShouldBroadcast(last, now: 106f, power: 1000f, NUCLEAR_CAPACITY, fuelConsumed: 0f), "A used up rod resets the progress.");
    }

    [TestMethod]
    public void WhatWasSentIsRememberedPerSourceUntilItIsDestroyed()
    {
        BasePowerState state = new();
        NitroxId panel = new();
        NitroxId reactor = new();
        Assert.IsNull(state.GetLastBroadcast(panel));

        state.RecordBroadcast(panel, new BasePowerState.Broadcast(100f, 40f, 0f));

        Assert.AreEqual(40f, state.GetLastBroadcast(panel)!.Value.Power);
        Assert.IsNull(state.GetLastBroadcast(reactor));

        state.Forget(panel);
        Assert.IsNull(state.GetLastBroadcast(panel));
    }
}
