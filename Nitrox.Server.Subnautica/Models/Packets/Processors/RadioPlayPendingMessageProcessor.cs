using Nitrox.Server.Subnautica.Models.GameLogic;
using Nitrox.Server.Subnautica.Models.Packets.Core;

namespace Nitrox.Server.Subnautica.Models.Packets.Processors;

internal sealed class RadioPlayPendingMessageProcessor(StoryManager storyManager, ILogger<RadioPlayPendingMessageProcessor> logger) : IAuthPacketProcessor<RadioPlayPendingMessage>
{
    private readonly StoryManager storyManager = storyManager;
    private readonly ILogger<RadioPlayPendingMessageProcessor> logger = logger;

    public async Task Process(AuthProcessorContext context, RadioPlayPendingMessage packet)
    {
        if (!storyManager.RemovePlayedRadioMessage(packet.MessageKey))
        {
            logger.ZLogWarning($"A player played the radio message {packet.MessageKey} which is not in the radio queue");
            return;
        }
        await context.SendToOthersAsync(packet);
    }
}
