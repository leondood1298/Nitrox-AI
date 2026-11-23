using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Nitrox.Model.Configuration;
using Nitrox.Model.Networking;
using Nitrox.Model.Subnautica.Packets;
using Nitrox.Server.Subnautica.Models.GameLogic;
using Nitrox.Server.Subnautica.Models.Packets.Core;
using Nitrox.Server.Subnautica.Services;
using Nitrox.Test.Server.Helper;
using NSubstitute;

namespace Nitrox.Server.Subnautica.Models.Packets.Processors;

/// <summary>
///     A story goal a player's game completed is stored and passed on to the others once.
/// </summary>
[TestClass]
public sealed class StoryGoalExecutedProcessorTest
{
    private readonly ConnectedPlayers players = new();
    private IPacketSender packetSender;
    private StoryManager storyManager;
    private PdaManager pdaManager;
    private StoryGoalExecutedProcessor processor;
    private Player first;
    private Player second;

    [TestInitialize]
    public void Init()
    {
        packetSender = Substitute.For<IPacketSender>();
        pdaManager = new PdaManager(NullLogger<PdaManager>.Instance);
        TimeService timeService = new(packetSender, new NtpSyncer(), NullLoggerFactory.Instance, NullLogger<TimeService>.Instance);
        storyManager = new StoryManager(packetSender, pdaManager, timeService, Options.Create(new SubnauticaServerOptions()), NullLogger<StoryManager>.Instance);
        StoryScheduler storyScheduler = new(packetSender, pdaManager, storyManager, timeService);
        processor = new StoryGoalExecutedProcessor(packetSender, storyManager, storyScheduler, pdaManager, NullLogger<StoryGoalExecutedProcessor>.Instance);
        first = players.Connect("First");
        second = players.Connect("Second");
    }

    [TestMethod]
    [DataRow(StoryGoalExecuted.EventType.STORY)]
    [DataRow(StoryGoalExecuted.EventType.RADIO)]
    [DataRow(StoryGoalExecuted.EventType.PDA)]
    [DataRow(StoryGoalExecuted.EventType.ENCYCLOPEDIA)]
    public async Task ANewGoalIsStoredAndPassedOn(StoryGoalExecuted.EventType type)
    {
        StoryGoalExecuted packet = new("Goal_New", type, 100f);

        await processor.Process(new AuthProcessorContext(first, packetSender), packet);

        storyManager.ContainsCompletedStory("Goal_New").Should().BeTrue();
        await packetSender.Received(1).SendPacketToOthersAsync(packet, first.SessionId);
    }

    [TestMethod]
    [DataRow(StoryGoalExecuted.EventType.STORY)]
    [DataRow(StoryGoalExecuted.EventType.RADIO)]
    [DataRow(StoryGoalExecuted.EventType.PDA)]
    [DataRow(StoryGoalExecuted.EventType.ENCYCLOPEDIA)]
    public async Task AGoalTheServerHadCompleteIsNotPassedOnAgain(StoryGoalExecuted.EventType type)
    {
        // Each game raises the Aurora warnings itself, for instance, and reports them
        await processor.Process(new AuthProcessorContext(first, packetSender), new StoryGoalExecuted("Goal_Twice", type, 100f));
        packetSender.ClearReceivedCalls();

        await processor.Process(new AuthProcessorContext(second, packetSender), new StoryGoalExecuted("Goal_Twice", type, 101f));

        await packetSender.DidNotReceive().SendPacketToOthersAsync(Arg.Any<StoryGoalExecuted>(), Arg.Any<Nitrox.Model.Core.SessionId>());
        storyManager.StoryGoalData.RadioQueue.Count.Should().Be(type == StoryGoalExecuted.EventType.RADIO ? 1 : 0, "a radio message is queued once");
    }
}
