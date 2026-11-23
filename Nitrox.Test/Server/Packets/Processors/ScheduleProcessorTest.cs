using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Nitrox.Model.Configuration;
using Nitrox.Model.Core;
using Nitrox.Model.DataStructures;
using Nitrox.Model.DataStructures.GameLogic;
using Nitrox.Model.DataStructures.Unity;
using Nitrox.Model.Networking;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.Packets;
using Nitrox.Server.Subnautica.Models.GameLogic;
using Nitrox.Server.Subnautica.Models.Packets.Core;
using Nitrox.Server.Subnautica.Services;
using NSubstitute;

namespace Nitrox.Server.Subnautica.Models.Packets.Processors;

[TestClass]
public sealed class ScheduleProcessorTest
{
    private IPacketSender packetSender;
    private AuthProcessorContext context;
    private ScheduleProcessor processor;

    [TestInitialize]
    public void Init()
    {
        packetSender = Substitute.For<IPacketSender>();
        PdaManager pdaManager = new(NullLogger<PdaManager>.Instance);
        TimeService timeService = new(packetSender, new NtpSyncer(), NullLoggerFactory.Instance, NullLogger<TimeService>.Instance);
        StoryManager storyManager = new(packetSender, pdaManager, timeService, Options.Create(new SubnauticaServerOptions()), NullLogger<StoryManager>.Instance);
        processor = new ScheduleProcessor(new StoryScheduler(packetSender, pdaManager, storyManager, timeService));

        Player player = new(1, 1, "Player", false, null, NitroxVector3.Zero, NitroxQuaternion.Identity, new NitroxId(), Optional.Empty, Perms.PLAYER,
                            new PlayerStatsData(100, 100, 100, 100, 100, 0), SubnauticaGameMode.SURVIVAL, [], [], new Dictionary<string, NitroxId>(), new Dictionary<string, float>(),
                            new Dictionary<string, PingInstancePreference>(), [], false, true);
        context = new AuthProcessorContext(player, packetSender);
    }

    [TestMethod]
    public async Task ScheduledGoalIsPassedOn()
    {
        await processor.Process(context, new Schedule(10000f, "Goal_Later", 0));

        await packetSender.Received(1).SendPacketToOthersAsync(Arg.Any<Schedule>(), Arg.Any<SessionId>());
    }

    [TestMethod]
    public async Task RejectedGoalIsNotPassedOn()
    {
        // Already scheduled, then in the past: the server keeps neither, so the other players must not get them
        await processor.Process(context, new Schedule(10000f, "Goal_Later", 0));
        await processor.Process(context, new Schedule(10000f, "Goal_Later", 0));
        await processor.Process(context, new Schedule(1f, "Goal_Past", 0));

        await packetSender.Received(1).SendPacketToOthersAsync(Arg.Any<Schedule>(), Arg.Any<SessionId>());
    }
}
