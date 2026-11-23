using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;
using Nitrox.Model.Subnautica.Packets;
using NitroxClient.Communication.Abstract;
using NitroxClient.GameLogic;
using NSubstitute;

namespace Nitrox.Test.Client.GameLogic;

[TestClass]
public sealed class BasePowerSourcesTest
{
    private const float NUCLEAR_CAPACITY = 2500f;
    private const float SOLAR_CAPACITY = 75f;

    private readonly BasePowerSources sources;

    public BasePowerSourcesTest()
    {
        IPacketSender packetSender = Substitute.For<IPacketSender>();
        sources = new BasePowerSources(packetSender, new SimulationOwnership(Substitute.For<IMultiplayerSession>(), packetSender));
    }

    [TestMethod]
    public void SourceSimulatedByCreativePlayerStillGenerates()
    {
        // The Survival players of the base need what the reactor makes, whoever happens to simulate it.
        Assert.IsTrue(BasePowerSources.MustGenerateWithoutPowerRequirement(localModeRequiresPower: false, simulatedLocally: true, amount: 0.07f));
    }

    [TestMethod]
    public void GameHandlesEverythingForPlayersWhoRequirePower()
    {
        Assert.IsFalse(BasePowerSources.MustGenerateWithoutPowerRequirement(localModeRequiresPower: true, simulatedLocally: true, amount: 0.07f));
        Assert.IsFalse(BasePowerSources.MustGenerateWithoutPowerRequirement(localModeRequiresPower: true, simulatedLocally: false, amount: 0.07f));
    }

    [TestMethod]
    [DataRow(-5f)]
    [DataRow(0f)]
    public void CreativePlayersOwnDrawsStayFree(float amount)
    {
        // Nothing the Creative player's own game draws may cost power: that is what the mode means for them.
        Assert.IsFalse(BasePowerSources.MustGenerateWithoutPowerRequirement(localModeRequiresPower: false, simulatedLocally: true, amount));
    }

    [TestMethod]
    public void CreativePlayerDoesNotGenerateForSourcesSomeoneElseSimulates()
    {
        Assert.IsFalse(BasePowerSources.MustGenerateWithoutPowerRequirement(localModeRequiresPower: false, simulatedLocally: false, amount: 0.07f));
    }

    [TestMethod]
    public void SourceIsSimulatedByThePlayerHoldingItsLock()
    {
        Assert.IsTrue(BasePowerSources.CanSimulate(hasId: true, simulatedLocally: true, rebuildingBases: false));
        Assert.IsFalse(BasePowerSources.CanSimulate(hasId: true, simulatedLocally: false, rebuildingBases: false));
    }

    [TestMethod]
    public void SourceIsNotSimulatedWhileItsBaseIsBeingRebuilt()
    {
        // During a resync a nuclear reactor exists before its rods do. Run in that state it would wipe its fuel
        // progress and take the same amount off its stored power, and the simulating player would send both.
        Assert.IsFalse(BasePowerSources.CanSimulate(hasId: true, simulatedLocally: true, rebuildingBases: true));
    }

    [TestMethod]
    public void SourceWithoutAnIdBehavesAsInTheGame()
    {
        Assert.IsTrue(BasePowerSources.CanSimulate(hasId: false, simulatedLocally: false, rebuildingBases: false));
        Assert.IsTrue(BasePowerSources.CanSimulate(hasId: false, simulatedLocally: false, rebuildingBases: true));
    }

    [TestMethod]
    [DataRow(40f, 75f, 2f, 42f)]
    [DataRow(74.5f, 75f, 2f, 75f)]
    [DataRow(75f, 75f, 2f, 75f)]
    public void GeneratingFillsUpToCapacity(float power, float maxPower, float amount, float expected)
    {
        Assert.AreEqual(expected, BasePowerSources.LevelAfterGenerating(power, maxPower, amount));
    }

    [TestMethod]
    [DataRow(40f, 5f, 35f)]
    [DataRow(3f, 5f, 0f)]
    [DataRow(0f, 5f, 0f)]
    public void DrawByAnotherPlayerStopsAtEmpty(float power, float amount, float expected)
    {
        Assert.AreEqual(expected, BasePowerSources.LevelAfterDraw(power, amount));
    }

