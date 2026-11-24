using System.Net;
using System.Threading.Tasks;
using Nitrox.Model.Core;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.Packets;
using Nitrox.Server.Subnautica.Models.AppEvents;
using Nitrox.Server.Subnautica.Models.Communication;
using Nitrox.Server.Subnautica.Models.GameLogic;

namespace Nitrox.Test.Server.GameLogic;

/// <summary>
///     A joining player is told what the others hold and where they sit, as those are only announced when they change.
/// </summary>
[TestClass]
public sealed class PlayerHandAndSeatStatesTest
{
    private static readonly SessionId alice = 1;
    private static readonly SessionId joiner = 2;

    [TestMethod]
    public void AJoinerIsToldWhatOthersHoldWithoutTheFirstUseFlag()
    {
        PlayerHandAndSeatStates states = new();
        NitroxId knife = new();

        states.OnHeldItemChanged(alice, new PlayerHeldItemChanged(alice, knife, PlayerHeldItemChanged.ChangeType.DRAW_AS_TOOL, new("Knife")));

        PlayerHeldItemChanged told = states.GetAnnouncementsFor(joiner).OfType<PlayerHeldItemChanged>().Single();
        told.ItemId.Should().Be(knife);
        told.Type.Should().Be(PlayerHeldItemChanged.ChangeType.DRAW_AS_TOOL);
        told.IsFirstTime.Should().BeNull();
        states.GetAnnouncementsFor(alice).Should().BeEmpty("a player is not told about themselves");
    }

    [TestMethod]
    public void AHolsteredItemIsNoLongerAnnounced()
    {
        PlayerHandAndSeatStates states = new();
        NitroxId knife = new();
        states.OnHeldItemChanged(alice, new PlayerHeldItemChanged(alice, knife, PlayerHeldItemChanged.ChangeType.DRAW_AS_TOOL, null));

        states.OnHeldItemChanged(alice, new PlayerHeldItemChanged(alice, knife, PlayerHeldItemChanged.ChangeType.HOLSTER_AS_TOOL, null));

        states.GetAnnouncementsFor(joiner).Should().BeEmpty();
    }

    [TestMethod]
    public void AJoinerIsToldWhoSitsWhereUntilTheyStandUp()
    {
        PlayerHandAndSeatStates states = new();
        NitroxId bench = new();

        states.OnBenchChanged(alice, new BenchChanged(alice, bench, BenchChanged.BenchChangeState.SITTING_DOWN));

        states.GetAnnouncementsFor(joiner).OfType<BenchChanged>().Should().ContainSingle(packet => packet.BenchId == bench && packet.ChangeState == BenchChanged.BenchChangeState.SITTING_DOWN);

        states.OnBenchChanged(alice, new BenchChanged(alice, bench, BenchChanged.BenchChangeState.STANDING_UP));
        states.GetAnnouncementsFor(joiner).Should().BeEmpty();
    }

    [TestMethod]
    public void AJoinerSeesAWelderInUseAfterItIsInTheHand()
    {
        PlayerHandAndSeatStates states = new();
        NitroxId welder = new();
        states.OnHeldItemChanged(alice, new PlayerHeldItemChanged(alice, welder, PlayerHeldItemChanged.ChangeType.DRAW_AS_TOOL, null));

        states.OnWelderUse(alice, true);

        List<Nitrox.Model.Packets.Packet> told = states.GetAnnouncementsFor(joiner).ToList();
        told.Should().HaveCount(2);
        told[0].Should().BeOfType<PlayerHeldItemChanged>();
        told[1].Should().BeOfType<WelderUse>().Which.Welding.Should().BeTrue();
    }

    [TestMethod]
    public void PuttingTheToolAwayEndsItsWeldingAndItsGrab()
    {
        PlayerHandAndSeatStates states = new();
        NitroxId cannon = new();
        states.OnHeldItemChanged(alice, new PlayerHeldItemChanged(alice, cannon, PlayerHeldItemChanged.ChangeType.DRAW_AS_TOOL, null));
        states.OnWelderUse(alice, true);
        states.OnPropulsionGrab(alice, new NitroxId());

        states.OnHeldItemChanged(alice, new PlayerHeldItemChanged(alice, cannon, PlayerHeldItemChanged.ChangeType.HOLSTER_AS_TOOL, null));

        states.GetAnnouncementsFor(joiner).Should().BeEmpty();
    }

    [TestMethod]
    public void AJoinerIsToldWhatACannonHoldsUntilItLetsGo()
    {
        PlayerHandAndSeatStates states = new();
        NitroxId crate = new();

        states.OnPropulsionGrab(alice, crate);
        states.GetAnnouncementsFor(joiner).OfType<PlayerPropulsionGrab>().Should().ContainSingle(packet => packet.GrabbedId == crate && packet.SessionId == alice);

        states.OnPropulsionGrab(alice, null);
        states.GetAnnouncementsFor(joiner).Should().BeEmpty();
    }

    [TestMethod]
    public void AJoinerIsShownACinematicUnderWayUntilItEnds()
    {
        PlayerHandAndSeatStates states = new();
        NitroxId hatch = new();

        states.OnCinematic(alice, new PlayerCinematicControllerCall(alice, hatch, 5, "hatch", true));
        states.GetAnnouncementsFor(joiner).OfType<PlayerCinematicControllerCall>().Should().ContainSingle(call => call.ControllerID == hatch && call.StartPlaying);

        states.OnCinematic(alice, new PlayerCinematicControllerCall(alice, hatch, 5, "hatch", false));
        states.GetAnnouncementsFor(joiner).Should().BeEmpty();
    }

    [TestMethod]
    public async Task APlayerWhoLeavesOrDiesIsForgotten()
    {
        PlayerHandAndSeatStates states = new();
        states.OnBenchChanged(alice, new BenchChanged(alice, new NitroxId(), BenchChanged.BenchChangeState.SITTING_DOWN));
        states.OnHeldItemChanged(joiner, new PlayerHeldItemChanged(joiner, new NitroxId(), PlayerHeldItemChanged.ChangeType.DRAW_AS_ITEM, null));
        states.OnWelderUse(alice, true);
        states.OnCinematic(joiner, new PlayerCinematicControllerCall(joiner, new NitroxId(), 1, "ladder", true));

        await states.OnEventAsync(new ISessionCleaner.Args(new SessionManager.Session(alice, new IPEndPoint(IPAddress.Loopback, 1)), 1));
        states.PlayerDied(joiner);

        states.GetAnnouncementsFor(3).Should().BeEmpty();
    }
}
