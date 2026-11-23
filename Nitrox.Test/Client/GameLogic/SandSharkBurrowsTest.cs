using Nitrox.Model.Core;
using Nitrox.Model.DataStructures;
using Nitrox.Model.DataStructures.Unity;
using Nitrox.Model.Subnautica.Packets;
using NitroxClient.Communication.Abstract;
using NitroxClient.GameLogic;
using NSubstitute;
using static Nitrox.Model.Subnautica.Packets.SandSharkBurrowChanged;

namespace Nitrox.Test.Client.GameLogic;

/// <summary>
///     Who sends a Sand Shark's burrow and who replays it. What a replay does to the shark needs the game.
/// </summary>
[TestClass]
public sealed class SandSharkBurrowsTest
{
    private readonly IPacketSender packetSender = Substitute.For<IPacketSender>();
    private readonly NitroxId sharkId = new();
    private readonly SimulationOwnership simulationOwnership;
    private readonly SandSharkBurrows burrows;

    public SandSharkBurrowsTest()
    {
        IMultiplayerSession multiplayerSession = Substitute.For<IMultiplayerSession>();
        multiplayerSession.Reservation.Returns(new MultiplayerSessionReservation((SessionId)(ushort)1));
        simulationOwnership = new SimulationOwnership(multiplayerSession, packetSender);
        burrows = new SandSharkBurrows(packetSender, simulationOwnership);
    }

    [TestMethod]
    public void SimulatingPlayerSendsWhereTheSharkLiesBuried()
    {
        simulationOwnership.SimulateEntity(sharkId, SimulationLockType.TRANSIENT);
        NitroxVector3 position = new(10f, -40f, 3f);

        burrows.Broadcast(sharkId, BurrowState.BURIED, position, NitroxQuaternion.Identity);

        SandSharkBurrowChanged sent = SentBurrows().Single();
        Assert.AreEqual(sharkId, sent.CreatureId);
        Assert.AreEqual(BurrowState.BURIED, sent.State);
        Assert.AreEqual(position, sent.Position);
    }

    [TestMethod]
    public void CopyOfAnotherPlayersSharkSendsNothingWhenItReplaysABurrow()
    {
        // The replay goes through the same game methods that the simulating player's burrow is sent from
        burrows.Broadcast(sharkId, BurrowState.COVERING, NitroxVector3.Zero, NitroxQuaternion.Identity);
        burrows.Broadcast(sharkId, BurrowState.DUG_UP, NitroxVector3.Zero, NitroxQuaternion.Identity);

        Assert.AreEqual(0, SentBurrows().Length);
    }

    [TestMethod]
    public void OnlyCopiesOfAnotherPlayersSharkReplayABurrow()
    {
        Assert.IsTrue(burrows.ShouldApply(sharkId));

        simulationOwnership.SimulateEntity(sharkId, SimulationLockType.TRANSIENT);

        Assert.IsFalse(burrows.ShouldApply(sharkId), "A late burrow from the previous simulating player would contradict this game's shark.");
    }

    private SandSharkBurrowChanged[] SentBurrows() =>
        packetSender.ReceivedCalls().Select(call => call.GetArguments()[0]).OfType<SandSharkBurrowChanged>().ToArray();
}
