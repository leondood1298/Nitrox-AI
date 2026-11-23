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
    public async Task APlayerWhoLeavesOrDiesIsForgotten()
    {
        PlayerHandAndSeatStates states = new();
        states.OnBenchChanged(alice, new BenchChanged(alice, new NitroxId(), BenchChanged.BenchChangeState.SITTING_DOWN));
        states.OnHeldItemChanged(joiner, new PlayerHeldItemChanged(joiner, new NitroxId(), PlayerHeldItemChanged.ChangeType.DRAW_AS_ITEM, null));

        await states.OnEventAsync(new ISessionCleaner.Args(new SessionManager.Session(alice, new IPEndPoint(IPAddress.Loopback, 1)), 1));
        states.PlayerDied(joiner);

        states.GetAnnouncementsFor(3).Should().BeEmpty();
    }
}