    [TestMethod]
    public void FirstStateOfASourceIsAlwaysSent()
    {
        Assert.IsTrue(BasePowerSources.ShouldBroadcast(null, now: 5f, power: 0f, NUCLEAR_CAPACITY, fuelConsumed: 0f));
    }

    [TestMethod]
    public void NothingIsSentTwiceWithinASecond()
    {
        BasePowerSources.Broadcast last = new(Time: 100f, Power: 1000f, FuelConsumed: 0f);

        Assert.IsFalse(BasePowerSources.ShouldBroadcast(last, now: 100.9f, power: 0f, NUCLEAR_CAPACITY, fuelConsumed: 0f));
    }

    [TestMethod]
    public void ChargingReactorIsNotSentEverySecond()
    {
        // A nuclear reactor makes about 4 power a second. One percent of its capacity is 25.
        BasePowerSources.Broadcast last = new(Time: 100f, Power: 1000f, FuelConsumed: 500f);

        Assert.IsFalse(BasePowerSources.ShouldBroadcast(last, now: 102f, power: 1008f, NUCLEAR_CAPACITY, fuelConsumed: 508f));
        Assert.IsTrue(BasePowerSources.ShouldBroadcast(last, now: 106f, power: 1025f, NUCLEAR_CAPACITY, fuelConsumed: 525f));
    }

    [TestMethod]
    public void DrawOfAFabricatorOnASolarPanelIsSentAtOnce()
    {
        BasePowerSources.Broadcast last = new(Time: 100f, Power: 40f, FuelConsumed: 0f);

        Assert.IsTrue(BasePowerSources.ShouldBroadcast(last, now: 101f, power: 35f, SOLAR_CAPACITY, fuelConsumed: 0f));
    }

    [TestMethod]
    public void SmallChangeStillArrivesWithTheHeartbeat()
    {
        BasePowerSources.Broadcast last = new(Time: 100f, Power: 1000f, FuelConsumed: 0f);

        Assert.IsFalse(BasePowerSources.ShouldBroadcast(last, now: 129f, power: 1003f, NUCLEAR_CAPACITY, fuelConsumed: 0f));
        Assert.IsTrue(BasePowerSources.ShouldBroadcast(last, now: 130f, power: 1003f, NUCLEAR_CAPACITY, fuelConsumed: 0f));
        Assert.IsTrue(BasePowerSources.ShouldBroadcast(last, now: 130f, power: 1000f, NUCLEAR_CAPACITY, fuelConsumed: 0f),
            "Also when nothing changed: it repairs a copy that drifted on another player's side.");
    }

    [TestMethod]
    [DataRow(0.4f, 0f)]
    [DataRow(0f, 0.4f)]
    public void RunningEmptyAndGettingPowerBackAreNeverHeldBack(float lastPower, float power)
    {
        // Whether the base has power at all hangs on this, however small the step.
        BasePowerSources.Broadcast last = new(Time: 100f, lastPower, FuelConsumed: 0f);

        Assert.IsTrue(BasePowerSources.ShouldBroadcast(last, now: 101f, power, NUCLEAR_CAPACITY, fuelConsumed: 0f));
    }

    [TestMethod]
    [DataRow(2499.5f, 2500f)]
    [DataRow(2500f, 2499.5f)]
    public void FillingUpAndLeavingFullAreNeverHeldBack(float lastPower, float power)
    {
        BasePowerSources.Broadcast last = new(Time: 100f, lastPower, FuelConsumed: 0f);

        Assert.IsTrue(BasePowerSources.ShouldBroadcast(last, now: 101f, power, NUCLEAR_CAPACITY, fuelConsumed: 0f));
    }

    [TestMethod]
    public void FuelProgressAloneIsSentWhenItMovedEnough()
    {
        // A full reactor that is drawn from as fast as it generates keeps its level while its fuel is used up.
        BasePowerSources.Broadcast last = new(Time: 100f, Power: 1000f, FuelConsumed: 500f);

        Assert.IsFalse(BasePowerSources.ShouldBroadcast(last, now: 102f, power: 1000f, NUCLEAR_CAPACITY, fuelConsumed: 508f));
        Assert.IsTrue(BasePowerSources.ShouldBroadcast(last, now: 106f, power: 1000f, NUCLEAR_CAPACITY, fuelConsumed: 525f));
        Assert.IsTrue(BasePowerSources.ShouldBroadcast(last, now: 106f, power: 1000f, NUCLEAR_CAPACITY, fuelConsumed: 0f), "A used up rod resets the progress.");
    }

