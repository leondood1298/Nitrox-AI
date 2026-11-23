using Nitrox.Model.Core;
using Nitrox.Model.DataStructures;
using Nitrox.Model.DataStructures.Unity;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;
using Nitrox.Model.Subnautica.Packets;
using NitroxClient.Communication.Abstract;
using NitroxClient.GameLogic;
using NitroxClient.GameLogic.Spawning.Metadata.Extractor;
using NSubstitute;

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
        burrows = new SandSharkBurrows(packetSender, simulationOwnership, new SandSharkMetadataExtractor());
    }

    [TestMethod]
    public void SimulatingPlayerStoresThatTheSharkLiesBuriedAsItsMetadata()
    {
        simulationOwnership.SimulateEntity(sharkId, SimulationLockType.TRANSIENT);
        NitroxVector3 position = new(10f, -40f, 3f);

        burrows.BroadcastState(sharkId, new SandSharkMetadata(true, position, NitroxQuaternion.Identity, CreatureMetadata.UNDECIDED));

        EntityMetadataUpdate sent = Sent<EntityMetadataUpdate>().Single();
        Assert.AreEqual(sharkId, sent.Id);
        SandSharkMetadata state = (SandSharkMetadata)sent.NewValue;
        Assert.IsTrue(state.Buried);
        Assert.AreEqual(position, state.BurryPosition);
    }

    [TestMethod]
    public void SimulatingPlayerSendsWhereTheSandStartsCoveringTheShark()
    {
        simulationOwnership.SimulateEntity(sharkId, SimulationLockType.TRANSIENT);

        burrows.BroadcastCoverStarted(sharkId, new NitroxVector3(1f, 2f, 3f), NitroxQuaternion.Identity);

        Assert.AreEqual(sharkId, Sent<SandSharkCoverStarted>().Single().CreatureId);
    }

    [TestMethod]
    public void CopyOfAnotherPlayersSharkSendsNothingWhenItReplaysABurrow()
    {
        // The replay goes through the same game methods that the simulating player's burrow is sent from
        burrows.BroadcastCoverStarted(sharkId, NitroxVector3.Zero, NitroxQuaternion.Identity);
        burrows.BroadcastState(sharkId, new SandSharkMetadata(false, NitroxVector3.Zero, NitroxQuaternion.Identity, CreatureMetadata.UNDECIDED));

        Assert.AreEqual(0, packetSender.ReceivedCalls().Count());
    }

    [TestMethod]
    public void OnlyCopiesOfAnotherPlayersSharkReplayTheSandCovering()
    {
        Assert.IsTrue(burrows.ShouldApply(sharkId));

        simulationOwnership.SimulateEntity(sharkId, SimulationLockType.TRANSIENT);

        Assert.IsFalse(burrows.ShouldApply(sharkId), "A late burrow from the previous simulating player would contradict this game's shark.");
    }

    private T[] Sent<T>() => packetSender.ReceivedCalls().Select(call => call.GetArguments()[0]).OfType<T>().ToArray();
}
