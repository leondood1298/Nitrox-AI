using Nitrox.Model.Core;
using Nitrox.Model.DataStructures;
using Nitrox.Model.DataStructures.Unity;
using Nitrox.Model.Packets;
using Nitrox.Model.Subnautica.Packets;
using NitroxClient.Communication;
using static NitroxClient.Communication.LoadingPacketGate;

namespace Nitrox.Test.Client.Communication;

/// <summary>
///     Packets that arrive before the game is in the multiplayer world: held and replayed in order, dropped, or let
///     through.
/// </summary>
[TestClass]
public sealed class LoadingPacketGateTest
{
    [TestMethod]
    public void BeforeJoiningOnlyTheSessionsPacketsPass()
    {
        LoadingPacketGate gate = new();

        gate.Admit(new PlayerKicked("bye")).Should().BeTrue();
        gate.Admit(new EntityDestroyed(new NitroxId())).Should().BeFalse();
        gate.HeldCount.Should().Be(0, "the snapshot gathered later already has what happened before the game asked to join");
    }

    [TestMethod]
    public void WhileLoadingWorldPacketsAreHeldAndReplayedInOrder()
    {
        LoadingPacketGate gate = new();
        gate.OnJoinRequested();
        EntityDestroyed first = new(new NitroxId());
        Disconnect second = new((SessionId)5);
        EntityDestroyed third = new(new NitroxId());

        gate.Admit(first).Should().BeFalse();
        gate.Admit(second).Should().BeFalse();
        gate.Admit(third).Should().BeFalse();
        List<Packet> released = gate.Release();

        released.Should().Equal(first, second, third);
        gate.CurrentPhase.Should().Be(LoadingPacketGate.Phase.IN_GAME);
        gate.Admit(new EntityDestroyed(new NitroxId())).Should().BeTrue();
    }

    [TestMethod]
    public void TheSnapshotItselfPassesWhileLoading()
    {
        LoadingPacketGate gate = new();
        gate.OnJoinRequested();

        PolicyOf(typeof(InitialPlayerSync)).Should().Be(Policy.IMMEDIATE);
        PolicyOf(typeof(JoinQueueInfo)).Should().Be(Policy.IMMEDIATE);
        PolicyOf(typeof(ServerStopped)).Should().Be(Policy.IMMEDIATE);
    }

    [TestMethod]
    public void PassingThingsAreDroppedWhileLoading()
    {
        LoadingPacketGate gate = new();
        gate.OnJoinRequested();

        gate.Admit(new PlayerMovement((SessionId)2, NitroxVector3.Zero, NitroxVector3.Zero, NitroxQuaternion.Identity, NitroxQuaternion.Identity)).Should().BeFalse();

        gate.HeldCount.Should().Be(0);
        PolicyOf(typeof(PlayerStats)).Should().Be(Policy.DROP);
        PolicyOf(typeof(FMODAssetPacket)).Should().Be(Policy.DROP);
    }

    [TestMethod]
    public void ALoadingPlayerIsNeverPutIntoTheSleepScreen()
    {
        PolicyOf(typeof(SleepStatusUpdate)).Should().Be(Policy.DROP);
        PolicyOf(typeof(SleepComplete)).Should().Be(Policy.DROP);
    }

    [TestMethod]
    public void BuildingAndWorldChangesAreHeldByDefault()
    {
        PolicyOf(typeof(UpdateBase)).Should().Be(Policy.HOLD);
        PolicyOf(typeof(PlaceGhost)).Should().Be(Policy.HOLD);
        PolicyOf(typeof(EntityReparented)).Should().Be(Policy.HOLD);
        PolicyOf(typeof(PlayerJoinedMultiplayerSession)).Should().Be(Policy.HOLD);
        PolicyOf(typeof(Disconnect)).Should().Be(Policy.HOLD);
        PolicyOf(typeof(PermsChanged)).Should().Be(Policy.HOLD);
    }
}