    [TestMethod]
    public void StateIsSentAsEntityMetadataOfTheSource()
    {
        NitroxId reactor = new();
        PowerSourceMetadata state = new(900f, 4200f, BasePowerSourceType.NUCLEAR);

        Assert.IsTrue(sources.TryCreateUpdate(reactor, state, NUCLEAR_CAPACITY, now: 100f, out EntityMetadataUpdate update));

        Assert.AreEqual(reactor, update.Id);
        Assert.AreSame(state, update.NewValue);
    }

    [TestMethod]
    public void WhatWasSentIsRememberedPerSource()
    {
        NitroxId panel = new();
        NitroxId reactor = new();
        Assert.IsTrue(sources.TryCreateUpdate(panel, new PowerSourceMetadata(40f, 0f, BasePowerSourceType.SOLAR), SOLAR_CAPACITY, now: 100f, out _));

        Assert.IsFalse(sources.TryCreateUpdate(panel, new PowerSourceMetadata(40.2f, 0f, BasePowerSourceType.SOLAR), SOLAR_CAPACITY, now: 105f, out _),
            "Less than one percent since what was sent.");
        Assert.IsTrue(sources.TryCreateUpdate(panel, new PowerSourceMetadata(41f, 0f, BasePowerSourceType.SOLAR), SOLAR_CAPACITY, now: 106f, out _));
        Assert.IsFalse(sources.TryCreateUpdate(panel, new PowerSourceMetadata(41.5f, 0f, BasePowerSourceType.SOLAR), SOLAR_CAPACITY, now: 107f, out _),
            "Measured against the state that was sent last, not the one that was held back.");
        Assert.IsTrue(sources.TryCreateUpdate(reactor, new PowerSourceMetadata(900f, 0f, BasePowerSourceType.NUCLEAR), NUCLEAR_CAPACITY, now: 107f, out _),
            "Each source has its own.");
    }

    [TestMethod]
    public void SourceOfAnUnknownKindIsNotSent()
    {
        // The server could not check its level against a capacity.
        Assert.IsFalse(sources.TryCreateUpdate(new NitroxId(), new PowerSourceMetadata(40f, 0f, BasePowerSourceType.UNKNOWN), 10000f, now: 100f, out _));
    }

    [TestMethod]
    public void DrawFromAnotherPlayerIsAppliedToASourceThisClientSimulates()
    {
        Assert.AreEqual(ForwardedDraw.APPLY, sources.DecideForwardedDraw(new NitroxId(), simulatedLocally: true, sourceObjectExists: true, finishedLoading: true));
    }

    [TestMethod]
    public void DrawForASourceSomeoneElseSimulatesByNowIsIgnored()
    {
        Assert.AreEqual(ForwardedDraw.IGNORE, sources.DecideForwardedDraw(new NitroxId(), simulatedLocally: false, sourceObjectExists: true, finishedLoading: true));
        Assert.AreEqual(ForwardedDraw.IGNORE, sources.DecideForwardedDraw(new NitroxId(), simulatedLocally: false, sourceObjectExists: false, finishedLoading: true));
    }

    [TestMethod]
    public void SourceMissingOnThisClientIsHandedBackOnce()
    {
        // Otherwise it stays frozen for everyone. If it comes back, nobody else could simulate it either.
        NitroxId source = new();

        Assert.AreEqual(ForwardedDraw.HAND_BACK, sources.DecideForwardedDraw(source, simulatedLocally: true, sourceObjectExists: false, finishedLoading: true));
        Assert.AreEqual(ForwardedDraw.IGNORE, sources.DecideForwardedDraw(source, simulatedLocally: true, sourceObjectExists: false, finishedLoading: true));
        Assert.AreEqual(ForwardedDraw.HAND_BACK, sources.DecideForwardedDraw(new NitroxId(), simulatedLocally: true, sourceObjectExists: false, finishedLoading: true),
            "Each source is judged on its own.");
    }

    [TestMethod]
    public void SourceNotSpawnedYetWhileLoadingIsKept()
    {
        NitroxId source = new();

        Assert.AreEqual(ForwardedDraw.IGNORE, sources.DecideForwardedDraw(source, simulatedLocally: true, sourceObjectExists: false, finishedLoading: false));
        Assert.AreEqual(ForwardedDraw.HAND_BACK, sources.DecideForwardedDraw(source, simulatedLocally: true, sourceObjectExists: false, finishedLoading: true),
            "Still missing once loading is over: it will not come.");
    }

