using Nitrox.Model.Subnautica.Packets;
using NitroxClient.Communication.Packets.Processors.Core;
using NitroxClient.GameLogic;
using Story;

namespace NitroxClient.Communication.Packets.Processors;

internal sealed class RadioPlayPendingMessageProcessor : IClientPacketProcessor<RadioPlayPendingMessage>
{
    public Task Process(ClientProcessorContext context, RadioPlayPendingMessage packet)
    {
        StoryGoalManager storyGoalManager = StoryGoalManager.main;
        // The game always plays the head of its queue, which need not be the message the other player played
        if (!PendingRadioMessages.TryMoveToFront(storyGoalManager.pendingRadioMessages, packet.MessageKey))
        {
            Log.Debug($"Radio message {packet.MessageKey} played by another player is not pending here");
            return Task.CompletedTask;
        }
        storyGoalManager.ExecutePendingRadioMessage();
        return Task.CompletedTask;
    }
}
