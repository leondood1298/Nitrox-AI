using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Nitrox.Model.Configuration;
using Nitrox.Model.Core;
using Nitrox.Model.Subnautica.Packets;
using Nitrox.Server.Subnautica.Models.GameLogic;
using Nitrox.Server.Subnautica.Models.Packets.Core;
using Nitrox.Test.Server.Helper;
using NSubstitute;
using ServerPlayer = Nitrox.Server.Subnautica.Models.Player;

namespace Nitrox.Test.Server.GameLogic;

/// <summary>
///     The join queue waits for each player to report that it applied the snapshot, and stops waiting when it leaves
///     or takes too long.
/// </summary>
[TestClass]
public sealed class JoiningManagerTest
{
    private static readonly TimeSpan poll = TimeSpan.FromMilliseconds(5);

    [TestMethod]
    public async Task AReportedSyncFinishes()
    {
        TaskCompletionSource finished = new();
        finished.SetResult();

        JoiningManager.SyncOutcome outcome = await JoiningManager.WaitForSyncAsync(finished.Task, () => true, TimeSpan.FromMinutes(1), poll);

        outcome.Should().Be(JoiningManager.SyncOutcome.FINISHED);
    }

    [TestMethod]
    public async Task ALeavingPlayerIsNoticed()
    {
        JoiningManager.SyncOutcome outcome = await JoiningManager.WaitForSyncAsync(new TaskCompletionSource().Task, () => false, TimeSpan.FromMinutes(1), poll);

        outcome.Should().Be(JoiningManager.SyncOutcome.DISCONNECTED);
    }

    [TestMethod]
    public async Task ATimeoutIsReportedInsteadOfThrown()
    {
        // The old wait threw when the timeout cancelled it, so the player who took too long was never kicked
        JoiningManager.SyncOutcome outcome = await JoiningManager.WaitForSyncAsync(new TaskCompletionSource().Task, () => true, TimeSpan.FromMilliseconds(30), poll);

        outcome.Should().Be(JoiningManager.SyncOutcome.TIMED_OUT);
    }

    [TestMethod]
    public async Task APlayerWhoFinishesAfterTheQueueStoppedWaitingIsStillAnnounced()
    {
        ConnectedPlayers players = new();
        ServerPlayer late = players.Connect("Late");
        IPacketSender packetSender = Substitute.For<IPacketSender>();
        JoiningManager joiningManager = new(packetSender, players.PlayerManager, players.SessionManager, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!,
                                            Options.Create(new SubnauticaServerOptions()), NullLogger<JoiningManager>.Instance);

        bool wasAwaited = await joiningManager.SyncFinishedAsync(late);

        wasAwaited.Should().BeFalse();
        await packetSender.Received(1).SendPacketToOthersAsync(Arg.Is<PlayerJoinedMultiplayerSession>(packet => packet.PlayerContext.SessionId == late.SessionId), late.SessionId);
    }
}
