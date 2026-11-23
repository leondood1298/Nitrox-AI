using System.Threading.Tasks;
using Nitrox.Model.DataStructures;
using Nitrox.Model.DataStructures.GameLogic;
using Nitrox.Model.DataStructures.Unity;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.Packets;
using Nitrox.Server.Subnautica.Models.Packets.Core;
using NSubstitute;

namespace Nitrox.Server.Subnautica.Models.Packets.Processors;

[TestClass]
public sealed class GoalCompletedProcessorTest
{
    [TestMethod]
    public async Task RepeatedGoalKeepsTheFirstCompletionTimeWithoutThrowing()
    {
        Player player = new(1, 1, "Player", false, null, NitroxVector3.Zero, NitroxQuaternion.Identity, new NitroxId(), Optional.Empty, Perms.PLAYER,
                            new PlayerStatsData(100, 100, 100, 100, 100, 0), SubnauticaGameMode.SURVIVAL, [], [], new Dictionary<string, NitroxId>(), new Dictionary<string, float>(),
                            new Dictionary<string, PingInstancePreference>(), [], false, true);
        AuthProcessorContext context = new(player, Substitute.For<IPacketSender>());
        GoalCompletedProcessor processor = new();

        await processor.Process(context, new GoalCompleted("Goal_Scanner", 10f));
        await processor.Process(context, new GoalCompleted("Goal_Scanner", 20f));

        player.PersonalCompletedGoalsWithTimestamp["Goal_Scanner"].Should().Be(10f);
    }
}
