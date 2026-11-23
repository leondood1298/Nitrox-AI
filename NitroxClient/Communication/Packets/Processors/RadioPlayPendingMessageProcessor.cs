using Nitrox.Model.Subnautica.Packets;
using NitroxClient.Communication.Packets.Processors.Core;
using NitroxClient.GameLogic;
using Story;

namespace NitroxClient.Communication.Packets.Processors;

internal sealed class RadioPlayPendingMessageProcessor : IClientPacketProcessor<RadioPlayPendingMessage>
{
    public Task Process(ClientProcessorContext context, RadioPlayPendingMessage packet)
    {
        if (!StoryManager.IsStoryLoaded())
        {
            return Task.CompletedTask;
        }

        StoryGoalManager storyGoalManager = StoryGoalManager.main;
        // The game always plays the head of its queue, which need not be the message the other player played
        if (!PendingRadioMessages.TryMoveToFront(storyGoalManager.pendingRadioMessages, packet.MessageKey))
        {
            Log.Debug($"Radio message {packet.MessageKey} played by another player is not pending here");
            // On the loading screen the snapshot may still bring the message as pending; this keeps it out
            // (PendingRadioMessages.Merge). The server has the goal complete, and a snapshot made later brings it without
            // running what it triggers either.
            storyGoalManager.completedGoals.Add(PendingRadioMessages.GetPlayedGoal(packet.MessageKey));
            return Task.CompletedTask;
        }
        storyGoalManager.ExecutePendingRadioMessage();
        return Task.CompletedTask;
    }
}