    [TestMethod]
    public void StateArrivingForASourceThisClientSimulatesIsTheAnswerToARefusedOne()
    {
        // What the server accepts from the simulating player goes to the others only.
        Assert.IsTrue(BasePowerSources.IsAnswerToRefusedState(simulatedLocally: true, finishedLoading: true));
        Assert.IsFalse(BasePowerSources.IsAnswerToRefusedState(simulatedLocally: false, finishedLoading: true), "The state another player simulates.");
        Assert.IsFalse(BasePowerSources.IsAnswerToRefusedState(simulatedLocally: true, finishedLoading: false), "The state a source spawns with.");
    }

    [TestMethod]
    public void DestroyedSourceIsForgotten()
    {
        // What was remembered for it must not hold back the first state of a later entity, or stop that entity from
        // being handed back.
        NitroxId source = new();
        Assert.IsTrue(sources.TryCreateUpdate(source, new PowerSourceMetadata(40f, 0f, BasePowerSourceType.SOLAR), SOLAR_CAPACITY, now: 100f, out _));
        Assert.AreEqual(ForwardedDraw.HAND_BACK, sources.DecideForwardedDraw(source, simulatedLocally: true, sourceObjectExists: false, finishedLoading: true));

        sources.Forget(source);

        Assert.IsTrue(sources.TryCreateUpdate(source, new PowerSourceMetadata(40f, 0f, BasePowerSourceType.SOLAR), SOLAR_CAPACITY, now: 100.5f, out _));
        Assert.AreEqual(ForwardedDraw.HAND_BACK, sources.DecideForwardedDraw(source, simulatedLocally: true, sourceObjectExists: false, finishedLoading: true));
    }

    [TestMethod]
    public void ASourceThatRegeneratesIsALifepodCell()
    {
        Assert.AreEqual(BasePowerSourceType.ESCAPE_POD, BasePowerSources.ClassifySource(false, false, false, false, regenerates: true));
        Assert.AreEqual(BasePowerSourceType.UNKNOWN, BasePowerSources.ClassifySource(false, false, false, false, regenerates: false));
        Assert.AreEqual(BasePowerSourceType.SOLAR, BasePowerSources.ClassifySource(true, false, false, false, regenerates: false));
        Assert.AreEqual(BasePowerSourceType.NUCLEAR, BasePowerSources.ClassifySource(false, false, false, true, regenerates: false));
    }

    [TestMethod]
    public void ALifepodCellRegeneratesOnlyInTheGameOfThePlayerWhoSimulatesIt()
    {
        // Regenerated in every game, each player would have energy of their own.
        Assert.IsTrue(BasePowerSources.SetsPowerHere(isEscapePodCell: true, hasId: true, simulatedLocally: true));
        Assert.IsFalse(BasePowerSources.SetsPowerHere(isEscapePodCell: true, hasId: true, simulatedLocally: false));
    }

    [TestMethod]
    public void ACellWithoutIdAndEverySourceThatIsNoCellAreLeftToTheGame()
    {
        // Before its entity has spawned a cell is not synchronized; a base source's level is not set by SetPower in
        // the game, and Nitrox only sets it there for sources it may set.
        Assert.IsTrue(BasePowerSources.SetsPowerHere(isEscapePodCell: true, hasId: false, simulatedLocally: false));
        Assert.IsTrue(BasePowerSources.SetsPowerHere(isEscapePodCell: false, hasId: true, simulatedLocally: false));
    }

    [TestMethod]
    public void ALifepodCellsStateIsSentLikeAnyOtherSource()
    {
        NitroxId cell = new();

        Assert.IsTrue(sources.TryCreateUpdate(cell, new PowerSourceMetadata(20f, 0f, BasePowerSourceType.ESCAPE_POD), 25f, now: 100f, out EntityMetadataUpdate update));

        Assert.AreEqual(BasePowerSourceType.ESCAPE_POD, ((PowerSourceMetadata)update.NewValue).SourceType);
        Assert.IsFalse(sources.TryCreateUpdate(cell, new PowerSourceMetadata(20.1f, 0f, BasePowerSourceType.ESCAPE_POD), 25f, now: 100.5f, out _), "Throttled like the others.");
    }
}
