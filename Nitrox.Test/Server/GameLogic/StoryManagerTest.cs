using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Nitrox.Model.Configuration;
using Nitrox.Model.Networking;
using Nitrox.Model.Subnautica.Packets;
using Nitrox.Server.Subnautica.Models.Packets.Core;
using Nitrox.Server.Subnautica.Services;
using Nitrox.Test.Server.Helper;
using NSubstitute;

namespace Nitrox.Server.Subnautica.Models.GameLogic;

[TestClass]
public sealed class StoryManagerTest
{
    private IPacketSender packetSender;
    private TimeService timeService;
    private StoryManager storyManager;

    [TestInitialize]
    public void Init()
    {
        packetSender = Substitute.For<IPacketSender>();
        timeService = new(packetSender, new NtpSyncer(), NullLoggerFactory.Instance, NullLogger<TimeService>.Instance);
        storyManager = new StoryManager(packetSender, new PdaManager(NullLogger<PdaManager>.Instance), timeService, Options.Create(new SubnauticaServerOptions()), NullLogger<StoryManager>.Instance);
    }

    [TestMethod]
    public void RemovePlayedRadioMessage_RemovesThePlayedMessageEvenIfNotFirst()
    {
        storyManager.QueueRadioStory("RadioFirst");
        storyManager.QueueRadioStory("RadioSecond");

        storyManager.RemovePlayedRadioMessage("RadioSecond").Should().BeTrue();

        storyManager.StoryGoalData.RadioQueue.Should().Equal("RadioFirst");
        storyManager.ContainsCompletedStory("OnPlayRadioSecond").Should().BeTrue();
    }

    [TestMethod]
    public void RemovePlayedRadioMessage_MessageNotQueued_ChangesNothing()
    {
        storyManager.QueueRadioStory("RadioFirst");

        storyManager.RemovePlayedRadioMessage("RadioOther").Should().BeFalse();

        storyManager.StoryGoalData.RadioQueue.Should().Equal("RadioFirst");
        storyManager.ContainsCompletedStory("OnPlayRadioOther").Should().BeFalse();
    }

    [TestMethod]
    public void OnGoalCompleted_GunDisabled_RemovesTheLastSunbeamMessage()
    {
        storyManager.QueueRadioStory("RadioFirst");
        storyManager.QueueRadioStory(StoryManager.SUNBEAM_LAST_RADIO_MESSAGE);

        storyManager.OnGoalCompleted(StoryManager.GUN_DISABLED_GOAL);

        storyManager.StoryGoalData.RadioQueue.Should().Equal("RadioFirst");
        storyManager.ContainsCompletedStory($"OnPlay{StoryManager.SUNBEAM_LAST_RADIO_MESSAGE}").Should().BeFalse();
    }

    [TestMethod]
    public void OnGoalCompleted_OtherGoal_KeepsTheRadioQueue()
    {
        storyManager.QueueRadioStory(StoryManager.SUNBEAM_LAST_RADIO_MESSAGE);

        storyManager.OnGoalCompleted("Goal_Something");

        storyManager.StoryGoalData.RadioQueue.Should().Equal(StoryManager.SUNBEAM_LAST_RADIO_MESSAGE);
    }

    [TestMethod]
    public async Task TimeSkip_SendsTheMovedAuroraExplosionTimeWithTheNewTime()
    {
        storyManager.AuroraRealExplosionTime = timeService.ActiveTime + TimeSpan.FromSeconds(600);

        await timeService.SkipTimeAsync(TimeSpan.FromSeconds(100));

        AuroraAndTimeUpdate update = ConnectedPlayers.SentToAll<AuroraAndTimeUpdate>(packetSender).Should().ContainSingle().Subject;
        update.Restore.Should().BeFalse();
        update.TimeData.AuroraEventData.AuroraRealExplosionTime.Should().BeApproximately((float)(timeService.ActiveTime.TotalSeconds + 500), 0.1f);
        update.TimeData.TimePacket.CurrentTime.Should().BeApproximately(timeService.GameTime.TotalSeconds, 1);
        ConnectedPlayers.SentToAll<TimeChange>(packetSender).Should().BeEmpty();
    }
}