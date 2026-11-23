using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.Packets;
using NitroxClient.Communication.Abstract;
using NSubstitute;

namespace NitroxClient.GameLogic;

[TestClass]
public sealed class BasePowerConsumptionForwarderTest
{
    private readonly IPacketSender packetSender = Substitute.For<IPacketSender>();
    private readonly SimulationOwnership simulationOwnership;
    private readonly BasePowerConsumptionForwarder forwarder;

    public BasePowerConsumptionForwarderTest()
    {
        simulationOwnership = new SimulationOwnership(Substitute.For<IMultiplayerSession>(), packetSender);
        forwarder = new BasePowerConsumptionForwarder(packetSender, simulationOwnership);
    }

    [TestMethod]
    public void DrawFromAnotherPlayersSourceInsideConsumerIsReportedOnce()
    {
        NitroxId reactor = new();

        forwarder.EnterConsumer();
        forwarder.RecordConsumption(reactor, 2f);
        forwarder.RecordConsumption(reactor, 3f);
        forwarder.ExitConsumer();
        forwarder.Flush(10f);
        forwarder.Flush(20f);

        packetSender.Received(1).Send(Arg.Is<BasePowerSourceConsume>(report => report.SourceId.Equals(reactor) && report.Amount == 5f));
        Assert.AreEqual(1, packetSender.ReceivedCalls().Count());
    }

    [TestMethod]
    public void DrawFromSourceThisClientSimulatesIsNotReported()
    {
        // Its own simulation of the source already contains the draw and broadcasts it.
        NitroxId reactor = new();
        simulationOwnership.SimulateEntity(reactor, SimulationLockType.TRANSIENT);

        forwarder.EnterConsumer();
        forwarder.RecordConsumption(reactor, 2f);
        forwarder.ExitConsumer();
        forwarder.Flush(10f);

        Assert.AreEqual(0, packetSender.ReceivedCalls().Count());
    }

    [TestMethod]
    public void LevelChangeOutsideAConsumerIsNotReported()
    {
        // For example the level being set from the owner's broadcast.
        forwarder.RecordConsumption(new NitroxId(), 2f);
        forwarder.Flush(10f);

        Assert.AreEqual(0, packetSender.ReceivedCalls().Count());
    }

    [TestMethod]
    public void ConsumerScopeEndsWithItsOutermostExit()
    {
        Assert.IsFalse(forwarder.IsConsumerActive);
        forwarder.EnterConsumer();
        forwarder.EnterConsumer();
        forwarder.ExitConsumer();
        Assert.IsTrue(forwarder.IsConsumerActive);
        forwarder.ExitConsumer();
        Assert.IsFalse(forwarder.IsConsumerActive);

        forwarder.ExitConsumer();
        forwarder.EnterConsumer();
        Assert.IsTrue(forwarder.IsConsumerActive, "An exit without an enter must not cancel the next consumer.");
    }

    [TestMethod]
    public void OnlyThePlayerSimulatingASharedConsumerDrawsForIt()
    {
        // Spotlights, floodlights and filtration machines update on every client.
        Assert.AreEqual(SharedConsumerDraw.DRAW_AND_REPORT, BasePowerConsumptionForwarder.GetSharedConsumerDraw(hasId: true, simulatedLocally: true));
        Assert.AreEqual(SharedConsumerDraw.NONE, BasePowerConsumptionForwarder.GetSharedConsumerDraw(hasId: true, simulatedLocally: false));
    }

    [TestMethod]
    public void ConsumerWithoutAnIdBehavesAsInTheGame()
    {
        Assert.AreEqual(SharedConsumerDraw.LOCAL_ONLY, BasePowerConsumptionForwarder.GetSharedConsumerDraw(hasId: false, simulatedLocally: false));
        Assert.AreEqual(SharedConsumerDraw.LOCAL_ONLY, BasePowerConsumptionForwarder.GetSharedConsumerDraw(hasId: false, simulatedLocally: true));
    }
}
