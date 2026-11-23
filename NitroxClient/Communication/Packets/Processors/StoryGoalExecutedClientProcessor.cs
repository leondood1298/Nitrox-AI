using Nitrox.Model.Subnautica.Packets;
using NitroxClient.Communication.Abstract;
using NitroxClient.Communication.Packets.Processors.Core;
using NitroxClient.GameLogic;
using Story;

namespace NitroxClient.Communication.Packets.Processors;

internal sealed class StoryGoalExecutedClientProcessor(IPacketSender packetSender) : IClientPacketProcessor<StoryGoalExecuted>
{
    private readonly IPacketSender packetSender = packetSender;

    public Task Process(ClientProcessorContext context, StoryGoalExecuted packet)
    {
        // The sender's game picked this line from its player's position, this game picks its own. The schedule is left
        // alone too: an entry with this key is this game's own pick.
        if (StoryManager.IsSunbeamDestroyEventPickedByPosition(packet.Key))
        {
            return Task.CompletedTask;
        }

        StoryGoalScheduler.main.schedule.RemoveAllFast(packet.Key, static (goal, packetGoalKey) => goal.goalKey == packetGoalKey);

        using (PacketSuppressor<StoryGoalExecuted>.Suppress())
        using (PacketSuppressor<PDALogEntryAdd>.Suppress())
        using (PacketSuppressor<KnownTechEntryAdd>.Suppress()) // StoryGoalManager => OnGoalUnlockTracker => UnlockBlueprintData => KnownTech.Add
        using (PacketSuppressor<PDAEncyclopediaEntryAdd>.Suppress())
        {
            StoryGoal.Execute(packet.Key, packet.Type.ToUnity());
        }
        return Task.CompletedTask;
    }
}
